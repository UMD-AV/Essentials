using System;
using System.Globalization;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace ExtronDmp
{
	public class ExtronDmpLevelControl : IBasicVolumeWithFeedback, IKeyed
	{
		public BoolFeedback MuteFeedback { get; private set; }
        public IntFeedback VolumeLevelFeedback { get; private set; }
		public bool Enabled { get; private set; }
        public bool HasMute { get; private set; }
        public bool HasLevel { get; private set; }
        public ePdtLevelTypes Type { get; private set; }
        public int MuteGroup { get; private set; }
        public string Key { get; private set; }

		/// <summary>
		/// Used for debug
		/// </summary>
		public string LevelCustomName { get; private set; }

		private float minLevel = -65;
		private float maxLevel= 20;
        private readonly ExtronDmp _parent;
        private bool _isMuted;
        private ushort _volumeLevel;
        private string _commandSuffix;
		private string _levelPrefix;
		private string _mutePrefix;
        private bool _useSisVolume;

		const ushort _rampResetTime = 30;
		CTimer _volumeUpRepeatTimer;
		CTimer _volumeDownRepeatTimer;
        CMutex _volumeUpLock;
        CMutex _volumeDownLock;
        private ushort _volumeUpCount;
        private ushort _volumeDownCount;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="key">instance key</param>
		/// <param name="config">level control block configuration object</param>
		/// <param name="parent">dsp parent isntance</param>
		public ExtronDmpLevelControl(string key, ExtronDmpControlBlockConfig config, ExtronDmp parent)
		{
		    _parent = parent;
            HasLevel = false;
            HasMute = false;
            MuteGroup = 0;
            _volumeUpCount = 0;
            _volumeDownCount = 0;
            Type = config.IsMic == true ? ePdtLevelTypes.Microphone : ePdtLevelTypes.Speaker;
            LevelCustomName = config.Label;
            key = string.Format("{0}-{1}", _parent.Key, key);

            if (config.Disabled == true)
            {
                Enabled = false;
                return;
            }

            Enabled = true;
            Debug.Console(2, this, "Adding LevelControl '{0}'", key);
            MuteFeedback = new BoolFeedback(() => _isMuted);
            VolumeLevelFeedback = new IntFeedback(() => _volumeLevel);            

            if (config.ControlId != null)
            {
                //Set up normal fader using control Id
                _commandSuffix = "AU";
                _useSisVolume = true;
                HasLevel = true;
                HasMute = true;
                _levelPrefix = "G" + config.ControlId;
                _mutePrefix = "M" + config.ControlId;                
            }

            else
            {
                _commandSuffix = "GRPM";
                _useSisVolume = false;
                if (config.LevelGroup != null)
                {
                    HasLevel = true;
                    _levelPrefix = string.Format("D{0}", config.LevelGroup.Value);
                }
                if (config.MuteGroup != null)
                {
                    MuteGroup = config.MuteGroup.Value;
                    HasMute = true;
                    _mutePrefix = string.Format("D{0}", config.MuteGroup.Value);
                }
            }

            _volumeUpRepeatTimer = new CTimer(VolumeUpRepeat, Timeout.Infinite);
            _volumeDownRepeatTimer = new CTimer(VolumeDownRepeat, Timeout.Infinite);
            if (config.Min != null) { minLevel = (int)config.Min; }
            if (config.Max != null) { maxLevel = (int)config.Max; }
		}

        /// <summary>
        /// Sends a command to the DSP
        /// </summary>
        /// <param name="cmd">command</param>
        /// <param name="instance">named control/instance tag</param>
        /// <param name="value">value (use "" if not applicable)</param>
        public void SendFullCommand(string objectId, string level, string suffix)
        {
            string cmdToSemd = string.Format("{0}{1}*{2}{3}", '\x1B', objectId, level, suffix);

            _parent.SendLine(cmdToSemd);
        }

        public void SendFullCommand(string objectId, Double level, string suffix)
        {
            string cmdToSemd = string.Format("{0}{1}*{2}{3}", '\x1B', objectId, level, suffix);

            _parent.SendLine(cmdToSemd);
        }

		/// <summary>
		/// Parses the response from the DSP. Command is "MUTE, GAIN, MINMAX, erc. Values[] is the returned values after the channel and group.
		/// </summary>
		/// <param name="command"></param>
		/// <param name="values"></param>
		public void ParseResponse(string command, string response)
		{
            Debug.Console(1, this, "Parsing command: {0} response: {1}", command, response);
			
			if(command == "D") // Group Level or Mute Response 
			{
                string[] responseArray = response.Substring(5).Split('*');
                int group = int.Parse(responseArray[0]);
                int value = int.Parse(responseArray[1]);
				if (group == MuteGroup)
				{
					if(value == 1)
					{
						_isMuted = true;
					}
					else if(value == 0)
					{
						_isMuted = false;
					}

					MuteFeedback.FireUpdate();
					return;
				}
				else if(this.HasLevel)
				{
                    float vol = value / 10;
					if (vol >= maxLevel)
						_volumeLevel = ushort.MaxValue;
                    else if (vol <= minLevel)
						_volumeLevel = ushort.MinValue;
                    else
						_volumeLevel = (ushort)(((vol - minLevel) * ushort.MaxValue) / (maxLevel - minLevel));
                    Debug.Console(1, this, "Level {0} VolumeLevel: '{1}'", LevelCustomName, _volumeLevel);
                
					VolumeLevelFeedback.FireUpdate();
					return;
				}			
			}
			else if(command == "L" && this.HasLevel) // Soft Limit response
			{
                string[] responseArray = response.Substring(5).Split('*');
                maxLevel = int.Parse(responseArray[1]) / 10;
                minLevel = int.Parse(responseArray[2]) / 10;
				Debug.Console(1, this, "Level {0} new min: {1}, new max: {2}", LevelCustomName, minLevel, maxLevel);
			}
            else if (command == "G") // Trim/gain response
            {
                int starPos = response.IndexOf('*');
                string value = response.Substring(starPos + 1);

                //Use Sis type Volume for trim/gain controls, unless it is a DMP Plus model. All other cases use dB*10
                float vol;
                if (_useSisVolume)
                {
                    vol = (int.Parse(value) - 2048) / 10;
                }
                else
                {
                    vol = int.Parse(value) / 10;
                }

                if (vol >= maxLevel)
                    _volumeLevel = ushort.MaxValue;
                else if (vol <= minLevel)
                    _volumeLevel = ushort.MinValue;
                else
                    _volumeLevel = (ushort)(((vol - minLevel) * ushort.MaxValue) / (maxLevel - minLevel));
                Debug.Console(1, this, "Level {0} VolumeLevel: '{1}'", LevelCustomName, _volumeLevel);

                VolumeLevelFeedback.FireUpdate();
                return;
            }
            else if (command == "M") // Mute response
            {
                int starPos = response.IndexOf('*');
                string value = response.Substring(starPos + 1,1);

                if (value == "1")
                {
                    _isMuted = true;
                }
                else if (value == "0")
                {
                    _isMuted = false;
                }

                MuteFeedback.FireUpdate();
                return;
            }
		}

        /// <summary>
        /// Polls the DSP for the min and max levels for this object
        /// </summary>
        public void GetCurrentMinMax()
        {
            if(_levelPrefix.Contains("D"))  //Only if this is a group control
            {
                string cmdToSemd = string.Format("{0}{1}GRPM\x0D", '\x1B', _levelPrefix.Replace("D", "L"));
                Debug.Console(1, this, "GetCurrentMinMax {0}", cmdToSemd);
                _parent.SendLine(cmdToSemd);
            }
        }

        /// <summary>
        /// Polls the DSP for the current gain for this object
        /// </summary>
        public void GetCurrentGain()
        {

			string cmdToSemd = string.Format("{0}{1}{2}\x0D", '\x1B', _levelPrefix, _commandSuffix);
			_parent.SendLine(cmdToSemd);
        }

        /// <summary>
        /// Polls the DSP for the current mute for this object
        /// </summary>
        public void GetCurrentMute()
        {
            string cmdToSemd = string.Format("{0}{1}{2}\x0D", '\x1B', _mutePrefix, _commandSuffix);
			_parent.SendLine(cmdToSemd);
        }

		/// <summary>
		/// Turns the mute off
		/// </summary>
		public void MuteOff()
		{
			SendFullCommand(_mutePrefix, "0", _commandSuffix);
		}

		/// <summary>
		/// Turns the mute on
		/// </summary>
		public void MuteOn()
		{
			SendFullCommand(_mutePrefix, "1", _commandSuffix);
		}

		/// <summary>
		/// Sets the volume to a specified level
		/// </summary>
		/// <param name="level"></param>
		public void SetVolume(ushort level)
		{
			Debug.Console(1, this, "Set Volume: {0}", level);
			if (_isMuted)
			{
				MuteOff();
			}
            double tempLevel;

            if (_useSisVolume)
            {
                tempLevel = ScaleSisFull(level);
            }
            else
            {
                tempLevel = ScaleFull(level);
            }

            Debug.Console(1, this, "Set Scaled Volume: {0}", tempLevel);
			SendFullCommand(_levelPrefix, tempLevel, _commandSuffix);
		}

		/// <summary>
		/// Toggles mute status
		/// </summary>
		public void MuteToggle()
		{
			if (MuteFeedback.BoolValue)
			{
				MuteOff();
			}
			else
			{
				MuteOn();
			}
		}

        /// <summary>
        /// Increments volume level
        /// </summary>
        /// <param name="callbackObject"></param>
        public void VolumeUpRepeat(object callbackObject)
        {
            this.VolumeUp(_volumeUpCount > 0);
        }

        /// <summary>
        /// Decrements volume level
        /// </summary>
        /// <param name="callbackObject"></param>
        public void VolumeDownRepeat(object callbackObject)
        {
            this.VolumeDown(_volumeDownCount > 0);
        }

        /// <summary>
        /// Decrements volume level
        /// </summary>
        /// <param name="press"></param>
        public void VolumeDown(bool press)
        {
            try
            {
                _volumeDownLock.WaitForMutex();
                if (_volumeDownCount > 100)
                {
                    _volumeDownCount = 0;
                    _volumeDownRepeatTimer.Stop();
                }
                else if (press)
                {
                    if (_isMuted)
                    {
                        MuteOff();
                    }
                    _volumeDownCount++;
                    SendFullCommand(_levelPrefix, "10-", _commandSuffix);
                    _volumeDownRepeatTimer.Reset(100);
                }
                else
                {
                    _volumeDownCount = 0;
                    _volumeDownRepeatTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("Extron Dmp Exception in VolumeDown: ", ex);
            }
            finally
            {
                _volumeDownLock.ReleaseMutex();
            }
        }

        /// <summary>
        /// Increments volume level
        /// </summary>
        /// <param name="press"></param>
        public void VolumeUp(bool press)
        {
            try
            {
                _volumeUpLock.WaitForMutex();
                if (_volumeUpCount > 100)
                {
                    _volumeUpCount = 0;
                    _volumeUpRepeatTimer.Stop();
                }
                else if (press)
                {
                    if (_isMuted)
                    {
                        MuteOff();
                    }
                    _volumeUpCount++;
                    SendFullCommand(_levelPrefix, "10+", _commandSuffix);
                    _volumeUpRepeatTimer.Reset(100);
                }
                else
                {
                    _volumeUpCount = 0;
                    _volumeUpRepeatTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("Extron Dmp Exception in VolumeUp: ", ex);
            }
            finally
            {
                _volumeUpLock.ReleaseMutex();
            }
        }

        /// <summary>
        /// Scales the input provided based on the absolute min/max values
        /// For Extron DMP this unit is dB*10 rounded to nearest integer
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        double ScaleFull(ushort input)
        {
			double scaled = 10 * ((input * (maxLevel - minLevel) / ushort.MaxValue) + minLevel);
            return Math.Round(scaled);
        }

        /// <summary>
        /// Scales the input provided based on the absolute min/max values to a SIS type value (see Extron protocol doc)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        double ScaleSisFull(ushort input)
        {
            //Add 2048 to desired dB (dB*10 rounded to nearest integer) to get SIS value
            return ScaleFull(input) + 2048;
        }
	}

	/// <summary>
	/// Level type enum
	/// </summary>
	public enum ePdtLevelTypes
	{
		Speaker = 0,
		Microphone = 1
	}

}