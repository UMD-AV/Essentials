using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.EthernetCommunication;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using Newtonsoft.Json;
using PepperDash.Core;
using ViscaCameraPlugin;

namespace RemoteCameraPlugin
{
    public class RemoteCamera : EssentialsBridgeableDevice
    {
        private ThreeSeriesTcpIpEthernetIntersystemCommunications CameraEisc;
        private BasicTriList InternalEisc;
        private BoolFeedback CameraOnline;
        private BoolFeedback InternalOnline;
        private string localCameraKey;
        private ViscaCameraDevice localCamera;
        private ViscaCameraBridgeJoinMap remoteCameraJoinMap = new ViscaCameraBridgeJoinMap(1);

        private uint internalJoinOffset;
        private uint endInternalJoin;

        public RemoteCamera(string key, string name, RemoteCameraPropertiesConfig props)
            : base(key, name)
        {
            CameraEisc = new ThreeSeriesTcpIpEthernetIntersystemCommunications(props.Control.IpIdInt,
                props.Control.TcpSshProperties.Address, Global.ControlSystem);
            CameraOnline = new BoolFeedback(() => GetOnlineState());
            CameraEisc.SigChange += new SigEventHandler(CameraEisc_SigChange);
            CameraEisc.OnlineStatusChange +=
                new Crestron.SimplSharpPro.OnlineStatusChangeEventHandler(CameraEisc_OnlineStatusChange);
            localCameraKey = props.LocalCameraKey;
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            ViscaCameraBridgeJoinMap joinMap = new ViscaCameraBridgeJoinMap(joinStart);
            internalJoinOffset = joinStart - 1;
            endInternalJoin = joinStart + 49;
            InternalEisc = trilist;
            InternalOnline = new BoolFeedback(() => trilist.IsOnline);
            trilist.SigChange += new SigEventHandler(InternalEisc_SigChange);
            trilist.OnlineStatusChange += new OnlineStatusChangeEventHandler(InternalEisc_OnlineStatusChange);

            //Send this device name to SIMPL
            InternalEisc.StringInput[joinMap.DeviceName.JoinNumber].StringValue = this.Name;
            //Send device model to SIMPL
            InternalEisc.StringInput[joinMap.DeviceModel.JoinNumber].StringValue = "RemoteCamera";

            //Send camera EISC online status to SIMPL on join 1
            CameraOnline.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
        }

        public override bool CustomActivate()
        {
            CameraEisc.Register();

            if (localCameraKey.Length > 0)
            {
                localCamera = DeviceManager.GetDeviceForKey(localCameraKey) as ViscaCameraDevice;
                if (localCamera != null)
                {
                    LinkLocalCameraToRemoteEisc(CameraEisc);
                    Debug.Console(0, this, "Remote Camera Eisc linked to camera: {0}", localCamera.Name);
                }
            }

            return true;
        }

        private bool GetOnlineState()
        {
            if (localCameraKey.Length > 0)
            {
                return CameraEisc.IsOnline && CameraEisc.BooleanOutput[51].BoolValue;
            }
            else
            {
                return CameraEisc.IsOnline;
            }
        }

