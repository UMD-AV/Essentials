using System;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using UmdEssentials.Core;
using UmdEssentials.Core.Bridges;

namespace UmdEssentials.Devices.Common.Microphones
{
    public enum LinkStates : ushort
    {
        Disconnected = 0,
        Connected = 1,
        Pairing = 2,
        Charging = 3,
        Unknown = 4
    }

    public class WirelessMic : EssentialsBridgeableDevice, IHasMuteControlWithFeedback
    {
        public WirelessMic(string key, string name, bool isBattery) : base(key, name)
        {
            _name = name;
            _isOnline = false;
            _isBattery = isBattery;
            _runtime = 65535;

            IsOnlineFeedback = new BoolFeedback(() => IsOnline);
            MuteFeedback = new BoolFeedback(() => MuteState);
            IsBatteryFeedback = new BoolFeedback(() => IsBattery);
            OnDockFeedback = new BoolFeedback(() => OnDock);
            MicrophoneInUseFeedback = new BoolFeedback(() => MicrophoneInUse);
            PercentChargeFeedback = new IntFeedback(() => PercentCharge);
            PercentHealthFeedback = new IntFeedback(() => PercentHealth);
            TemperatureFFeedback = new IntFeedback(() => TemperatureF);
            BatteryErrorAnalogFeedback = new IntFeedback(() => BatteryErrorAnalog);
            RuntimeFeedback = new IntFeedback(() => Runtime);
            LinkStateFeedback = new IntFeedback(() => (ushort)LinkState);
            ModelFeedback = new StringFeedback(() => Model);
            NameFeedback = new StringFeedback(() => Name);
            ErrorStringFeedback = new StringFeedback(() => ErrorString);
            StateFeedback = new StringFeedback(() => State);
            DeviceFirmwareVersionFeedback = new StringFeedback(() => DeviceFirmwareVersion);
        }

        private bool _isOnline;

