using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Queues;

namespace PepperDash.Essentials.Devices.Common.ShureMxa
{
    public class ShureMxaDevice : EssentialsBridgeableDevice
    {
        private readonly ShureMxaConfig _config;

        private readonly IBasicCommunication _comms;
        private readonly GenericCommunicationMonitor _commsMonitor;
        private const string CommsDelimiter = ">";

        private IBasicVolumeWithFeedback DspObject;
        private CMutex DspObjectMutex;
        private bool dspObjectLock;
        private CMutex DeviceObjectMutex;
        private bool deviceObjectLock;
        private bool deviceMuteChangeInProgress;
        CTimer deviceMuteChangeTimer;

        private readonly GenericQueue _commsQueue;

        /// <summary>
        /// Reports socket status feedback through the bridge
        /// </summary>
        public IntFeedback SocketStatusFeedback { get; private set; }

        /// <summary>
        /// Reports monitor status feedback through the bridge
        /// Typically used for Fusion status reporting and system status LED's
        /// </summary>
        public IntFeedback MonitorStatusFeedback { get; private set; }


        #region Device LED state (DEV_LED_IN_STATE)

        // device LED state on/off field
        private bool _deviceLedState;
        /// <summary>
        /// Device LED state on/off property
        /// </summary>
        public bool DeviceLedState
        {
            get { return _deviceLedState; }
            set
            {
                _deviceLedState = value;
                DeviceLedStateFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Device LED state on/off feedback
        /// </summary>
        public BoolFeedback DeviceLedStateFeedback { get; private set; }

        /// <summary>
        /// Sets the device LED state
        /// </summary>
        /// <param name="state">boolean value</param>
        /// <returns>null</returns>
        public void SetDeviceLedState(bool state)
        {
            SendText(string.Format("SET DEV_LED_IN_STATE {0}", state ? "ON" : "OFF"));
        }

        /// <summary>
        /// Sets the device LED state on
        /// </summary>
        /// <remarks>
        /// Need this to avoid having to use the action delegate that was not working correctly
        /// </remarks>
        public void SetDeviceLedStateOn()
        {
            SetDeviceLedState(true);
        }

        /// <summary>
        /// Sets the device LED state off
        /// </summary>
        /// <remarks>
        /// Need this to avoid having to use the action delegate that was not working correctly
        /// </remarks>
        public void SetDeviceLedStateOff()
        {
            SetDeviceLedState(false);
        }

        #endregion


        #region Device Audio Mute (DEV_AUDIO_MUTE)

        // device audio mute state field
        private bool _deviceAudioMuteState;
        /// <summary>
        /// Device audio mute state property
        /// </summary>
        public bool DeviceAudioMuteState
        {
            get { return _deviceAudioMuteState; }
            set
            {
                _deviceAudioMuteState = value;
                DeviceAudioMuteStateFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Device audio mute state feedback
        /// </summary>
        public BoolFeedback DeviceAudioMuteStateFeedback { get; private set; }

        /// <summary>
        /// Toggles the device audio mute 
        /// </summary>
        public void ToggleDeviceAudioMute()
        {
            DeviceMuteChangeTimerStart();
            SendText("SET DEVICE_AUDIO_MUTE TOGGLE");
        }

        /// <summary>
        /// Sets the device audio mute state
        /// </summary>
        public void SetDeviceAudioMute(bool state)
        {
            DeviceMuteChangeTimerStart();
            SendText(string.Format("SET DEVICE_AUDIO_MUTE {0}", state ? "ON" : "OFF"));
        }

        /// <summary>
        /// Sets the device audio mute on
        /// </summary>
        /// <remarks>
        /// Need this to avoid having to use the action delegate
        /// </remarks>
        public void SetDeviceAudioMuteOn()
        {
            SetDeviceAudioMute(true);
        }

        /// <summary>
        /// Sets the device audio mute off
        /// </summary>
        /// <remarks>
        /// Need this to avoid having to use the action delegate
        /// </remarks>
        public void SetDeviceAudioMuteOff()
        {
            SetDeviceAudioMute(false);
        }

        #endregion


        #region Device Mute LED Status (DEVICE_MUTE_STATUS_LED_STATE)

        // device mute led state field
        private bool _deviceMuteStatusLedState;
        /// <summary>
        /// Devicee mute led state 
        /// </summary>
        public bool DeviceMuteStatusLedState
        {
            get { return _deviceMuteStatusLedState; }
            set
            {
                _deviceMuteStatusLedState = value;
                DeviceMuteStatusLedStateFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Device mute led state feedback
        /// </summary>
        public BoolFeedback DeviceMuteStatusLedStateFeedback { get; private set; }

        #endregion


        #region External Switch State (EXT_SWITCH_OUT_STATE)

        // external swtich state
        private bool _externalSwitchState;
        /// <summary>
        /// Gets the external switch state
        /// </summary>
        public bool ExternalSwitchState
        {
            get { return _externalSwitchState; }
            set
            {
                _externalSwitchState = value;
                ExternalSwitchStateFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// External switch state feedback
        /// </summary>
        public BoolFeedback ExternalSwitchStateFeedback { get; private set; }

        #endregion


        #region Presets

        // Current preset
        private uint _currentPreset;
        /// <summary>
        /// CurrentPreset property
        /// </summary>
        public uint CurrentPreset
        {
            get { return _currentPreset; }
            set
            {
                _currentPreset = value;
                CurrentPresetIntFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Current Preset int feedback
        /// </summary>
        public IntFeedback CurrentPresetIntFeedback { get; private set; }

        /// <summary>
        /// Gets the current preset
        /// </summary>
        public void GetCurrentPreset()
        {
            SendText("GET PRESET");
        }

        /// <summary>
        /// Recalls preset
        /// </summary>
        /// <param name="value">uint value, 1-10</param>
        // < SET PRESET nn > 
        // Where nn is the preset number 1-10. (Leading zero is optional when using the SET command).
        public void RecallPreset(uint value)
        {
            if (value <= 0 || value > 10) return;

            //SendText(string.Format("SET PRESET {0:00}", value));
            SendText(string.Format("SET PRESET {0}", value));
        }

        /// <summary>
        /// Gets the programmed named of the preset
        /// </summary>
        /// <param name="value">value of 1-10</param>
        // < GET PRESET# >
        // # = value of 1-10
        public void GetPresetName(uint value)
        {
            if (value <= 0 || value > 10) return;
            SendText(string.Format("GET PRESET{0}", value));
        }

        #endregion


        #region LED Color (LED_COLOR_MUTED | LED_COLOR_UNMUTED)

        /// <summary>
        /// LED Color Enum
        /// </summary>
        public enum ELedColor
        {
            /// <summary>
            /// Led color red
            /// </summary>
            RED = 0,
            /// <summary>
            /// Led color green
            /// </summary>
            GREEN = 1,
            /// <summary>
            /// Led color blue
            /// </summary>
            BLUE = 2,
            /// <summary>
            /// Led color pink
            /// </summary>
            PINK = 3,
            /// <summary>
            /// Led color purple
            /// </summary>
            PURPLE = 4,
            /// <summary>
            /// Led color yellow
            /// </summary>
            YELLOW = 5,
            /// <summary>
            /// Led color orange
            /// </summary>
            ORANGE = 6,
            /// <summary>
            /// Led color white
            /// </summary>
            WHITE = 7,
            /// <summary>
            /// Led color gold
            /// </summary>
            GOLD = 8,
            /// <summary>
            /// Led color yellow-green
            /// </summary>
            YELLOWGREEN = 9,
            /// <summary>
            /// Led color turquoise 
            /// </summary>
            TURQUOISE = 10,
            /// <summary>
            /// Led color powder-blue
            /// </summary>
            POWDERBLUE = 11,
            /// <summary>
            /// Led color cyan
            /// </summary>
            CYAN = 12,
            /// <summary>
            /// Led color sky-blue
            /// </summary>
            SKYBLUE = 13,
            /// <summary>
            /// Led color light-purple
            /// </summary>
            LIGHTPURPLE = 14,
            /// <summary>
            /// Led color violet
            /// </summary>
            VIOLET = 15,
            /// <summary>
            /// Led color orchid
            /// </summary>
            ORCHID = 16
        }

        // led muted color number
        private uint _ledMutedColorNumber;
        /// <summary>
        /// Led muted color
        /// </summary>
        public uint LedMutedColorNumber
        {
            get { return _ledMutedColorNumber; }
            set
            {
                _ledMutedColorNumber = value;
                LedMutedColorNumberFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Led muted color number feedback
        /// </summary>
        public IntFeedback LedMutedColorNumberFeedback { get; private set; }

        // led muted color name
        private string _ledMutedColorName;

        /// <summary>
        /// Led muted color name
        /// </summary>
        public string LedMutedColorName
        {
            get { return _ledMutedColorName; }
            set
            {
                _ledMutedColorName = value;
                LedMutedColorNameFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Led muted color name feedback
        /// </summary>
        public StringFeedback LedMutedColorNameFeedback { get; private set; }


        // led unmuted color number
        private uint _ledUnmutedColorNumber;

        /// <summary>
        /// Led unmuted color number
        /// </summary>
        public uint LedUnmutedColorNumber
        {
            get { return _ledUnmutedColorNumber; }
            set
            {
                _ledUnmutedColorNumber = value;
                LedUnmutedColorNumberFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Led unmuted color feedback
        /// </summary>
        public IntFeedback LedUnmutedColorNumberFeedback { get; private set; }

        // led unmuted color name
        private string _ledUnmutedColorName;

        /// <summary>
        /// LED unmuted color name
        /// </summary>
        public string LedUnmutedColorName
        {
            get { return _ledUnmutedColorName; }
            set
            {
                _ledUnmutedColorName = value;
                LedUnmutedColorNameFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Led unmuted color name feedback
        /// </summary>
        public StringFeedback LedUnmutedColorNameFeedback { get; private set; }

        /// <summary>
        /// Sets the device led color when muted
        /// </summary>
        /// <param name="value"></param>
        public void SetDeviceLedColorMuted(uint value)
        {
            var color = (ELedColor)Enum.Parse(typeof(ELedColor), value.ToString(), true);
            var defined = Enum.IsDefined(typeof(ELedColor), color);
            Debug.Console(1, this, "SetDeviceLedColorMuted: color-{0}, defined-{1}", color.ToString(), defined.ToString());
            if (defined) SendText(string.Format("SET LED_COLOR_MUTED {0}", color.ToString().ToUpper()));
        }

        /// <summary>
        /// Sets the device led color when unmuted
        /// </summary>
        /// <param name="value"></param>
        public void SetDeviceLedColorUnmuted(uint value)
        {
            var color = (ELedColor)Enum.Parse(typeof(ELedColor), value.ToString(), true);
            var defined = Enum.IsDefined(typeof(ELedColor), color);
            Debug.Console(1, this, "SetDeviceLedColorUnmuted: color-{0}, defined-{1}", color.ToString(), defined.ToString());
            if (defined) SendText(string.Format("SET LED_COLOR_UNMUTED {0}", color.ToString().ToUpper()));
        }

        #endregion


        #region Device Info

        // device model field
        private string _deviceModel;
        /// <summary>
        /// Device model property
        /// </summary>
        public string DeviceModel
        {
            get { return _deviceModel; }
            set
            {
                _deviceModel = value;
                DeviceModelFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Device model feedback
        /// </summary>
        public StringFeedback DeviceModelFeedback { get; private set; }


        // device serial number field
        private string _deviceSerialNumber;
        /// <summary>
        /// Device serial number property
        /// </summary>
        public string DeviceSerialNumber
        {
            get { return _deviceSerialNumber; }
            set
            {
                _deviceSerialNumber = value;
                DeviceSerialNumberFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Device serial number feedback
        /// </summary>
        public StringFeedback DeviceSerialNumberFeedback { get; private set; }


        // device firmware version field
        private string _deviceFirmwareVersion;
        /// <summary>
        /// Device firmware property
        /// </summary>
        public string DeviceFirmwareVersion
        {
            get { return _deviceFirmwareVersion; }
            set
            {
                _deviceFirmwareVersion = value;
                DeviceFirmwareVersionFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Device firmware version feedback
        /// </summary>
        public StringFeedback DeviceFirmwareVersionFeedback { get; private set; }

        // devicee error field
        private string _deviceError;
        /// <summary>
        /// Device error property
        /// </summary>
        public string DeviceError
        {
            get { return _deviceError; }
            set
            {
                _deviceError = value;
                DeviceErrorFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Deivce error feedback
        /// </summary>
        public StringFeedback DeviceErrorFeedback { get; private set; }


        #endregion


        /// <summary>
        /// Plugin device constructor
        /// </summary>
        /// <param name="key">device key</param>
        /// <param name="name">device name</param>
        /// <param name="config">device configuration object</param>
        /// <param name="comms">device communication as IBasicCommunication</param>
        /// <see cref="PepperDash.Core.IBasicCommunication"/>
        /// <seealso cref="Crestron.SimplSharp.CrestronSockets.SocketStatus"/>
        public ShureMxaDevice(string key, string name, ShureMxaConfig config, IBasicCommunication comms)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new {0} instance", name);

            _config = config;

            MonitorStatusFeedback = new IntFeedback(() => (int)_commsMonitor.Status);

            // digital feedbacks
            DeviceLedStateFeedback = new BoolFeedback(() => DeviceLedState);
            DeviceAudioMuteStateFeedback = new BoolFeedback(() => DeviceAudioMuteState);
            DeviceMuteStatusLedStateFeedback = new BoolFeedback(() => DeviceMuteStatusLedState);
            ExternalSwitchStateFeedback = new BoolFeedback(() => ExternalSwitchState);

            // analog feedbacks
            LedMutedColorNumberFeedback = new IntFeedback(() => (int)LedMutedColorNumber);
            LedUnmutedColorNumberFeedback = new IntFeedback(() => (int)LedUnmutedColorNumber);
            CurrentPresetIntFeedback = new IntFeedback(() => (int)CurrentPreset);

            // serial feedbacks
            DeviceModelFeedback = new StringFeedback(() => DeviceModel);
            DeviceSerialNumberFeedback = new StringFeedback(() => DeviceSerialNumber);
            DeviceFirmwareVersionFeedback = new StringFeedback(() => DeviceFirmwareVersion);
            DeviceErrorFeedback = new StringFeedback(() => DeviceError);
            LedMutedColorNameFeedback = new StringFeedback(() => LedMutedColorName);
            LedUnmutedColorNameFeedback = new StringFeedback(() => LedUnmutedColorName);

            _comms = comms;
            var commsGather = new CommunicationGather(_comms, CommsDelimiter) { IncludeDelimiter = true };
            commsGather.LineReceived += Handle_LineRecieved;
            _commsMonitor = new GenericCommunicationMonitor(this, _comms, 30000, 180000, 300000, Poll);
            _commsQueue = new GenericQueue(key + "-queue");

            deviceMuteChangeTimer = new CTimer(DeviceMuteChangeTimerCallback, Timeout.Infinite);

            var socket = _comms as ISocketStatus;
            if (socket != null)
            {
                // device comms is IP **ELSE** device comms is RS232
                socket.ConnectionChange += socket_ConnectionChange;
                SocketStatusFeedback = new IntFeedback(() => (int)socket.ClientStatus);
            }

            if (_config.DspObjectKey != null)
            {
                DspObjectMutex = new CMutex();
                DeviceObjectMutex = new CMutex();
            }
        }


        /// <summary>
        /// Use the custom activiate to connect the device and start the comms monitor.
        /// This method will be called when the device is built.
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            if (_config.DspObjectKey != null)
            {
                var dspObject = DeviceManager.GetDeviceForKey(_config.DspObjectKey) as IBasicVolumeWithFeedback;
                if (dspObject != null)
                {
                    Debug.Console(1, this, "Linking {0} to dsp object", this.Name, _config.DspObjectKey);
                    DspObject = dspObject;
                    dspObject.MuteFeedback.OutputChange += DspMuteFeedbackChange;
                    this.DeviceAudioMuteStateFeedback.OutputChange += DeviceMuteStateChange;
                }
            }

            // Essentials will handle the connect method to the device                       
            _comms.Connect();
            // Essentialss will handle starting the comms monitor
            _commsMonitor.Start();

            return base.CustomActivate();
        }

        private void DspMuteFeedbackChange(object obj, FeedbackEventArgs args)
        {
            DeviceMuteChangeTimerStart();
            if (!dspObjectLock)
            {
                CrestronInvoke.BeginInvoke((o) =>
                {
                    dspObjectLock = true;
                    bool test = DspObjectMutex.WaitForMutex();
                    if (test)
                    {
                        try
                        {
                            dspObjectLock = false;
                            if (DeviceAudioMuteState != DspObject.MuteFeedback.BoolValue)
                            {
                                Debug.Console(1, this, "Got dsp feedback. Setting mic state to {0}", DspObject.MuteFeedback.BoolValue);
                                if (DspObject.MuteFeedback.BoolValue)
                                {
                                    this.SetDeviceAudioMuteOn();
                                }
                                else
                                {
                                    this.SetDeviceAudioMuteOff();
                                }
                            }
                        }
                        finally
                        {
                            Thread.Sleep(1000);
                            DspObjectMutex.ReleaseMutex();
                        }
                    }
                });
            }
        }

        private void DeviceMuteChangeTimerStart()
        {
            deviceMuteChangeInProgress = true;
            deviceMuteChangeTimer.Reset(5000);
        }

        private void DeviceMuteChangeTimerCallback(object o)
        {
            deviceMuteChangeInProgress = false;
        }

        private void DeviceMuteStateChange(object obj, FeedbackEventArgs args)
        {
            if (!deviceObjectLock)
            {
                DeviceMuteChangeTimerStart();
                CrestronInvoke.BeginInvoke((o) =>
                {
                    deviceObjectLock = true;
                    bool test = DeviceObjectMutex.WaitForMutex();
                    if (test)
                    {
                        deviceObjectLock = false;
                        try
                        {
                            if (DspObject.MuteFeedback.BoolValue != DeviceAudioMuteState)
                            {
                                Debug.Console(1, this, "Got mic state feedback, Setting dsp state to {0}", DeviceAudioMuteState);
                                if (DeviceAudioMuteState)
                                {
                                    DspObject.MuteOn();
                                }
                                else
                                {
                                    DspObject.MuteOff();
                                }
                            }
                        }
                        finally
                        {
                            Thread.Sleep(1000);
                            DeviceObjectMutex.ReleaseMutex();
                        }
                    }
                });
            }
        }

        // socket connection change event handler
        private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
        {
            if (SocketStatusFeedback != null)
                SocketStatusFeedback.FireUpdate();

            if (args.Client.IsConnected)
                UpdateStatus();
        }


        // handles line recieved		
        private void Handle_LineRecieved(object sender, GenericCommMethodReceiveTextArgs args)
        {
            _commsQueue.Enqueue(new ProcessStringMessage(args.Text, ProcessLineRecieved));
        }


        // processes linee recieved
        private void ProcessLineRecieved(string lineRecieved)
        {
            if (string.IsNullOrEmpty(lineRecieved)) return;

            Debug.Console(2, this, "ProcessLineRecieved: lineReceived = {0}", lineRecieved);

            // Shure MXA910 command strings
            // https://pubs.shure.com/command-strings/MXA910			
            // Shure MXA310 command strings
            // https://pubs.shure.com/command-strings/MXA310

            var regexPattern = new Regex(@"< REP (?<Index>[0-9]\s)?(?<Command>.*\b) (?<State>\w+|\{.*\}) >", RegexOptions.IgnoreCase);
            var responses = regexPattern.Match(lineRecieved);
            if (responses == null) return;

            Debug.Console(2, this, "group[{0}-Index] = {1}", responses.Groups["Index"].Index, responses.Groups["Index"].Value);
            Debug.Console(2, this, "group[{0}-Command] = {1}", responses.Groups["Command"].Index, responses.Groups["Command"].Value);
            Debug.Console(2, this, "group[{0}-State] = {1}", responses.Groups["State"].Index, responses.Groups["State"].Value);

            char[] trimPattern = { '{', '}', ' ' };

            var index = responses.Groups["Index"].Value.Trim();
            var command = responses.Groups["Command"].Value.Trim();
            var state = responses.Groups["State"].Value.Trim(trimPattern);

            if (string.IsNullOrEmpty(command)) return;

            Debug.Console(2, this, "ProcessLineRecieved: index-'{0}' | command-'{1} | state-'{2}'", index, command, state);

            switch (command)
            {
                // LED State (LED on/off)
                // TX: "< GET DEV_LED_IN_STATE >"
                // TX: "< SET DEV_LED_IN_STATE {ON|OFF} >"
                // RX: "< REP DEV_LED_IN_STATE {ON|OFF} >"
                case "DEV_LED_IN_STATE":
                    {
                        DeviceLedState = state.Contains("ON");
                        break;
                    }
                // Device Audio Mute
                // TX: "< GET DEVICE_AUDIO_MUTE >"
                // TX: "< SET DEVICE_AUDIO_MUTE {ON|OFF|TOGGLE} >"
                // RX: "< REP DEVICE_AUDIO_MUTE {ON|OFF} >"
                case "DEVICE_AUDIO_MUTE":
                    {
                        DeviceAudioMuteState = state.Contains("ON");

                        if (_config.DspObjectKey != null)
                        {
                            if ((DspObject.MuteFeedback.BoolValue != DeviceAudioMuteState) && !deviceMuteChangeInProgress)
                            {
                                Debug.Console(0, this, "Dsp feedback doesn't match. Setting mic state to {0}", DspObject.MuteFeedback);
                                if (DspObject.MuteFeedback.BoolValue)
                                {
                                    SetDeviceAudioMuteOn();
                                }
                                else
                                {
                                    SetDeviceAudioMuteOff();
                                }
                            }
                        }
                        break;
                    }
                // Device Mute LED state (on = muted, off = unmuted)
                // TX: "< GET DEV_MUTE_STATUS_LED_STATE >"
                // RX: "< REP DEV_MUTE_STATUS_LED_STATE {ON|OFF} >"
                case "DEV_MUTE_STATUS_LED_STATE":
                    {
                        DeviceMuteStatusLedState = state.Contains("ON");
                        break;
                    }
                // External switch out **MXA310 ONLY**
                // TX: "< GET EXT_SWITCH_OUT_STATE >"	
                // RX: "< REP EXT_SWITCH_OUT_STATE {ON|OFF} >"
                case "EXT_SWITCH_OUT_STATE":
                    {
                        ExternalSwitchState = state.Contains("ON");
                        break;
                    }
                // Get Current CurrentPreset
                // TX: "< GET PRESET >"
                // RX: "< REP PRESET {n} >" // n is preset number, 1-10
                // Set CurrentPreset
                // TX: "< SET PRESET {n} >" // n is preset number, 1-10
                // RX: "< REP PRESET {n} >" // n is preset number, 1-10
                // Get CurrentPreset Name
                // TX: "< GET PRESET{n} >" // n is preset number, 1-10
                // RX: "< REP PRESET{n} {y} >" // n is preset number, 1-10, y is 25-char preset name
                case "PRESET":
                    {
                        CurrentPreset = Convert.ToUInt16(state);
                        break;
                    }

                // Model Number
                // TX: "< GET MODEL >"
                // RX: "< REP MODEL {y} >"	// y is 32-char model number
                case "MODEL":
                    {
                        DeviceModel = state;
                        break;
                    }
                // Serial Number
                // TX: "< GET SERIAL_NUM >"
                // RX: "< REP SERIAL_NUM {y} >" // y is 32-char serial number
                case "SERIAL_NUM":
                    {
                        DeviceSerialNumber = state;
                        break;
                    }
                // Firmware Version
                // TX: "< GET FW_VER >"
                // RX: "< REP FW_VER {y} >" // y is 18-char firmware version
                case "FW_VER":
                    {
                        DeviceFirmwareVersion = state;
                        break;
                    }
                // Device ID
                // TX: "< GET DEVICE_ID >"
                // RX: "< REP DEVICE_ID {y} >" // y is 31-char device ID
                //else if (commandState.Contains("DEVICE_ID"))
                //{				
                //}
                // LED Muted/Unmuted Color
                // TX: "< GET LED_COLOR_{MUTED|UNMUTED} >"
                // TX: "< SET LED_COLOR_{MUTED|UNMUTED} {RED|GREEN|BLUE|PINK|PURPLE|YELLOW|ORANGE|WHITE} >" // FW ver < 3.0
                // TX: "< SET LED_COLOR_{MUTED|UNMUTED} {RED|GREEN|BLUE|PINK|PURPLE|YELLOW|ORANGE|WHITE|GOLD|YELLOWGREEN|TURQUOISE|POWDERBLUE|CYAN|SKYBLUE|LIGHTPURPLE|VIOLET|ORCHID} >" // FW ver > 3.0
                // RX: "< REP LED_COLUR_{MUTED|UNMUTED} {n} >" // n is LED color
                case "LED_COLOR_MUTED":
                    {
                        LedMutedColorName = state;
                        try
                        {
                            var colorNumber = (ELedColor)Enum.Parse(typeof(ELedColor), LedMutedColorName, true);
                            LedMutedColorNumber = (uint)colorNumber;
                        }
                        catch (Exception ex)
                        {
                            Debug.Console(1, this, "LedMutedColorName: Enum.Parse({0}) exception: {1}", LedMutedColorName, ex);
                        }
                        break;
                    }
                case "LED_COLOR_UNMUTED":
                    {
                        LedUnmutedColorName = state;
                        try
                        {
                            var colorNumber = (ELedColor)Enum.Parse(typeof(ELedColor), LedUnmutedColorName, true);
                            LedUnmutedColorNumber = (uint)colorNumber;
                        }
                        catch (Exception ex)
                        {
                            Debug.Console(1, this, "LedUnmutedColorName: Enum.Parse({0}) exception: {1}", LedUnmutedColorName, ex);
                        }
                        break;
                    }
                // LED Muted/Unmuted Behavior
                // TX: "< GET LED_STATE_{MUTED|UNMUTED} >"
                // TX: "< SET LED_STATE_{MUTED|UNMUTED} {ON|OFF|FLASHING} >"
                //case "LED_STATE_MUTED":
                //{				
                //	break;
                //}
                //case "LED_STATE_UNMUTED":
                //{				
                //	break;
                //}
                // Error events
                // TX: "< GET LAST_ERROR_EVENT >"
                // RX: "< REP LAST_ERROR_EVENT {y} >" // y is up-to 128-char response
                //case "LAST_ERROR_EVENT": 
                //{			
                //	break;
                //}
                // Mute Button Status **MXA310 ONLY**
                // TX: "< GET MUTE_BUTTON_STATUS >"
                // RX: "< REP MUTE_BUTTON_STATUS >"
                //case "MUTE_BUTTON_STATUS":
                //{				
                //	break;
                //}
                // Mute Button LED State **MXA310 ONLY**
                // *** this command is only available when both 'mute control function' is set to 'logic out' OR 'disabled' AND light ring 'lighting style' is set to 'ring' from the GUI.
                // TX: "< GET MUTE_BUTTON_LED_STATE >"
                // RX: "< REP MUTE_BUTTON_LED_STATE {ON|OFF} >"
                //case "MUTE_BUTTON_LED_STATE":
                //{				
                //	break;
                //}
                case "ERR":
                    {
                        DeviceError = state;
                        break;
                    }
                default:
                    {
                        Debug.Console(1, this, "ProcessLineReceived: Unkown command-'{0}' with state-'{1}'", command, state);
                        break;
                    }
            }
        }

        /// <summary>
        /// Sends text to the device plugin comms
        /// </summary>
        /// <param name="text">Command to be sent</param>		
        public void SendText(string text)
        {
            if (_comms.IsConnected == false) return;

            if (string.IsNullOrEmpty(text)) return;

            var cmd = string.Format("< {0} >", text.ToUpper());

            Debug.Console(1, this, "SendText: {0}", cmd);
            _comms.SendText(cmd);
        }

        #region Polls

        /// <summary>
        /// Polls the device
        /// </summary>
        /// <remarks>
        /// Poll method is used by the communication monitor.  Update the poll method as needed for the plugin being developed
        /// </remarks>
        public void Poll()
        {
            SendText("GET DEVICE_AUDIO_MUTE");
        }

        #endregion Polls


        #region Overrides of EssentialsBridgeableDevice

        /// <summary>
        /// Links the plugin device to the EISC bridge
        /// </summary>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
        /// <param name="bridge"></param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new ShureMxaBridgeJoinMap(joinStart);

            // This adds the join map to the collection on the bridge
            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);
            if (customJoins != null)
            {
                joinMap.SetCustomJoinData(customJoins);
            }

            Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);

            // links to bridge
            trilist.SetString(joinMap.DeviceName.JoinNumber, Name);

            // _commsMonitor.IsOnlineFeedback is used to drive IsOnlineFb on the bridge
            _commsMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            SocketStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.SocketStatus.JoinNumber]);
            MonitorStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.MonitorStatus.JoinNumber]);

            // reboot **press&hold, 5s**
            trilist.SetSigHeldAction(joinMap.Reboot.JoinNumber, 5000, DeviceReboot);

            // update all property statuses **trigger only**
            trilist.SetSigTrueAction(joinMap.UpdateStatus.JoinNumber, UpdateStatus);

            // device led state (DEV_LED_IN_STATE)
            trilist.SetSigTrueAction(joinMap.DeviceLedStateOn.JoinNumber, SetDeviceLedStateOn);
            trilist.SetSigTrueAction(joinMap.DeviceLedStateOff.JoinNumber, SetDeviceLedStateOff);
            DeviceLedStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DeviceLedStateOn.JoinNumber]);
            DeviceLedStateFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.DeviceLedStateOff.JoinNumber]);

            // device audio mute (DEVICE_AUDIO_MUTE)
            trilist.SetSigTrueAction(joinMap.DeviceAudioMuteToggle.JoinNumber, ToggleDeviceAudioMute);
            trilist.SetSigTrueAction(joinMap.DeviceAudioMuteOn.JoinNumber, SetDeviceAudioMuteOn);
            trilist.SetSigTrueAction(joinMap.DeviceAudioMuteOff.JoinNumber, SetDeviceAudioMuteOff);
            DeviceAudioMuteStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DeviceAudioMuteToggle.JoinNumber]);
            DeviceAudioMuteStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DeviceAudioMuteOn.JoinNumber]);
            DeviceAudioMuteStateFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.DeviceAudioMuteOff.JoinNumber]);

            // device mute LED status (DEV_MUTE_STATUS_LED_STATE) **feedback only**
            DeviceMuteStatusLedStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DeviceMuteStatusLedOn.JoinNumber]);
            DeviceMuteStatusLedStateFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.DeviceMuteStatusLedOff.JoinNumber]);

            // external switch (EXT_SWITCH_OUT_STATE) **feedback only**
            ExternalSwitchStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.ExternalSwitchOn.JoinNumber]);
            ExternalSwitchStateFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.ExternalSwitchOff.JoinNumber]);