        private void CameraEisc_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            Debug.Console(2, this, "Camera Eisc change IPID: {0} Type:{1} Number:{2}", currentDevice.ID, args.Sig.Type,
                args.Sig.Number);

            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                {
                    //Remote camera command
                    if (args.Sig.Number > 0 && args.Sig.Number <= 50 && localCamera != null)
                    {
                        if (args.Sig.Number == remoteCameraJoinMap.PanLeft.JoinNumber)
                        {
                            localCamera.Move(args.Sig.BoolValue,
                                ViscaCameraPlugin.ViscaCameraDevice.EDirection.PanLeft);
                        }
                        else if (args.Sig.Number == remoteCameraJoinMap.PanRight.JoinNumber)
                        {
                            localCamera.Move(args.Sig.BoolValue,
                                ViscaCameraPlugin.ViscaCameraDevice.EDirection.PanRight);
                        }
                        else if (args.Sig.Number == remoteCameraJoinMap.TiltUp.JoinNumber)
                        {
                            localCamera.Move(args.Sig.BoolValue, ViscaCameraPlugin.ViscaCameraDevice.EDirection.TiltUp);
                        }
                        else if (args.Sig.Number == remoteCameraJoinMap.TiltDown.JoinNumber)
                        {
                            localCamera.Move(args.Sig.BoolValue,
                                ViscaCameraPlugin.ViscaCameraDevice.EDirection.TiltDown);
                        }
                        else if (args.Sig.Number == remoteCameraJoinMap.ZoomIn.JoinNumber)
                        {
                            localCamera.Move(args.Sig.BoolValue, ViscaCameraPlugin.ViscaCameraDevice.EDirection.ZoomIn);
                        }
                        else if (args.Sig.Number == remoteCameraJoinMap.ZoomOut.JoinNumber)
                        {
                            localCamera.Move(args.Sig.BoolValue,
                                ViscaCameraPlugin.ViscaCameraDevice.EDirection.ZoomOut);
                        }
                        else if (args.Sig.Number == remoteCameraJoinMap.Home.JoinNumber)
                        {
                            if (args.Sig.BoolValue)
                            {
                                localCamera.RecallHomePosition();
                            }
                        }
                        else if (args.Sig.Number == remoteCameraJoinMap.AutoTrackingOn.JoinNumber)
                        {
                            if (args.Sig.BoolValue)
                            {
                                localCamera.SetAutoTrackingOn();
                            }
                        }
                        else if (args.Sig.Number == remoteCameraJoinMap.AutoTrackingOff.JoinNumber)
                        {
                            if (args.Sig.BoolValue)
                            {
                                localCamera.SetAutoTrackingOff();
                            }
                        }
                        else if (args.Sig.Number == remoteCameraJoinMap.AutoFocusOn.JoinNumber)
                        {
                            if (args.Sig.BoolValue)
                            {
                                localCamera.AutoFocusSet(true);
                            }
                        }
                        else if (args.Sig.Number == remoteCameraJoinMap.AutoFocusOff.JoinNumber)
                        {
                            if (args.Sig.BoolValue)
                            {
                                localCamera.AutoFocusSet(false);
                            }
                        }
                        else if (args.Sig.Number >= remoteCameraJoinMap.PresetRecall.JoinNumber
                                 && args.Sig.Number < remoteCameraJoinMap.PresetRecall.JoinNumber +
                                 remoteCameraJoinMap.PresetRecall.JoinSpan)
                        {
                            if (args.Sig.BoolValue)
                            {
                                localCamera.RecallPresetByNumber(args.Sig.Number - 10);
                            }
                        }
                        else if (args.Sig.Number == remoteCameraJoinMap.PowerOn.JoinNumber)
                        {
                            if (args.Sig.BoolValue)
                            {
                                localCamera.SetPowerOn();
                            }
                        }
                        else if (args.Sig.Number == remoteCameraJoinMap.PowerOff.JoinNumber)
                        {
                            if (args.Sig.BoolValue)
                            {
                                localCamera.SetPowerOff();
                            }
                        }
                    }

                    //Remote camera feedback - shift to offset joins on bridge
                    if (args.Sig.Number > 50 && args.Sig.Number <= 100 && InternalEisc != null)
                    {
                        if (args.Sig.Number == 51)
                        {
                            //Special case for online feedback updating
                            CameraOnline.FireUpdate();
                        }
                        else
                        {
                            InternalEisc.BooleanInput[args.Sig.Number + internalJoinOffset - 50].BoolValue =
                                args.Sig.BoolValue;
                        }
                    }

                    break;
                }
                case eSigType.UShort:
                {
                    //Remote camera command
                    if (args.Sig.Number > 0 && args.Sig.Number <= 50 && localCamera != null)
                    {
                        if (args.Sig.Number == remoteCameraJoinMap.PresetRecallByNumber.JoinNumber)
                        {
                            localCamera.RecallPresetByNumber(args.Sig.UShortValue);
                        }
                    }

                    //Remote camera feedback - shift to offset joins on bridge
                    if (args.Sig.Number > 50 && args.Sig.Number <= 100 && InternalEisc != null)
                    {
                        InternalEisc.UShortInput[args.Sig.Number + internalJoinOffset - 50].UShortValue =
                            args.Sig.UShortValue;
                    }

                    break;
                }
                case eSigType.String:
                {
                    //Remote camera command
                    if (args.Sig.Number > 0 && args.Sig.Number <= 50 && localCamera != null)
                    {
                    }

                    //Remote camera feedback - shift to offset joins on bridge
                    if (args.Sig.Number > 50 && args.Sig.Number <= 100 && InternalEisc != null)
                    {
                        if (args.Sig.Number == 51 || args.Sig.Number == 52)
                        {
                            //Ignore camera name on join 51 and device model on join 52
                        }
                        else
                        {
                            InternalEisc.StringInput[args.Sig.Number + internalJoinOffset - 50].StringValue =
                                args.Sig.StringValue;
                        }
                    }

                    break;
                }
            }
        }

        private void LinkLocalCameraToRemoteEisc(BasicTriList trilist)
        {
            //For remote eisc, use feedback at 51-100
            ViscaCameraBridgeJoinMap joinMapFeedback = new ViscaCameraBridgeJoinMap(51);

            localCamera.CommunicationMonitor.IsOnlineFeedback.LinkInputSig(
                trilist.BooleanInput[joinMapFeedback.IsOnline.JoinNumber]);

            // must null check so LinkToApi doesn't except when the device is TCP or UDP
            if (localCamera.SocketStatusFeedback != null)
                localCamera.SocketStatusFeedback.LinkInputSig(trilist.UShortInput[joinMapFeedback.Status.JoinNumber]);

            // power on
            localCamera.PowerFeedback.LinkInputSig(trilist.BooleanInput[joinMapFeedback.PowerOn.JoinNumber]);
            // power off
            localCamera.PowerFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMapFeedback.PowerOff.JoinNumber]);

            //Autotracking capable
            localCamera.AutoTrackingCapable.LinkInputSig(
                trilist.BooleanInput[joinMapFeedback.AutoTrackingCapable.JoinNumber]);

            // auto tracking on
            localCamera.AutoTrackingOnFeedback.LinkInputSig(
                trilist.BooleanInput[joinMapFeedback.AutoTrackingOn.JoinNumber]);

            // auto tracking off
            localCamera.AutoTrackingOnFeedback.LinkComplementInputSig(
                trilist.BooleanInput[joinMapFeedback.AutoTrackingOff.JoinNumber]);

            // focus
            localCamera.AutoFocusFeedback.LinkInputSig(trilist.BooleanInput[joinMapFeedback.AutoFocusOn.JoinNumber]);
            localCamera.AutoFocusFeedback.LinkComplementInputSig(
                trilist.BooleanInput[joinMapFeedback.AutoFocusOff.JoinNumber]);

            // preset analog feedback
            localCamera.ActivePresetFeedback.LinkInputSig(
                trilist.UShortInput[joinMapFeedback.PresetRecallByNumber.JoinNumber]);

            // preset count feedback
            localCamera.PresetCountFeedback.LinkInputSig(trilist.UShortInput[joinMapFeedback.PresetCount.JoinNumber]);

            foreach (KeyValuePair<uint, StringFeedback> item in localCamera.PresetNameFeedbacks)
            {
                // preset number
                ushort preset = (ushort)item.Key;

                // preset names
                uint nameJoin = preset + joinMapFeedback.PresetNames.JoinNumber - 1;
                StringFeedback nameFeedback = item.Value;
                nameFeedback.LinkInputSig(trilist.StringInput[nameJoin]);
            }

            //Link boolean preset feedback
            foreach (KeyValuePair<uint, BoolFeedback> item in localCamera.PresetActiveFeedbacks)
            {
                item.Value.LinkInputSig(trilist.BooleanInput[item.Key + joinMapFeedback.PresetRecall.JoinNumber - 1]);
            }
        }

        private void InternalEisc_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            Debug.Console(2, this, "Internal Eisc change IPID: {0} Type:{1} Number:{2}", currentDevice.ID,
                args.Sig.Type, args.Sig.Number);

            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                {
                    //For sending commands to remote camera - shift to joins 1-50 on remote EISC
                    if (args.Sig.Number > internalJoinOffset && args.Sig.Number <= endInternalJoin &&
                        CameraEisc != null)
                    {
                        CameraEisc.BooleanInput[args.Sig.Number - internalJoinOffset].BoolValue = args.Sig.BoolValue;
                    }

                    break;
                }
                case eSigType.UShort:
                {
                    //For sending commands to remote camera - shift to joins 1-50 on remote EISC
                    if (args.Sig.Number > internalJoinOffset && args.Sig.Number <= endInternalJoin &&
                        CameraEisc != null)
                    {
                        CameraEisc.UShortInput[args.Sig.Number - internalJoinOffset].UShortValue = args.Sig.UShortValue;
                    }

                    break;
                }
                case eSigType.String:
                {
                    //For sending commands to remote camera - shift to joins 1-50 on remote EISC
                    if (args.Sig.Number > internalJoinOffset && args.Sig.Number <= endInternalJoin &&
                        CameraEisc != null)
                    {
                        CameraEisc.StringInput[args.Sig.Number - internalJoinOffset].StringValue = args.Sig.StringValue;
                    }

                    break;
                }
            }
        }

        private void PushCameraOutputData()
        {
            for (uint x = 1; x <= 50; x++)
            {
                CameraEisc.BooleanInput[x].BoolValue = InternalEisc.BooleanOutput[x + internalJoinOffset].BoolValue;
                CameraEisc.UShortInput[x].UShortValue = InternalEisc.UShortOutput[x + internalJoinOffset].UShortValue;
                CameraEisc.StringInput[x].StringValue = InternalEisc.StringOutput[x + internalJoinOffset].StringValue;
            }

            localCamera.ActivePresetFeedback.FireUpdate();
            localCamera.AutoFocusFeedback.FireUpdate();
            localCamera.AutoTrackingOnFeedback.FireUpdate();
            localCamera.OnlineFeedback.FireUpdate();
            localCamera.PowerFeedback.FireUpdate();
            foreach (KeyValuePair<uint, BoolFeedback> p in localCamera.PresetActiveFeedbacks)
            {
                p.Value.FireUpdate();
            }

            foreach (KeyValuePair<uint, StringFeedback> p in localCamera.PresetNameFeedbacks)
            {
                p.Value.FireUpdate();
            }

            localCamera.PresetCountFeedback.FireUpdate();
            localCamera.PrivacyOnFeedback.FireUpdate();
        }

        private void PushInternalOutputData()
        {
            for (uint x = 1; x <= 50; x++)
            {
                InternalEisc.BooleanInput[x + internalJoinOffset].BoolValue =
                    CameraEisc.BooleanOutput[x + 50].BoolValue;
                InternalEisc.UShortInput[x + internalJoinOffset].UShortValue =
                    CameraEisc.UShortOutput[x + 50].UShortValue;

                //skip join 1 & 2 which is used for camera name and camera device to SIMPL
                if (x != 1 && x != 2)
                {
                    InternalEisc.StringInput[x + internalJoinOffset].StringValue =
                        CameraEisc.StringOutput[x + 50].StringValue;
                }
            }
        }

        private void ClearCameraOutputData()
        {
            for (uint x = 0; x <= 100; x++)
            {
                CameraEisc.BooleanInput[x].BoolValue = false;
            }
        }

        private void ClearInternalOutputData()
        {
            for (uint x = 1; x <= 50; x++)
            {
                InternalEisc.BooleanInput[x + internalJoinOffset].BoolValue = false;
            }
        }

        private void CameraEisc_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            CameraOnline.FireUpdate();
            if (args.DeviceOnLine)
            {
                PushCameraOutputData();
                PushInternalOutputData();
            }
            else
            {
                ClearInternalOutputData();
            }
        }

        private void InternalEisc_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            InternalOnline.FireUpdate();
            if (args.DeviceOnLine)
            {
                PushCameraOutputData();
                PushInternalOutputData();
            }
            else
            {
                ClearCameraOutputData();
            }
        }
    }

    public class RemoteCameraPropertiesConfig
    {
        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }

        [JsonProperty("localCameraKey")] public string LocalCameraKey { get; set; }
    }

    public class RemoteCameraFactory : EssentialsDeviceFactory<RemoteCamera>
    {
        public RemoteCameraFactory()
        {
            TypeNames = new List<string>() { "remotecamera" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Remote Camera Device");
            RemoteCameraPropertiesConfig props =
                Newtonsoft.Json.JsonConvert.DeserializeObject<RemoteCameraPropertiesConfig>(dc.Properties.ToString());

            return new RemoteCamera(dc.Key, dc.Name, props);
        }
    }
}