        public bool IsOnline
        {
            get { return _isOnline; }
            set
            {
                _isOnline = value;
                IsOnlineFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     Device online feedback
        /// </summary>
        public BoolFeedback IsOnlineFeedback { get; private set; }

        private bool _muteState;

        public bool MuteState
        {
            get { return _muteState; }
            set
            {
                _muteState = value;
                MuteFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     Device audio mute state feedback
        /// </summary>
        public BoolFeedback MuteFeedback { get; private set; }

        private bool _isBattery;

        public bool IsBattery
        {
            get { return _isBattery; }
            set
            {
                _isBattery = value;
                IsBatteryFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     Device is a wireless microphone feedback
        /// </summary>
        public BoolFeedback IsBatteryFeedback { get; private set; }

        private bool _onDock;

        public bool OnDock
        {
            get { return _onDock; }
            set
            {
                _onDock = value;
                OnDockFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     Microphone on dock feedback
        /// </summary>
        public BoolFeedback OnDockFeedback { get; private set; }

        private bool _microphoneInUse;

        public bool MicrophoneInUse
        {
            get { return _microphoneInUse; }
            set
            {
                _microphoneInUse = value;
                MicrophoneInUseFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     Microphone present feedback
        /// </summary>
        public BoolFeedback MicrophoneInUseFeedback { get; private set; }

        private LinkStates _linkState;

        public LinkStates LinkState
        {
            get { return _linkState; }
            set
            {
                _linkState = value;
                State = GetLinkStateName(value);
                OnDock = value == LinkStates.Charging;
                MicrophoneInUse = value == LinkStates.Connected;
                LinkStateFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     Microphone link state feedback
        /// </summary>
        public IntFeedback LinkStateFeedback { get; private set; }

        private int _percentCharge;

        public int PercentCharge
        {
            get { return _percentCharge; }
            set
            {
                _percentCharge = value;
                PercentChargeFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     Battery percent charge feedback
        /// </summary>
        public IntFeedback PercentChargeFeedback { get; private set; }


        private int _percentHealth;

        public int PercentHealth
        {
            get { return _percentHealth; }
            set
            {
                _percentHealth = value;
                PercentHealthFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     Battery percent health feedback
        /// </summary>
        public IntFeedback PercentHealthFeedback { get; private set; }


        private int _temperatureF;

        public int TemperatureF
        {
            get { return _temperatureF; }
            set
            {
                _temperatureF = value;
                TemperatureFFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     Battery temperature in F feedback
        /// </summary>
        public IntFeedback TemperatureFFeedback { get; private set; }

        private int _batteryErrorAnalog;

        protected int BatteryErrorAnalog
        {
            get { return _batteryErrorAnalog; }
            set
            {
                _batteryErrorAnalog = value;
                BatteryErrorAnalogFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     Battery error analog feedback
        /// </summary>
        public IntFeedback BatteryErrorAnalogFeedback { get; private set; }

        private int _runtime;

        public int Runtime
        {
            get { return _runtime; }
            set
            {
                _runtime = value;
                RuntimeFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     Run time feedback
        /// </summary>
        public IntFeedback RuntimeFeedback { get; private set; }

        private string _model;

        public string Model
        {
            get { return _model; }
            set
            {
                _model = value;
                ModelFeedback.FireUpdate();
            }
        }

        public StringFeedback ModelFeedback { get; private set; }

        private string _errorString;

        public string ErrorString
        {
            get { return _errorString; }
            set
            {
                _errorString = value;
                ErrorStringFeedback.FireUpdate();
            }
        }

        public StringFeedback ErrorStringFeedback { get; private set; }

        private string _state;

        public string State
        {
            get { return _state; }
            set
            {
                _state = value;
                StateFeedback.FireUpdate();
            }
        }

        public StringFeedback StateFeedback { get; private set; }

        private string _deviceFirmwareVersion;

        public string DeviceFirmwareVersion
        {
            get { return _deviceFirmwareVersion; }
            set
            {
                _deviceFirmwareVersion = value;
                DeviceFirmwareVersionFeedback.FireUpdate();
            }
        }

        public StringFeedback DeviceFirmwareVersionFeedback { get; private set; }

        private string _name;

        public new string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                NameFeedback.FireUpdate();
            }
        }

        public StringFeedback NameFeedback { get; private set; }

        public void FireUpdate()
        {
            IsOnlineFeedback.FireUpdate();
            IsBatteryFeedback.FireUpdate();
            OnDockFeedback.FireUpdate();
            MuteFeedback.FireUpdate();
            MicrophoneInUseFeedback.FireUpdate();
            PercentChargeFeedback.FireUpdate();
            PercentHealthFeedback.FireUpdate();
            TemperatureFFeedback.FireUpdate();
            BatteryErrorAnalogFeedback.FireUpdate();
            RuntimeFeedback.FireUpdate();
            LinkStateFeedback.FireUpdate();
            NameFeedback.FireUpdate();
            ModelFeedback.FireUpdate();
            ErrorStringFeedback.FireUpdate();
            StateFeedback.FireUpdate();
            DeviceFirmwareVersionFeedback.FireUpdate();
        }

        private void UpdateFeedbacks()
        {
            FireUpdate();
        }


        private static string GetLinkStateName(LinkStates linkState)
        {
            switch (linkState)
            {
                case LinkStates.Disconnected:
                    return "Disconnected";
                case LinkStates.Connected:
                    return "Connected";
                case LinkStates.Pairing:
                    return "Pairing";
                case LinkStates.Charging:
                    return "Charging";
                case LinkStates.Unknown:
                default:
                    return "Unknown";
            }
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            try
            {
                MicrophoneDeviceJoinMap joinMap = new MicrophoneDeviceJoinMap(joinStart);

                // This adds the join map to the collection on the bridge
                if (bridge != null) bridge.AddJoinMap(Key, joinMap);

                Dictionary<string, JoinData> customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);
                if (customJoins != null) joinMap.SetCustomJoinData(customJoins);

                Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
                Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);

                // links to bridge
                IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

                //trilist.SetSigTrueAction(joinMap.DeviceAudioMuteOn.JoinNumber, SetDeviceAudioMuteOn);
                //trilist.SetSigTrueAction(joinMap.DeviceAudioMuteOff.JoinNumber, SetDeviceAudioMuteOff);

                trilist.BooleanInput[joinMap.IsWireless.JoinNumber].BoolValue = true;
                MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DeviceAudioMuteOn.JoinNumber]);
                MuteFeedback.LinkComplementInputSig(
                    trilist.BooleanInput[joinMap.DeviceAudioMuteOff.JoinNumber]);

                IsBatteryFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsBattery.JoinNumber]);
                OnDockFeedback.LinkInputSig(trilist.BooleanInput[joinMap.OnDock.JoinNumber]);
                MicrophoneInUseFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsPresent.JoinNumber]);

                PercentChargeFeedback.LinkInputSig(trilist.UShortInput[joinMap.PercentCharge.JoinNumber]);
                PercentHealthFeedback.LinkInputSig(trilist.UShortInput[joinMap.PercentHealth.JoinNumber]);
                TemperatureFFeedback.LinkInputSig(trilist.UShortInput[joinMap.TempF.JoinNumber]);
                BatteryErrorAnalogFeedback.LinkInputSig(trilist.UShortInput[joinMap.BatteryErrorAnalog.JoinNumber]);
                RuntimeFeedback.LinkInputSig(trilist.UShortInput[joinMap.BatteryRunTime.JoinNumber]);
                LinkStateFeedback.LinkInputSig(trilist.UShortInput[joinMap.LinkState.JoinNumber]);

                NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Name.JoinNumber]);
                ErrorStringFeedback.LinkInputSig(trilist.StringInput[joinMap.ErrorString.JoinNumber]);
                StateFeedback.LinkInputSig(trilist.StringInput[joinMap.State.JoinNumber]);
                ModelFeedback.LinkInputSig(trilist.StringInput[joinMap.Model.JoinNumber]);
                DeviceFirmwareVersionFeedback.LinkInputSig(
                    trilist.StringInput[joinMap.DeviceFirmwareVersion.JoinNumber]);

                UpdateFeedbacks();

                trilist.OnlineStatusChange += (o, a) =>
                {
                    if (!a.DeviceOnLine) return;
                    UpdateFeedbacks();
                };
            }
            catch (Exception ex)
            {
                Debug.ConsoleWithLog(0, "Exception Linking to Bridge Type {0}: {1}", GetType().Name, ex.Message);
            }
        }

        public void MuteToggle()
        {
            //
        }

        public void MuteOn()
        {
            //
        }

        public void MuteOff()
        {
            //
        }
    }
}