            // Led color muted (LED_COLOR_MUTED)
            trilist.SetUShortSigAction(joinMap.LedMutedColorNumber.JoinNumber, value => SetDeviceLedColorMuted(value));
            LedMutedColorNumberFeedback.LinkInputSig(trilist.UShortInput[joinMap.LedMutedColorNumber.JoinNumber]);
            LedMutedColorNameFeedback.LinkInputSig(trilist.StringInput[joinMap.LedMutedColorName.JoinNumber]);

            // Led color unmuted (LED_COLOR_UNMUTED)
            trilist.SetUShortSigAction(joinMap.LedUnmutedColorNumber.JoinNumber, value => SetDeviceLedColorUnmuted(value));
            LedUnmutedColorNumberFeedback.LinkInputSig(trilist.UShortInput[joinMap.LedUnmutedColorNumber.JoinNumber]);
            LedUnmutedColorNameFeedback.LinkInputSig(trilist.StringInput[joinMap.LedUnmutedColorName.JoinNumber]);

            // presets
            trilist.SetUShortSigAction(joinMap.PresetRecallByNumber.JoinNumber, value => RecallPreset(value));
            CurrentPresetIntFeedback.LinkInputSig(trilist.UShortInput[joinMap.PresetRecallByNumber.JoinNumber]);
            foreach (var item in _config.Presets)
            {
                var join = joinMap.PresetNames.JoinNumber + item.Key - 1;
                var key = item.Key;
                var name = item.Value.Name;
                Debug.Console(1, this, "Preset Names: {0}-{1} @ Join-{2}", key, name, join);
                trilist.SetString(join, name);
            }

