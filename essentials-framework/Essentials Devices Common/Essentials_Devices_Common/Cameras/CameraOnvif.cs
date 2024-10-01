using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Devices.Common.Codec;
using System.Text.RegularExpressions;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharp.Onvif;
using Newtonsoft.Json;
using PepperDash.Core;

namespace PepperDash.Essentials.Devices.Common.Cameras
{
    public class CameraOnvif : CameraBase, IHasCameraPtzControl, IHasCameraPresets, IHasPowerControlWithFeedback,
        IBridgeAdvanced, IHasCameraFocusControl, IHasAutoFocusMode
    {
        CameraOnvifPropertiesConfig PropertiesConfig;

        private OnvifDevice onvifDevice;
        private PTZControl ptzControl;


        public byte PanSpeedSlow = 0x10;
        public byte TiltSpeedSlow = 0x10;

        public byte PanSpeedFast = 0x13;
        public byte TiltSpeedFast = 0x13;

        //private bool IsMoving;
        private bool IsZooming;

        bool _powerIsOn;

        public bool PowerIsOn
        {
            get { return _powerIsOn; }
            private set
            {
                if (value != _powerIsOn)
                {
                    _powerIsOn = value;
                    PowerIsOnFeedback.FireUpdate();
                    CameraIsOffFeedback.FireUpdate();
                }
            }
        }

        const byte ZoomInCmd = 0x02;
        const byte ZoomOutCmd = 0x03;
        const byte ZoomStopCmd = 0x00;

        /// <summary>
        /// Used to determine when to move the camera at a faster speed if a direction is held
        /// </summary>
        CTimer SpeedTimer;
        // TODO: Implment speed timer for PTZ controls

        long FastSpeedHoldTimeMs = 2000;

        byte[] IncomingBuffer = new byte[] { };
        public BoolFeedback PowerIsOnFeedback { get; private set; }

        public CameraOnvif(string key, string name, IBasicCommunication comm, CameraOnvifPropertiesConfig props) :
            base(key, name)
        {
            Presets = props.Presets;

            PropertiesConfig = props;

            onvifDevice = new OnvifDevice(props.Control.TcpSshProperties.Address,
                props.Control.TcpSshProperties.Username, props.Control.TcpSshProperties.Password);
            ptzControl = onvifDevice.Profiles[0].PTZ;


            OutputPorts.Add(new RoutingOutputPort("videoOut", eRoutingSignalType.Video, eRoutingPortConnectionType.None,
                null, this, true));

            // Default to all capabilties
            Capabilities = eCameraCapabilities.Pan | eCameraCapabilities.Tilt | eCameraCapabilities.Zoom |
                           eCameraCapabilities.Focus;

            PowerIsOnFeedback = new BoolFeedback(() => { return PowerIsOn; });
            CameraIsOffFeedback = new BoolFeedback(() => { return !PowerIsOn; });
        }

        public override bool CustomActivate()
        {
            return true;
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            LinkCameraToApi(this, trilist, joinStart, joinMapKey, bridge);
        }


        void SendPowerQuery()
        {
        }

        public void PowerOn()
        {
        }

        public void PowerOff()
        {
        }

        public void PowerToggle()
        {
            if (PowerIsOnFeedback.BoolValue)
                PowerOff();
            else
                PowerOn();
        }

        public void PanLeft()
        {
            ptzControl.Left();
        }

        public void PanRight()
        {
            ptzControl.Right();
        }

        public void PanStop()
        {
            ptzControl.Stop();
        }

        public void TiltDown()
        {
            ptzControl.Down();
        }

        public void TiltUp()
        {
            ptzControl.Up();
        }

        public void TiltStop()
        {
            ptzControl.Stop();
        }

        public void ZoomIn()
        {
            ptzControl.ZoomIn();
            IsZooming = true;
        }

        public void ZoomOut()
        {
            ptzControl.ZoomOut();
            IsZooming = true;
        }

        public void ZoomStop()
        {
            ptzControl.Stop();
        }

        public void Stop()
        {
            ptzControl.Stop();
        }

        public void PositionHome()
        {
        }

        public void RecallPreset(int presetNumber)
        {
        }

        public void SavePreset(int presetNumber)
        {
        }

        #region IHasCameraPresets Members

        public event EventHandler<EventArgs> PresetsListHasChanged;

        protected void OnPresetsListHasChanged()
        {
            EventHandler<EventArgs> handler = PresetsListHasChanged;
            if (handler == null)
                return;

            handler.Invoke(this, EventArgs.Empty);
        }

        public List<CameraPreset> Presets { get; private set; }

        public void PresetSelect(int preset)
        {
            RecallPreset(preset);
        }

        public void PresetStore(int preset, string description)
        {
            SavePreset(preset);
        }

        #endregion

        #region IHasCameraFocusControl Members

        public void FocusNear()
        {
        }

        public void FocusFar()
        {
        }

        public void FocusStop()
        {
        }

        public void TriggerAutoFocus()
        {
        }

        #endregion

        #region IHasAutoFocus Members

        public void SetFocusModeAuto()
        {
        }

        public void SetFocusModeManual()
        {
        }

        public void ToggleFocusMode()
        {
        }

        #endregion

        void SendAutoFocusQuery()
        {
        }


        #region IHasCameraOff Members

        public BoolFeedback CameraIsOffFeedback { get; private set; }


        public void CameraOff()
        {
            PowerOff();
        }

        #endregion
    }

    public class CameraOnvifFactory : EssentialsDeviceFactory<CameraOnvif>
    {
        public CameraOnvifFactory()
        {
            TypeNames = new List<string>() { "cameraonvif" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new CameraOnvif Device");
            IBasicCommunication comm = CommFactory.CreateCommForDevice(dc);
            CameraOnvifPropertiesConfig props = Newtonsoft.Json.JsonConvert.DeserializeObject<Cameras.CameraOnvifPropertiesConfig>(
                dc.Properties.ToString());
            return new Cameras.CameraOnvif(dc.Key, dc.Name, comm, props);
        }
    }


    public class CameraOnvifPropertiesConfig : CameraPropertiesConfig
    {
    }
}