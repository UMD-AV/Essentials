using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Queues;

namespace PepperDash.Essentials.Devices.Common.Microphones
{
    public class ShureMxaDevice : EssentialsBridgeableDevice
    {
        private const string CommsDelimiter = ">";

        private readonly IBasicCommunication _comms;
        private readonly GenericCommunicationMonitor _commsMonitor;

        private readonly GenericQueue _commsQueue;
        private readonly ShureMxaConfig _config;
        private readonly CTimer _deviceMuteChangeTimer;
        private readonly CMutex _deviceObjectMutex;
        private readonly CMutex _dspObjectMutex;

        private readonly Regex _regexPattern = new Regex(
            @"< REP (?<Index>[0-9]\s)?(?<Command>.*\b) (?<State>\w+|\{.*\}) >",
            RegexOptions.IgnoreCase);

        private bool _deviceMuteChangeInProgress;
        private bool _deviceObjectLock;

        private IBasicVolumeWithFeedback _dspObject;
        private bool _dspObjectLock;


        /// <summary>
        ///     Plugin device constructor
        /// </summary>
        /// <param name="key">device key</param>
        /// <param name="name">device name</param>
        /// <param name="config">device configuration object</param>
        /// <param name="comms">device communication as IBasicCommunication</param>
        /// <see cref="PepperDash.Core.IBasicCommunication" />
        /// <seealso cref="Crestron.SimplSharp.CrestronSockets.SocketStatus" />
        public ShureMxaDevice(string key, string name, ShureMxaConfig config, IBasicCommunication comms)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new {0} instance", name);

            _config = config;

            MonitorStatusFeedback = new IntFeedback(() =>
            {
                if (_commsMonitor != null) return (int)_commsMonitor.Status;
                return 0;
            });

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
            CommunicationGather commsGather = new CommunicationGather(_comms, CommsDelimiter)
                { IncludeDelimiter = true };
            commsGather.LineReceived += Handle_LineReceived;
            _commsMonitor = new GenericCommunicationMonitor(this, _comms, 30000, 180000, 300000, Poll);
            _commsQueue = new GenericQueue(key + "-queue");

            _deviceMuteChangeTimer = new CTimer(DeviceMuteChangeTimerCallback, Timeout.Infinite);

            ISocketStatus socket = _comms as ISocketStatus;
            if (socket != null)
            {
                // device comms is IP **ELSE** device comms is RS232
                socket.ConnectionChange += socket_ConnectionChange;
                SocketStatusFeedback = new IntFeedback(() => (int)socket.ClientStatus);
            }

            if (_config.DspObjectKey != null)
            {
                _dspObjectMutex = new CMutex();
                _deviceObjectMutex = new CMutex();
            }
        }

        /// <summary>
        ///     Reports socket status feedback through the bridge
        /// </summary>
        public IntFeedback SocketStatusFeedback { get; private set; }

        /// <summary>
        ///     Reports monitor status feedback through the bridge
        ///     Typically used for Fusion status reporting and system status LED's
        /// </summary>
        public IntFeedback MonitorStatusFeedback { get; private set; }


        /// <summary>
        ///     Use the custom activate to connect the device and start the comms monitor.
        ///     This method will be called when the device is built.
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            if (_config.DspObjectKey != null)
            {
                IBasicVolumeWithFeedback dspObject =
                    DeviceManager.GetDeviceForKey(_config.DspObjectKey) as IBasicVolumeWithFeedback;
                if (dspObject != null)
                {
                    Debug.Console(1, this, "Linking {0} to dsp object", Name, _config.DspObjectKey);
                    _dspObject = dspObject;
                    dspObject.MuteFeedback.OutputChange += DspMuteFeedbackChange;
                    DeviceAudioMuteStateFeedback.OutputChange += DeviceMuteStateChange;
                }
            }

            // Essentials will handle the connect method to the device                       
            _comms.Connect();
            // Essentials will handle starting the comms monitor
            _commsMonitor.Start();

