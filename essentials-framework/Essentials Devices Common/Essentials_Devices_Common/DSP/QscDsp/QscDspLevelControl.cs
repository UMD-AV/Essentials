using System;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace QscQsysDspPlugin
{
    public class QscDspLevelControl : QscDspControlPoint, IBasicVolumeWithFeedback, IKeyed
    {
        private bool _isMuted;
        private ushort _volumeLevel;

        public BoolFeedback MuteFeedback { get; private set; }

        public IntFeedback VolumeLevelFeedback { get; private set; }

        public bool Enabled { get; set; }
        public bool UseAbsoluteValue { get; set; }
        public ePdtLevelTypes Type;
        public int Permissions { get; set; }
        private CTimer _volumeUpRepeatTimer;
        private CTimer _volumeDownRepeatTimer;
        private CMutex _volumeUpLock;
        private CMutex _volumeDownLock;
        private ushort _volumeUpCount;
        private ushort _volumeDownCount;
        private readonly QscDsp _parent;

        /// <summary>
        /// Used for to identify level subscription values
        /// </summary>
        public string LevelCustomName { get; private set; }

        /// <summary>
        /// Used for to identify mute subscription values
        /// </summary>
        public string MuteCustomName { get; private set; }

        /// <summary>
        /// Minimum fader level
        /// </summary>
        //double MinLevel;

        /// <summary>
        /// Maximum fader level
        /// </summary>
        //double MaxLevel;

        /// <summary>
        /// Checks if a valid subscription string has been recieved for all subscriptions
        /// </summary>
        public bool IsSubsribed
        {
            get
            {
                //bool isSubscribed = false;

                //if (HasMute && MuteIsSubscribed)
                //    isSubscribed = true;

                //if (HasLevel && LevelIsSubscribed)
                //    isSubscribed = true;

                //return isSubscribed;

                bool isSubscribed = HasMute && _muteIsSubscribed || HasLevel && _levelIsSubscribed;
                return isSubscribed;
            }
        }

        public bool AutomaticUnmuteOnVolume { get; private set; }

        public bool HasMute { get; private set; }
        public bool HasLevel { get; private set; }

        private bool _muteIsSubscribed;
        private bool _levelIsSubscribed;

        //public TesiraForteLevelControl(string label, string id, int index1, int index2, bool hasMute, bool hasLevel, BiampTesiraForteDsp parent)
        //    : base(id, index1, index2, parent)
        //{
        //    Initialize(label, hasMute, hasLevel);
        //}

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="key">instance key</param>
        /// <param name="config">level control block configuration object</param>
        /// <param name="parent">dsp parent isntance</param>
        public QscDspLevelControl(string key, QscDspLevelControlBlockConfig config, QscDsp parent)
            : base(config.LevelInstanceTag, config.MuteInstanceTag, parent)
        {
            _parent = parent;
            if (config.Disabled)
                return;

            parent.CommunicationMonitor.IsOnlineFeedback.OutputChange += (sender, args) =>
            {
                if (!args.BoolValue)
                    return;

                CrestronInvoke.BeginInvoke(o =>
                {
                    if (!string.IsNullOrEmpty(config.LevelInstanceTag) && config.HasLevel)
                        _parent.SendLine(string.Format("cg \"{0}\"", config.LevelInstanceTag));

                    if (!string.IsNullOrEmpty(config.MuteInstanceTag) && config.HasMute)
                        _parent.SendLine(string.Format("cg \"{0}\"", config.MuteInstanceTag));
                });
            };

            Initialize(key, config);
        }

        /// <summary>
        /// Initializes this attribute based on config values and generates subscriptions commands and adds commands to the parent's queue.
        /// </summary>
        /// <param name="key">instance key</param>
        /// <param name="config">level control block configuration object</param>
        public void Initialize(string key, QscDspLevelControlBlockConfig config)
        {
            Key = string.Format("{0}-{1}", Parent.Key, key);
            Enabled = true;
            DeviceManager.AddDevice(this);
            Type = config.IsMic ? ePdtLevelTypes.Microphone : ePdtLevelTypes.Speaker;
            Permissions = config.Permissions;

            Debug.Console(2, this, "Adding LevelControl '{0}'", Key);

            _volumeDownLock = new CMutex();
            _volumeUpLock = new CMutex();
            _volumeUpCount = 0;
            _volumeDownCount = 0;
            this.IsSubscribed = false;

            MuteFeedback = new BoolFeedback(() => _isMuted);

            VolumeLevelFeedback = new IntFeedback(() => _volumeLevel);

            _volumeUpRepeatTimer = new CTimer(VolumeUpRepeat, Timeout.Infinite);
            _volumeDownRepeatTimer = new CTimer(VolumeDownRepeat, Timeout.Infinite);
            LevelCustomName = config.Label;
            HasMute = config.HasMute;
            HasLevel = config.HasLevel;
            UseAbsoluteValue = config.UseAbsoluteValue;
            AutomaticUnmuteOnVolume = config.UnmuteOnVolChange;
        }

        /// <summary>
        /// Subscribes this level control object as configured
        /// </summary>
        public void Subscribe()
        {
            // Do subscriptions and blah blah
            // Subscribe to mute
            if (this.HasMute)
            {
                SendSubscriptionCommand(this.MuteInstanceTag);
            }

            // Subscribe to level
            if (this.HasLevel)
            {
                SendSubscriptionCommand(this.LevelInstanceTag);
            }
        }


        /// <summary>
        /// Parses the response from the DspBase
        /// </summary>
        /// <param name="customName"></param>
        /// <param name="value"></param>
        /// <param name="absoluteValue"></param>
        public void ParseSubscriptionMessage(string customName, string value, string absoluteValue)
        {
            // Check for valid subscription response
            Debug.Console(1, this, "Level {0} Response: '{1}'", customName, value);
            if (
                !string.IsNullOrEmpty(MuteInstanceTag)
                && customName.Equals(MuteInstanceTag, StringComparison.OrdinalIgnoreCase))
            {
                switch (value)
                {
                    case "true":
                    case "muted":
                        _isMuted = true;
                        _muteIsSubscribed = true;
                        break;
                    case "false":
                    case "unmuted":
                        _isMuted = false;
                        _muteIsSubscribed = true;
                        break;
                }

                MuteFeedback.FireUpdate();
            }
            else if (
                !string.IsNullOrEmpty(LevelInstanceTag)
                && customName.Equals(LevelInstanceTag, StringComparison.OrdinalIgnoreCase)
                && !UseAbsoluteValue)
            {
                double parsedValue = double.Parse(value);

                _volumeLevel = (ushort)(parsedValue * 65535);
                Debug.Console(1, this, "Level {0} VolumeLevel: '{1}'", customName, _volumeLevel);
                _levelIsSubscribed = true;

                VolumeLevelFeedback.FireUpdate();
            }
            else if (
                !string.IsNullOrEmpty(LevelInstanceTag)
                && customName.Equals(LevelInstanceTag, StringComparison.OrdinalIgnoreCase)
                && UseAbsoluteValue)
            {
                _volumeLevel = ushort.Parse(absoluteValue);
                Debug.Console(1, this, "Level {0} VolumeLevel: '{1}'", customName, _volumeLevel);
                _levelIsSubscribed = true;

                VolumeLevelFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Turns the mute off
        /// </summary>
        public void MuteOff()
        {
            SendFullCommand("csv", this.MuteInstanceTag, "0");
        }

        /// <summary>
        /// Turns the mute on
        /// </summary>
        public void MuteOn()
        {
            SendFullCommand("csv", this.MuteInstanceTag, "1");
        }

        /// <summary>
        /// Sets the volume to a specified level
        /// </summary>
        /// <param name="level"></param>
        public void SetVolume(ushort level)
        {
            Debug.Console(1, this, "volume: {0}", level);
            // Unmute volume if new level is higher than existing
            if (AutomaticUnmuteOnVolume && _isMuted)
            {
                MuteOff();
            }

            if (!UseAbsoluteValue)
            {
                double newLevel = Scale(level);
                Debug.Console(1, this, "newVolume: {0}", newLevel);
                SendFullCommand("csp", this.LevelInstanceTag, string.Format("{0}", newLevel));
            }
            else
            {
                SendFullCommand("csv", this.LevelInstanceTag, string.Format("{0}", level));
            }
        }

        /// <summary>
        /// Toggles mute status
        /// </summary>
        public void MuteToggle()
        {
            SendFullCommand("csv", this.MuteInstanceTag, _isMuted ? "0" : "1");
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
                    if (AutomaticUnmuteOnVolume && _isMuted)
                    {
                        MuteOff();
                    }

                    _volumeDownCount++;
                    SendFullCommand("css ", this.LevelInstanceTag, "--");
                    _volumeDownRepeatTimer.Reset(80);
                }
                else
                {
                    _volumeDownCount = 0;
                    _volumeDownRepeatTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("Qsc Exception in VolumeDown: ", ex);
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
                    if (AutomaticUnmuteOnVolume && _isMuted)
                    {
                        MuteOff();
                    }

                    _volumeUpCount++;
                    SendFullCommand("css ", this.LevelInstanceTag, "++");
                    _volumeUpRepeatTimer.Reset(80);
                }
                else
                {
                    _volumeUpCount = 0;
                    _volumeUpRepeatTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("Qsc Exception in VolumeUp: ", ex);
            }
            finally
            {
                _volumeUpLock.ReleaseMutex();
            }
        }

        /// <summary>
        /// Scales the input provided
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private double Scale(double input)
        {
            Debug.Console(1, this, "Scaling (double) input '{0}'", input);

            double output = (input / 65535);

            Debug.Console(1, this, "Scaled output '{0}'", output);

            return output;
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