            // device information feedback
            DeviceModelFeedback.LinkInputSig(trilist.StringInput[joinMap.DeviceModel.JoinNumber]);
            DeviceSerialNumberFeedback.LinkInputSig(trilist.StringInput[joinMap.DeviceSerialNumber.JoinNumber]);
            DeviceFirmwareVersionFeedback.LinkInputSig(trilist.StringInput[joinMap.DeviceFirmwareVersion.JoinNumber]);
            DeviceErrorFeedback.LinkInputSig(trilist.StringInput[joinMap.DeviceError.JoinNumber]);

            UpdateFeedbacks();

            trilist.OnlineStatusChange += (o, a) =>
            {
                if (!a.DeviceOnLine) return;

                trilist.SetString(joinMap.DeviceName.JoinNumber, Name);

                foreach (var item in _config.Presets)
                {
                    var join = joinMap.PresetNames.JoinNumber + item.Key - 1;
                    var key = item.Key;
                    var name = item.Value.Name;
                    Debug.Console(1, this, "Preset Names: {0}-{1} @ Join-{2}", key, name, join);
                    trilist.SetString(join, name);
                }

                UpdateFeedbacks();
            };
        }

        private void UpdateFeedbacks()
        {
            SocketStatusFeedback.FireUpdate();
            MonitorStatusFeedback.FireUpdate();

            DeviceLedStateFeedback.FireUpdate();
            DeviceAudioMuteStateFeedback.FireUpdate();
            DeviceMuteStatusLedStateFeedback.FireUpdate();
            DeviceErrorFeedback.FireUpdate();
            ExternalSwitchStateFeedback.FireUpdate();

            LedMutedColorNumberFeedback.FireUpdate();
            LedUnmutedColorNumberFeedback.FireUpdate();

            DeviceModelFeedback.FireUpdate();
            DeviceSerialNumberFeedback.FireUpdate();
            DeviceFirmwareVersionFeedback.FireUpdate();
            LedMutedColorNameFeedback.FireUpdate();
            LedUnmutedColorNameFeedback.FireUpdate();
        }