            return base.CustomActivate();
        }

        private void DspMuteFeedbackChange(object obj, FeedbackEventArgs args)
        {
            DeviceMuteChangeTimerStart();
            if (!_dspObjectLock)
                CrestronInvoke.BeginInvoke(o =>
                {
                    _dspObjectLock = true;
                    bool test = _dspObjectMutex.WaitForMutex();
                    if (test)
                        try
                        {
                            _dspObjectLock = false;
                            if (DeviceAudioMuteState != _dspObject.MuteFeedback.BoolValue)
                            {
                                Debug.Console(1, this, "Got dsp feedback. Setting mic state to {0}",
                                    _dspObject.MuteFeedback.BoolValue);
                                if (_dspObject.MuteFeedback.BoolValue)
                                    SetDeviceAudioMuteOn();
                                else
                                    SetDeviceAudioMuteOff();
                            }
                        }
                        finally
                        {
                            Thread.Sleep(1000);
                            _dspObjectMutex.ReleaseMutex();
                        }
                });
        }

        private void DeviceMuteChangeTimerStart()
        {
            _deviceMuteChangeInProgress = true;
            _deviceMuteChangeTimer.Reset(5000);
        }

        private void DeviceMuteChangeTimerCallback(object o)
        {
            _deviceMuteChangeInProgress = false;
        }

        private void DeviceMuteStateChange(object obj, FeedbackEventArgs args)
        {
            if (!_deviceObjectLock)
            {
                DeviceMuteChangeTimerStart();
                CrestronInvoke.BeginInvoke(o =>
                {
                    _deviceObjectLock = true;
                    bool test = _deviceObjectMutex.WaitForMutex();
                    if (test)
                    {
                        _deviceObjectLock = false;
                        try
                        {
                            if (_dspObject.MuteFeedback.BoolValue != DeviceAudioMuteState)
                            {
                                Debug.Console(1, this, "Got mic state feedback, Setting dsp state to {0}",
                                    DeviceAudioMuteState);
                                if (DeviceAudioMuteState)
                                    _dspObject.MuteOn();
                                else
                                    _dspObject.MuteOff();
                            }
                        }
                        finally
                        {
                            Thread.Sleep(1000);
                            _deviceObjectMutex.ReleaseMutex();
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


        // handles line received		
        private void Handle_LineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            _commsQueue.Enqueue(new ProcessStringMessage(args.Text, ProcessLineReceived));
        }


        // processes line received
        private void ProcessLineReceived(string lineReceived)
        {
            if (string.IsNullOrEmpty(lineReceived)) return;

            Debug.Console(2, this, "ProcessLinereceived: lineReceived = {0}", lineReceived);

            // Shure MXA910 command strings
            // https://pubs.shure.com/command-strings/MXA910			
            // Shure MXA310 command strings
            // https://pubs.shure.com/command-strings/MXA310


            Match responses = _regexPattern.Match(lineReceived);

            Debug.Console(2, this, "group[{0}-Index] = {1}", responses.Groups["Index"].Index,
                responses.Groups["Index"].Value);
            Debug.Console(2, this, "group[{0}-Command] = {1}", responses.Groups["Command"].Index,
                responses.Groups["Command"].Value);
            Debug.Console(2, this, "group[{0}-State] = {1}", responses.Groups["State"].Index,
                responses.Groups["State"].Value);

            char[] trimPattern = { '{', '}', ' ' };

            string index = responses.Groups["Index"].Value.Trim();
            string command = responses.Groups["Command"].Value.Trim();
            string state = responses.Groups["State"].Value.Trim(trimPattern);

            if (string.IsNullOrEmpty(command)) return;

            Debug.Console(2, this, "ProcessLinereceived: index-'{0}' | command-'{1} | state-'{2}'", index, command,
                state);

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
                        if (_dspObject.MuteFeedback.BoolValue != DeviceAudioMuteState && !_deviceMuteChangeInProgress)
                        {
                            Debug.Console(0, this, "Dsp feedback doesn't match. Setting mic state to {0}",
                                _dspObject.MuteFeedback);
                            if (_dspObject.MuteFeedback.BoolValue)
                                SetDeviceAudioMuteOn();
                            else
                                SetDeviceAudioMuteOff();
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
                // RX: "< REP LED_COLOR_{MUTED|UNMUTED} {n} >" // n is LED color
                case "LED_COLOR_MUTED":
                {
                    LedMutedColorName = state;
                    try
                    {
                        ELedColor colorNumber = (ELedColor)Enum.Parse(typeof(ELedColor), LedMutedColorName, true);
                        LedMutedColorNumber = (uint)colorNumber;
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(1, this, "LedMutedColorName: Enum.Parse({0}) exception: {1}", LedMutedColorName,
                            ex);
                    }

                    break;
                }
                case "LED_COLOR_UNMUTED":
                {
                    LedUnmutedColorName = state;
                    try
                    {
                        ELedColor colorNumber = (ELedColor)Enum.Parse(typeof(ELedColor), LedUnmutedColorName, true);
                        LedUnmutedColorNumber = (uint)colorNumber;
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(1, this, "LedUnmutedColorName: Enum.Parse({0}) exception: {1}",
                            LedUnmutedColorName, ex);
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
                    Debug.Console(1, this, "ProcessLineReceived: Unkown command-'{0}' with state-'{1}'", command,
                        state);
                    break;
                }
            }
        }

        /// <summary>
        ///     Sends text to the device plugin comms
        /// </summary>
        /// <param name="text">Command to be sent</param>
        public void SendText(string text)
        {
            if (!_comms.IsConnected) return;

            if (string.IsNullOrEmpty(text)) return;

            string cmd = string.Format("< {0} >", text.ToUpper());

            Debug.Console(1, this, "SendText: {0}", cmd);
            _comms.SendText(cmd);
        }

        #region Polls

        /// <summary>
        ///     Polls the device
        /// </summary>
        /// <remarks>
        ///     Poll method is used by the communication monitor.  Update the poll method as needed for the plugin being developed
        /// </remarks>
        public void Poll()
        {
            SendText("GET DEVICE_AUDIO_MUTE");
        }

        #endregion Polls

        /// <summary>
        ///     Reboot device
        /// </summary>
        public void DeviceReboot()
        {
            SendText("SET REBOOT");
        }

        /// <summary>
        ///     Update status of all parameters
        ///     Shure command string API recommends running this command on first power up
        /// </summary>
        public void UpdateStatus()
        {
            SendText("GET 0 ALL");
        }

        /// <summary>
        ///     Flash device to identify control
        /// </summary>
        /// <param name="state">true/false</param>
        public void DeviceFlash(bool state)
        {
            SendText(string.Format("SET FLASH {0}", state ? "ON" : "OFF"));
        }


        #region Device LED state (DEV_LED_IN_STATE)

        // device LED state on/off field
        private bool _deviceLedState;

        /// <summary>
        ///     Device LED state on/off property
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
        ///     Device LED state on/off feedback
        /// </summary>
        public BoolFeedback DeviceLedStateFeedback { get; private set; }

        /// <summary>
        ///     Sets the device LED state
        /// </summary>
        /// <param name="state">boolean value</param>
        /// <returns>null</returns>
        public void SetDeviceLedState(bool state)
        {
            SendText(string.Format("SET DEV_LED_IN_STATE {0}", state ? "ON" : "OFF"));
        }

        /// <summary>
        ///     Sets the device LED state on
        /// </summary>
        /// <remarks>
        ///     Need this to avoid having to use the action delegate that was not working correctly
        /// </remarks>
        public void SetDeviceLedStateOn()
        {
            SetDeviceLedState(true);
        }

        /// <summary>
        ///     Sets the device LED state off
        /// </summary>
        /// <remarks>
        ///     Need this to avoid having to use the action delegate that was not working correctly
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
        ///     Device audio mute state property
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
        ///     Device audio mute state feedback
        /// </summary>
        public BoolFeedback DeviceAudioMuteStateFeedback { get; private set; }

        /// <summary>
        ///     Toggles the device audio mute
        /// </summary>
        public void ToggleDeviceAudioMute()
        {
            DeviceMuteChangeTimerStart();
            SendText("SET DEVICE_AUDIO_MUTE TOGGLE");
        }

        /// <summary>
        ///     Sets the device audio mute state
        /// </summary>
        public void SetDeviceAudioMute(bool state)
        {
            DeviceMuteChangeTimerStart();
            SendText(string.Format("SET DEVICE_AUDIO_MUTE {0}", state ? "ON" : "OFF"));
        }

        /// <summary>
        ///     Sets the device audio mute on
        /// </summary>
        /// <remarks>
        ///     Need this to avoid having to use the action delegate
        /// </remarks>
        public void SetDeviceAudioMuteOn()
        {
            SetDeviceAudioMute(true);
        }

        /// <summary>
        ///     Sets the device audio mute off
        /// </summary>
        /// <remarks>
        ///     Need this to avoid having to use the action delegate
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
        ///     Device mute led state
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
        ///     Device mute led state feedback
        /// </summary>
        public BoolFeedback DeviceMuteStatusLedStateFeedback { get; private set; }

        #endregion


        #region External Switch State (EXT_SWITCH_OUT_STATE)

        // external switch state
        private bool _externalSwitchState;

        /// <summary>
        ///     Gets the external switch state
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
        ///     External switch state feedback
        /// </summary>
        public BoolFeedback ExternalSwitchStateFeedback { get; private set; }

        #endregion


        #region Presets

        // Current preset
        private uint _currentPreset;

        /// <summary>
        ///     CurrentPreset property
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
        ///     Current Preset int feedback
        /// </summary>
        public IntFeedback CurrentPresetIntFeedback { get; private set; }

        /// <summary>
        ///     Gets the current preset
        /// </summary>
        public void GetCurrentPreset()
        {
            SendText("GET PRESET");
        }

        /// <summary>
        ///     Recalls preset
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
        ///     Gets the programmed named of the preset
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
        ///     LED Color Enum
        /// </summary>
        public enum ELedColor
        {
            /// <summary>
            ///     Led color red
            /// </summary>
            Red = 0,

            /// <summary>
            ///     Led color green
            /// </summary>
            Green = 1,

            /// <summary>
            ///     Led color blue
            /// </summary>
            Blue = 2,

            /// <summary>
            ///     Led color pink
            /// </summary>
            Pink = 3,

            /// <summary>
            ///     Led color purple
            /// </summary>
            Purple = 4,

            /// <summary>
            ///     Led color yellow
            /// </summary>
            Yellow = 5,

            /// <summary>
            ///     Led color orange
            /// </summary>
            Orange = 6,

            /// <summary>
            ///     Led color white
            /// </summary>
            White = 7,

            /// <summary>
            ///     Led color gold
            /// </summary>
            Gold = 8,

            /// <summary>
            ///     Led color yellow-green
            /// </summary>
            Yellowgreen = 9,

            /// <summary>
            ///     Led color turquoise
            /// </summary>
            Turquoise = 10,

            /// <summary>
            ///     Led color powder-blue
            /// </summary>
            Powderblue = 11,

            /// <summary>
            ///     Led color cyan
            /// </summary>
            Cyan = 12,

            /// <summary>
            ///     Led color sky-blue
            /// </summary>
            Skyblue = 13,

            /// <summary>
            ///     Led color light-purple
            /// </summary>
            Lightpurple = 14,

            /// <summary>
            ///     Led color violet
            /// </summary>
            Violet = 15,

            /// <summary>
            ///     Led color orchid
            /// </summary>
            Orchid = 16
        }

        // led muted color number
        private uint _ledMutedColorNumber;

        /// <summary>
        ///     Led muted color
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
        ///     Led muted color number feedback
        /// </summary>
        public IntFeedback LedMutedColorNumberFeedback { get; private set; }

        // led muted color name
        private string _ledMutedColorName;

        /// <summary>
        ///     Led muted color name
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
        ///     Led muted color name feedback
        /// </summary>
        public StringFeedback LedMutedColorNameFeedback { get; private set; }


        // led unmuted color number
        private uint _ledUnmutedColorNumber;

        /// <summary>
        ///     Led unmuted color number
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
        ///     Led unmuted color feedback
        /// </summary>
        public IntFeedback LedUnmutedColorNumberFeedback { get; private set; }

        // led unmuted color name
        private string _ledUnmutedColorName;

        /// <summary>
        ///     LED unmuted color name
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
        ///     Led unmuted color name feedback
        /// </summary>
        public StringFeedback LedUnmutedColorNameFeedback { get; private set; }

        /// <summary>
        ///     Sets the device led color when muted
        /// </summary>
        /// <param name="value"></param>
        public void SetDeviceLedColorMuted(uint value)
        {
            ELedColor color = (ELedColor)Enum.Parse(typeof(ELedColor), value.ToString(), true);
            bool defined = Enum.IsDefined(typeof(ELedColor), color);
            Debug.Console(1, this, "SetDeviceLedColorMuted: color-{0}, defined-{1}", color.ToString(),
                defined.ToString());
            if (defined) SendText(string.Format("SET LED_COLOR_MUTED {0}", color.ToString().ToUpper()));
        }

        /// <summary>
        ///     Sets the device led color when unmuted
        /// </summary>
        /// <param name="value"></param>
        public void SetDeviceLedColorUnmuted(uint value)
        {
            ELedColor color = (ELedColor)Enum.Parse(typeof(ELedColor), value.ToString(), true);
            bool defined = Enum.IsDefined(typeof(ELedColor), color);
            Debug.Console(1, this, "SetDeviceLedColorUnmuted: color-{0}, defined-{1}", color.ToString(),
                defined.ToString());
            if (defined) SendText(string.Format("SET LED_COLOR_UNMUTED {0}", color.ToString().ToUpper()));
        }

        #endregion


        #region Device Info

        // device model field
        private string _deviceModel;

        /// <summary>
        ///     Device model property
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
        ///     Device model feedback
        /// </summary>
        public StringFeedback DeviceModelFeedback { get; private set; }


        // device serial number field
        private string _deviceSerialNumber;

        /// <summary>
        ///     Device serial number property
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
        ///     Device serial number feedback
        /// </summary>
        public StringFeedback DeviceSerialNumberFeedback { get; private set; }


        // device firmware version field
        private string _deviceFirmwareVersion;

        /// <summary>
        ///     Device firmware property
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
        ///     Device firmware version feedback
        /// </summary>
        public StringFeedback DeviceFirmwareVersionFeedback { get; private set; }

        // device error field
        private string _deviceError;

        /// <summary>
        ///     Device error property
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
        ///     Device error feedback
        /// </summary>
        public StringFeedback DeviceErrorFeedback { get; private set; }

        #endregion


        #region Overrides of EssentialsBridgeableDevice

        /// <summary>
        ///     Links the plugin device to the EISC bridge
        /// </summary>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
        /// <param name="bridge"></param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            MicrophoneDeviceJoinMap joinMap = new MicrophoneDeviceJoinMap(joinStart);

            // This adds the join map to the collection on the bridge
            if (bridge != null) bridge.AddJoinMap(Key, joinMap);

            Dictionary<string, JoinData> customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);
            if (customJoins != null) joinMap.SetCustomJoinData(customJoins);

            Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);

            // links to bridge
            trilist.SetString(joinMap.Name.JoinNumber, Name);
            _commsMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

            // device audio mute (DEVICE_AUDIO_MUTE)
            trilist.SetSigTrueAction(joinMap.DeviceAudioMuteOn.JoinNumber, SetDeviceAudioMuteOn);
            trilist.SetSigTrueAction(joinMap.DeviceAudioMuteOff.JoinNumber, SetDeviceAudioMuteOff);
            DeviceAudioMuteStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DeviceAudioMuteOn.JoinNumber]);
            DeviceAudioMuteStateFeedback.LinkComplementInputSig(
                trilist.BooleanInput[joinMap.DeviceAudioMuteOff.JoinNumber]);

            // device information feedback
            DeviceModelFeedback.LinkInputSig(trilist.StringInput[joinMap.Model.JoinNumber]);
            DeviceFirmwareVersionFeedback.LinkInputSig(trilist.StringInput[joinMap.DeviceFirmwareVersion.JoinNumber]);
            DeviceErrorFeedback.LinkInputSig(trilist.StringInput[joinMap.ErrorString.JoinNumber]);

            UpdateFeedbacks();

            trilist.OnlineStatusChange += (o, a) =>
            {
                if (!a.DeviceOnLine) return;
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
    }

    /// <summary>
    ///     Shure MXA Plugin device configuration object
    /// </summary>
    /// <example>
    ///     <code>
    ///  {
    /// 		"devices": [
    /// 			{
    /// 				"key": "shuremxa1-plugin",
    /// 				"name": "Shure MXA Plugin",
    /// 				"type": "shuremxa",
    /// 				"group": "pluginDevices",
    /// 				"properties": {
    /// 					"control": {	
    /// 						"tcpSshProperties": {
    /// 							"address": "",
    /// 							"port": 2202,
    /// 							"username": "",
    /// 							"password": "",
    /// 							"autoReconnect": true,
    /// 							"autoReconnectIntervalMs": 5000
    /// 						}
    /// 					},
    /// 					"pollTimeMs": 30000,
    /// 					"warningTimeoutMs": 180000,
    /// 					"errorTimeoutMs": 300000,
    /// 					"deviceId": 1,
    /// 					"presets": {
    /// 						"1": { "name": "CurrentPreset 1"	},
    /// 						"2": { "name": "CurrentPreset 2"	}
    /// 					}
    /// 				}
    /// 			}
    /// 		]
    ///  }
    ///  </code>
    /// </example>
    [ConfigSnippet(
        "{\"devices\":[{\"key\":\"shuremxa1-plugin\",\"name\":\"Shure MXA Plugin\",\"type\":\"shuremxa\",\"group\":\"pluginDevices\",\"properties\":{\"control\":{\"method\":\"tcpip\",\"tcpSshProperties\":{\"address\":\"\",\"port\":2202,\"username\":\"\",\"password\":\"\",\"autoReconnect\":true,\"autoReconnectIntervalMs\":5000}},\"pollTimeMs\":30000,\"warningTimeoutMs\":180000,\"errorTimeoutMs\":300000,\"deviceId\":1,\"linkWithDspObjectKey\":\"dsp01--fader11\",\"presets\":{\"1\":{\"name\":\"CurrentPreset 1\"},\"2\":{\"name\":\"CurrentPreset 2\"}}}}]}")]
    public class ShureMxaConfig
    {
        /// <summary>
        ///     Constructor
        /// </summary>
        public ShureMxaConfig()
        {
            Presets = new Dictionary<uint, ShureMxaPresetsConfig>();
        }

        /// <summary>
        ///     JSON control object
        /// </summary>
        [JsonProperty("control")]
        public EssentialsControlPropertiesConfig Control { get; set; }

        /// <summary>
        ///     DSP object to link mute state to
        /// </summary>
        [JsonProperty("dspObjectKey")]
        public string DspObjectKey { get; set; }

        /// <summary>
        ///     Device ID
        /// </summary>
        [JsonProperty("deviceId")]
        public long DeviceId { get; set; }

        /// <summary>
        ///     CurrentPreset name dictionary
        /// </summary>
        [JsonProperty("presets")]
        public Dictionary<uint, ShureMxaPresetsConfig> Presets { get; set; }
    }

    /// <summary>
    ///     CurrentPreset name dictionary
    /// </summary>
    /// <example>
    ///     <code>
    ///  "properties": {
    /// 		"presets": {
    /// 			"1": { "name": "CurrentPreset 1" },
    /// 			"2": { "name": "CurrentPreset 2" }
    /// 		}
    ///  }
    ///  </code>
    /// </example>
    public class ShureMxaPresetsConfig
    {
        /// <summary>
        ///     Serializes collection name property
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    /// <summary>
    ///     Plugin factory for devices that require communications using IBasicCommunications or custom communication methods
    /// </summary>
    public class ShureMxaFactory : EssentialsDeviceFactory<ShureMxaDevice>
    {
        /// <summary>
        ///     Device factory constructor
        /// </summary>
        public ShureMxaFactory()
        {
            TypeNames = new List<string> { "shuremxa" };
        }

        /// <summary>
        ///     Builds and returns an instance of ShureMxaDevice
        /// </summary>
        /// <param name="dc">device configuration</param>
        /// <returns>plugin device or null</returns>
        /// <seealso cref="PepperDash.Core.eControlMethod" />
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            try
            {
                Debug.Console(0, "[{0}] Factory attempting to create new device from type: {1}", dc.Key, dc.Type);

                // get the device properties configuration object and check for null 
                ShureMxaConfig propertiesConfig = dc.Properties.ToObject<ShureMxaConfig>();
                if (propertiesConfig == null)
                {
                    Debug.Console(0, "[{0}] Factory: failed to read properties config for {1}", dc.Key, dc.Name);
                    return null;
                }

                // build the device comms (for all other comms methods) & check for null			
                IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);
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