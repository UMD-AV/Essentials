using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Devices;
using Crestron.SimplSharpPro;

namespace ExtronMlsDsp
{
    public class ExtronMlsDsp : EssentialsBridgeableDevice, ICommunicationMonitor, IOnline
	{
        private readonly IBasicCommunication _comms;
        private CommunicationGather _gather;
        public readonly GenericCommunicationMonitor _commsMonitor;
        private bool _muteFb;
        private ushort _volumeFb;
        CTimer _volumeUpRepeatTimer;
        CTimer _volumeDownRepeatTimer;
        CMutex _volumeUpLock;
        CMutex _volumeDownLock;
        private ushort _volumeUpCount;
        private ushort _volumeDownCount;
        private ushort _defaultVolume;
        private bool _readyForLevel;
        CTimer _readyForLevelTimer;

        /// <summary>
        /// Online feedback
        /// </summary>
        public BoolFeedback OnlineFeedback { get; private set; }

        /// <summary>
        /// Monitor status feedback
        /// </summary>
        public IntFeedback MonitorStatusFeedback { get; private set; }

        /// <summary>
        /// Mute feedback
        /// </summary>
        public BoolFeedback MuteFeedback { get; private set; }

        /// <summary>
        /// Volume feedback
        /// </summary>
        public IntFeedback VolumeFeedback { get; private set; }

        public BoolFeedback IsOnline { get { return _commsMonitor.IsOnlineFeedback; } }
        public StatusMonitorBase CommunicationMonitor
        {
            get { return _commsMonitor; }
        }

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="key">String</param>
		/// <param name="name">String</param>
		/// <param name="comm">IBasicCommunication</param>
		public ExtronMlsDsp(string key, string name, ExtronMlsDspPropertiesConfig config, IBasicCommunication comm)
			: base(key, name)
		{
            _comms = comm;
            _muteFb = false;
            _readyForLevel = false;

            //Set volume up/down controls
            _volumeDownLock = new CMutex();
            _volumeUpLock = new CMutex();
            _volumeUpCount = 0;
            _volumeDownCount = 0;
            _defaultVolume = (config.defaultVolume != null && config.defaultVolume < 100 && config.defaultVolume >= 0) ? (ushort)config.defaultVolume : (ushort)50;

            _volumeUpRepeatTimer = new CTimer(VolumeUpRepeat, Timeout.Infinite);
            _volumeDownRepeatTimer = new CTimer(VolumeDownRepeat, Timeout.Infinite);
            _readyForLevelTimer = new CTimer(EnableLevelSend, Timeout.Infinite);

			// Comm monitoring, will poll every 30s.
			_commsMonitor = new GenericCommunicationMonitor(this, _comms, 30000, 120000, 300000, Poll);
            _commsMonitor.Start();

            //Link feedback
            OnlineFeedback = new BoolFeedback(() => _comms.IsConnected);
            MonitorStatusFeedback = new IntFeedback(() => (int)_commsMonitor.Status);
            MuteFeedback = new BoolFeedback(() => _muteFb);
            VolumeFeedback = new IntFeedback(() => _volumeFb);

            _gather = new CommunicationGather(_comms, "\x0d\x0a");
            _gather.LineReceived += this.Handle_BytesRecieved;
            _comms.Connect();

            _commsMonitor.IsOnlineFeedback.OutputChange += new EventHandler<FeedbackEventArgs>(IsOnlineFeedback_OutputChange);
		}

        /// <summary>
        /// Link to API
        /// </summary>
        /// <param name="trilist">BasicTriList</param>
        /// <param name="joinStart">uint</param>
        /// <param name="joinMapKey">string</param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
           var joinMap = new ExtronMlsDspDeviceJoinMap(joinStart);

			Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

			//From Plugin to Simpl
			OnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = this.Name;
            trilist.StringInput[joinMap.Presets.JoinNumber + 1].StringValue = "Default Volume";

            //From Simpl to Plugin
            trilist.SetSigTrueAction(joinMap.Presets.JoinNumber + 1, () => DefaultVolume());
            trilist.SetBoolSigAction(joinMap.EnableLevelSend.JoinNumber, b => SetLevelSend(b));
            
            //Link main volume to channel 1 feedback
			trilist.StringInput[joinMap.ChannelName.JoinNumber + 1].StringValue = "Main Volume";
            trilist.UShortInput[joinMap.ChannelType.JoinNumber + 1].UShortValue = 0;
            trilist.BooleanInput[joinMap.ChannelVisible.JoinNumber + 1].BoolValue = true;

            MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.ChannelMuteToggle.JoinNumber + 1]);
            VolumeFeedback.LinkInputSig(trilist.UShortInput[joinMap.ChannelVolume.JoinNumber + 1]);

            //Link channel 1 actions to main volume            
            trilist.SetSigTrueAction(joinMap.ChannelMuteToggle.JoinNumber + 1, () => MuteToggle());
            trilist.SetSigTrueAction(joinMap.ChannelMuteOn.JoinNumber + 1, () => MuteOn());
            trilist.SetSigTrueAction(joinMap.ChannelMuteOff.JoinNumber + 1, () => MuteOff());

            trilist.SetBoolSigAction(joinMap.ChannelVolumeUp.JoinNumber + 1, b => VolumeUp(b));
            trilist.SetBoolSigAction(joinMap.ChannelVolumeDown.JoinNumber + 1, b => VolumeDown(b));

            trilist.SetUShortSigAction(joinMap.ChannelVolume.JoinNumber + 1, u =>
            {
                if(trilist.BooleanOutput[joinMap.EnableLevelSend.JoinNumber].BoolValue == true)
                { 
                    SetVolume(u);
                }
            });
        }

        void SetLevelSend(bool b)
        {
            if (b == true)
            {
                _readyForLevelTimer.Reset(5000);
            }
            else
            {
                _readyForLevelTimer.Stop();
                _readyForLevel = false;
            }
        }

        void EnableLevelSend(object callbackObject)
        {
            _readyForLevel = true;
        }

		/// <summary>
		/// Polls the device, should be called by comm monitor only.
		/// </summary>
		void Poll()
		{
            //Query the main volume level and mute
			SendText("V");
            SendText("Z");
		}

		/// <summary>
		/// Handles a response message from the DSP
		/// </summary>
		/// <param name="dev"></param>
		/// <param name="args"></param>
		void Handle_BytesRecieved(object dev, GenericCommMethodReceiveTextArgs args)
		{
			Debug.Console(1, this, "Extron Mls RX: '{0}'", args.Text);
			try
			{
				if (args.Text.Contains("Vol"))
				{
                    //Get number after Vol, for example 013 if return is Vol013
                    int start = args.Text.IndexOf("Vol") + 3;
                    ushort volRaw = ushort.Parse(args.Text.Substring(start, 3));
                    if (volRaw >= 0 && volRaw <= 100)
                    {
                        _volumeFb = (ushort)(volRaw * ushort.MaxValue / 100);
                        VolumeFeedback.FireUpdate();
                    }
				}

                else if (args.Text.Contains("Amt"))
                {
                    //Found mute feedback text
                    if(args.Text.Contains("Amt0"))
                    {
                        _muteFb = false;
                        MuteFeedback.FireUpdate();
                    }
                    else if (args.Text.Contains("Amt1"))
                    {
                        _muteFb = true;
                        MuteFeedback.FireUpdate();
                    }
                }
			}
			catch (Exception e)
			{
				if (Debug.Level == 2)
					Debug.Console(2, this, "Error parsing response: '{0}'\n{1}", args.Text, e);
			}
		}

        void IsOnlineFeedback_OutputChange(object dev, FeedbackEventArgs args)
        {
            OnlineFeedback.FireUpdate();
            if (args.BoolValue == true)
            {
                //Device is now online
                //Lock front panel adjustments
                SendText("1X");
                //Set Audio Input 1
                SendText("1$");
                //Poll Volume
                SendText("V");
                //Poll Mute
                SendText("Z");
            }
        }

		/// <summary>
		/// Sends a command to the DSP
		/// </summary>
		/// <param name="s">Command to send</param>
		public void SendText(string s)
		{
            Debug.Console(1, this, "Extron Mls TX: '{0}'", s);
			_comms.SendText(s);
		}

        public void DefaultVolume()
        {
            SendText(string.Format("{0}V", _defaultVolume));
        }

        public void SetVolume(ushort vol)
        {
            if (_readyForLevel)
            {
                if (_muteFb)
                {
                    MuteOff();
                }
                int scaledVol = vol * 100 / ushort.MaxValue;
                SendText(string.Format("{0}V", scaledVol));
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
                    if (_muteFb)
                    {
                        MuteOff();
                    }
                    _volumeDownCount++;
                    SendText("-V");
                    _volumeDownRepeatTimer.Reset(50);
                }
                else
                {
                    _volumeDownCount = 0;
                    _volumeDownRepeatTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("Extron Mls Dsp Exception in VolumeDown: ", ex);
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
                    if (_muteFb)
                    {
                        MuteOff();
                    }
                    _volumeUpCount++;
                    SendText("+V");
                    _volumeUpRepeatTimer.Reset(50);
                }
                else
                {
                    _volumeUpCount = 0;
                    _volumeUpRepeatTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("Extron Mls Dsp Exception in VolumeUp: ", ex);
            }
            finally
            {
                _volumeUpLock.ReleaseMutex();
            }
        }

        public void MuteToggle()
        {
            if (_muteFb == true)
            {
                MuteOff();
            }
            else
            {
                MuteOn();
            }
        }

        public void MuteOn()
        {
            SendText("1Z");
        }

        public void MuteOff()
        {
            SendText("0Z");
        }
	}
}