        #endregion Overrides of EssentialsBridgeableDevice

        /// <summary>
        /// Reboot device
        /// </summary>
        public void DeviceReboot()
        {
            SendText("SET REBOOT");
        }

        /// <summary>
        /// Update status of all parameters
        /// Shure command string API recommends ruunning this command on first power up
        /// </summary>
        public void UpdateStatus()
        {
            SendText("GET 0 ALL");
        }

        /// <summary>
        /// Flash device to identify control
        /// </summary>
        /// <param name="state">true/false</param>
        public void DeviceFlash(bool state)
        {
            SendText(string.Format("SET FLASH {0}", state ? "ON" : "OFF"));
        }
    }

    /// <summary>
    /// Shure MXA Plugin device configuration object
    /// </summary>
    /// <example>
    /// <code>
    /// {
    ///		"devices": [
    ///			{
    ///				"key": "shuremxa1-plugin",
    ///				"name": "Shure MXA Plugin",
    ///				"type": "shuremxa",
    ///				"group": "pluginDevices",
    ///				"properties": {
    ///					"control": {	
    ///						"tcpSshProperties": {
    ///							"address": "",
    ///							"port": 2202,
    ///							"username": "",
    ///							"password": "",
    ///							"autoReconnect": true,
    ///							"autoReconnectIntervalMs": 5000
    ///						}
    ///					},
    ///					"pollTimeMs": 30000,
    ///					"warningTimeoutMs": 180000,
    ///					"errorTimeoutMs": 300000,
    ///					"deviceId": 1,
    ///					"presets": {
    ///						"1": { "name": "CurrentPreset 1"	},
    ///						"2": { "name": "CurrentPreset 2"	}
    ///					}
    ///				}
    ///			}
    ///		]
    /// }
    /// </code>
    /// </example>
    [ConfigSnippet("{\"devices\":[{\"key\":\"shuremxa1-plugin\",\"name\":\"Shure MXA Plugin\",\"type\":\"shuremxa\",\"group\":\"pluginDevices\",\"properties\":{\"control\":{\"method\":\"tcpip\",\"tcpSshProperties\":{\"address\":\"\",\"port\":2202,\"username\":\"\",\"password\":\"\",\"autoReconnect\":true,\"autoReconnectIntervalMs\":5000}},\"pollTimeMs\":30000,\"warningTimeoutMs\":180000,\"errorTimeoutMs\":300000,\"deviceId\":1,\"linkWithDspObjectKey\":\"dsp01--fader11\",\"presets\":{\"1\":{\"name\":\"CurrentPreset 1\"},\"2\":{\"name\":\"CurrentPreset 2\"}}}}]}")]
    public class ShureMxaConfig
    {
        /// <summary>
        /// JSON control object
        /// </summary>
        [JsonProperty("control")]
        public EssentialsControlPropertiesConfig Control { get; set; }

        /// <summary>
        /// DSP object to link mute state to
        /// </summary>
        [JsonProperty("dspObjectKey")]
        public string DspObjectKey { get; set; }

        /// <summary>
        /// Device ID
        /// </summary>
        [JsonProperty("deviceId")]
        public long DeviceId { get; set; }

        /// <summary>
        /// CurrentPreset name dictionary
        /// </summary>
        [JsonProperty("presets")]
        public Dictionary<uint, ShureMxaPresetsConfig> Presets { get; set; }

        /// <summary>
        /// Constuctor
        /// </summary>
        public ShureMxaConfig()
        {
            Presets = new Dictionary<uint, ShureMxaPresetsConfig>();
        }
    }

    /// <summary>
    /// CurrentPreset name dictionary
    /// </summary>
    /// <example>
    /// <code>
    /// "properties": {
    ///		"presets": {
    ///			"1": { "name": "CurrentPreset 1" },
    ///			"2": { "name": "CurrentPreset 2" }
    ///		}
    /// }
    /// </code>
    /// </example>
    public class ShureMxaPresetsConfig
    {
        /// <summary>
        /// Serializes collection name property
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class ShureMxaBridgeJoinMap : JoinMapBaseAdvanced
    {
        #region Digital

        /// <summary>
        /// Get device online feedback
        /// </summary>
        [JoinName("IsOnline")]
        public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Is Online",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Set device reboot
        /// </summary>
        [JoinName("Reboot")]
        public JoinDataComplete Reboot = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 12,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Reboot",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Update status of all parameters
        /// </summary>
        [JoinName("UpdateStatus")]
        public JoinDataComplete UpdateStatus = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Update Status",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Device LED State On
        /// </summary>
        /// <example>
        /// "{GET|SET|REP} DEV_LED_IN_STATE ON"
        /// </example>
        [JoinName("DeviceLedStateOn")]
        public JoinDataComplete DeviceLedStateOn = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 5,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device LED State On",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Device LED State Off
        /// </summary>
        /// <example>
        /// "{GET|SET|REP} DEV_LED_IN_STATE OFF"
        /// </example>
        [JoinName("DeviceLedStateOff")]
        public JoinDataComplete DeviceLedStateOff = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 6,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device LED State Off",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Toggle device audio mute
        /// </summary>
        /// <example>
        /// "{SET|REP} DEVICE_AUDIO_MUTE TOGGLE"
        /// </example>
        [JoinName("DeviceAudioMuteToggle")]
        public JoinDataComplete DeviceAudioMuteToggle = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 4,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Audio Mute Toggle",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Get/Set device audio mute on
        /// </summary>
        /// <example>
        /// "{GET|SET|REP} DEVICE_AUDIO_MUTE {ON|OFF}"
        /// </example>
        [JoinName("DeviceAudioMuteOn")]
        public JoinDataComplete DeviceAudioMuteOn = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Audio Mute On",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Get/Set device audio mute off
        /// </summary>
        /// <example>
        /// "{GET|SET|REP} DEVICE_AUDIO_MUTE {ON|OFF}"
        /// </example>
        [JoinName("DeviceAudioMuteOff")]
        public JoinDataComplete DeviceAudioMuteOff = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 3,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Audio Mute Off",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Get device mute status led on feedback
        /// </summary>
        /// <example>
        /// "{GET|REP} DEVICE_MUTE_STATUS_LED_STATE {ON|OFF}"
        /// </example>
        [JoinName("DeviceMuteStatusLedOn")]
        public JoinDataComplete DeviceMuteStatusLedOn = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 9,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Mute Status LED On",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Get device mute status led off feedback
        /// </summary>
        /// <example>
        /// "{GET|REP} DEVICE_MUTE_STATUS_LED_STATE {ON|OFF}"
        /// </example>
        [JoinName("DeviceMuteStatusLedOff")]
        public JoinDataComplete DeviceMuteStatusLedOff = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 10,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Mute Status LED Off",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Gets external switch state
        /// </summary>
        /// <example>
        /// "{GET|REP} EXT_SWITCH_OUT_STATE ON"
        /// </example>
        [JoinName("ExternalSwitchOn")]
        public JoinDataComplete ExternalSwitchOn = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 7,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "External Switch On",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Gets external switch state
        /// </summary>
        /// <example>
        /// "{GET|REP} EXT_SWITCH_OUT_STATE OFF"
        /// </example>
        [JoinName("ExternalSwitchOff")]
        public JoinDataComplete ExternalSwitchOff = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 8,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "External Switch Off",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        #endregion


        #region Analog

        /// <summary>
        /// Get device socket status join map
        /// </summary>
        /// <see cref="Crestron.SimplSharp.CrestronSockets.SocketStatus"/>
        [JoinName("SocketStatus")]
        public JoinDataComplete SocketStatus = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Socket SocketStatus",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Get device monitor status join map
        /// </summary>
        /// <see cref="PepperDash.Essentials.Core.MonitorStatus"/>
        [JoinName("MonitorStatus")]
        public JoinDataComplete MonitorStatus = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Monitor Status",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Get/Set LED muted color by number
        /// </summary>
        /// <remarks>
        /// Red = 0,
        /// Greeen = 1,
        /// Blue = 2,
        /// Pink = 3,
        /// Purple = 4,
        /// Yellow = 5,
        /// Orange = 6,
        /// White = 7,
        /// Gold = 8,
        /// YellowGreen = 9,
        /// Turquoise = 10,
        /// PowderBlue = 11, 
        /// Cyan = 12,
        /// SkyBlue = 13,
        /// LightPurple = 14,
        /// Violet = 15,
        /// Orchid = 16
        /// </remarks>
        /// <example>
        /// "{GET|SET|REP} LED_COLOR_MUTED {COLOR}"
        /// </example>
        [JoinName("LedMutedColorNumber")]
        public JoinDataComplete LedMutedColorNumber = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 6,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "LED Muted Color Number",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Get/Set MED unmuted color by number 
        /// </summary>
        /// <remarks>
        /// Red = 0,
        /// Greeen = 1,
        /// Blue = 2,
        /// Pink = 3,
        /// Purple = 4,
        /// Yellow = 5,
        /// Orange = 6,
        /// White = 7,
        /// Gold = 8,
        /// YellowGreen = 9,
        /// Turquoise = 10,
        /// PowderBlue = 11, 
        /// Cyan = 12,
        /// SkyBlue = 13,
        /// LightPurple = 14,
        /// Violet = 15,
        /// Orchid = 16
        /// </remarks>
        /// <example>
        /// "{GET|SET|REP} LED_COLOR_UNMUTED {COLOR}"
        /// </example>
        [JoinName("LedUnmutedColorNumber")]
        public JoinDataComplete LedUnmutedColorNumber = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 7,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "LED Unmuted Color Number",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Get/Set preset by number
        /// </summary>
        /// <example>
        /// "{GET|SET|REP} PRESET {n}"
        /// </example>
        [JoinName("PresetRecallByNumber")]
        public JoinDataComplete PresetRecallByNumber = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "CurrentPreset Recall by Number",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        #endregion


        #region Serial

        /// <summary>
        /// Get device name
        /// </summary>
        [JoinName("DeviceName")]
        public JoinDataComplete DeviceName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        /// Get device model
        /// </summary>
        /// <example>
        /// "{GET|REP} MODEL {y}"
        /// </example>
        [JoinName("DeviceModel")]
        public JoinDataComplete DeviceModel = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Model",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        /// Get device serial number
        /// </summary>
        /// <example>
        /// "{GET|REP} SERIAL_NUM {y}"
        /// </example>
        [JoinName("DeviceSerialNumber")]
        public JoinDataComplete DeviceSerialNumber = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 3,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Serial Number",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        /// Get device firmware version
        /// </summary>
        /// <example>
        /// "{GET|REP} FW_VER {y}"
        /// </example>
        [JoinName("DeviceFirmwareVersion")]
        public JoinDataComplete DeviceFirmwareVersion = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 4,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Firmware Version",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        /// Outputs device error, if recieved
        /// </summary>
        /// <example>
        /// "REP ERR {y}"
        /// </example>
        [JoinName("DeviceError")]
        public JoinDataComplete DeviceError = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 5,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Error",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        /// Get/Set LED muted color by Name 
        /// </summary>
        /// <remarks>
        /// Red = 0,
        /// Greeen = 1,
        /// Blue = 2,
        /// Pink = 3,
        /// Purple = 4,
        /// Yellow = 5,
        /// Orange = 6,
        /// White = 7,
        /// Gold = 8,
        /// YellowGreen = 9,
        /// Turquoise = 10,
        /// PowderBlue = 11, 
        /// Cyan = 12,
        /// SkyBlue = 13,
        /// LightPurple = 14,
        /// Violet = 15,
        /// Orchid = 16
        /// </remarks>
        /// <example>
        /// "{GET|SET|REP} LED_COLOR_UNMUTED {COLOR}"
        /// </example>
        [JoinName("LedMutedColorName")]
        public JoinDataComplete LedMutedColorName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 6,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "LED Muted Color Name",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Serial
            });


        /// <summary>
        /// Get/Set LED unmuted color by number 
        /// </summary>
        /// <remarks>
        /// Red = 0,
        /// Greeen = 1,
        /// Blue = 2,
        /// Pink = 3,
        /// Purple = 4,
        /// Yellow = 5,
        /// Orange = 6,
        /// White = 7,
        /// Gold = 8,
        /// YellowGreen = 9,
        /// Turquoise = 10,
        /// PowderBlue = 11, 
        /// Cyan = 12,
        /// SkyBlue = 13,
        /// LightPurple = 14,
        /// Violet = 15,
        /// Orchid = 16
        /// </remarks>
        /// <example>
        /// "{GET|SET|REP} LED_COLOR_UNMUTED {COLOR}"
        /// </example>
        [JoinName("LedUnmutedColorName")]
        public JoinDataComplete LedUnmutedColorName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 7,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "LED Unmuted Color Name",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        /// Get preset names
        /// </summary>
        /// <example>
        /// "{GET|REP} PRESET{n} {y}"
        /// </example>
        [JoinName("PresetNames")]
        public JoinDataComplete PresetNames = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 10
            },
            new JoinMetadata
            {
                Description = "Preset Names",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });


        #endregion

        /// <summary>
        /// Plugin device BridgeJoinMap constructor
        /// </summary>
        /// <param name="joinStart">This will be the join it starts on the EISC bridge</param>
        public ShureMxaBridgeJoinMap(uint joinStart)
            : base(joinStart, typeof(ShureMxaBridgeJoinMap))
        {
        }
    }

    /// <summary>
    /// Plugin factory for devices that require communications using IBasicCommunications or custom communication methods
    /// </summary>
    public class ShureMxaFactory : EssentialsDeviceFactory<ShureMxaDevice>
    {
        /// <summary>
        /// Device factory constructor
        /// </summary>
        public ShureMxaFactory()
        {
            // In the constructor we initialize the list with the typenames that will build an instance of this device
            // only include unique typenames, when the constructur is used all the typenames will be evaluated in lower case.
            TypeNames = new List<string>() { "shuremxa" };
        }

        /// <summary>
        /// Builds and returns an instance of ShureMxaDevice
        /// </summary>
        /// <param name="dc">device configuration</param>
        /// <returns>plugin device or null</returns>
        /// <seealso cref="PepperDash.Core.eControlMethod"/>
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            try
            {
                Debug.Console(0, "[{0}] Factory attempting to create new device from type: {1}", dc.Key, dc.Type);

                // get the device properties configuration object & check for null 
                var propertiesConfig = dc.Properties.ToObject<ShureMxaConfig>();
                if (propertiesConfig == null)
                {
                    Debug.Console(0, "[{0}] Factory: failed to read properties config for {1}", dc.Key, dc.Name);
                    return null;
                }

                // build the device comms (for all other comms methods) & check for null			
                var comms = CommFactory.CreateCommForDevice(dc);
                if (comms != null) return new ShureMxaDevice(dc.Key, dc.Name, propertiesConfig, comms);
                Debug.Console(0, "[{0}] Factory: failed to create comm for {1}", dc.Key, dc.Name);
                return null;
            }
            catch (Exception ex)
            {
                Debug.Console(0, "[{0}] Factory BuildDevice Exception: {1}", dc.Key, ex);
                return null;
            }
        }
    }
}