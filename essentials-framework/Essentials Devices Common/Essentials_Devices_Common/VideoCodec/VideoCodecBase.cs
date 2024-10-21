﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Core.XSigUtility;
using PepperDash.Core.XSigUtility.Tokens;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Devices;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core.Routing;
using PepperDash.Essentials.Devices.Common.Cameras;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces;
using PepperDash.Essentials.Core.Bridges.JoinMaps;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace PepperDash.Essentials.Devices.Common.VideoCodec
{
    public abstract class VideoCodecBase : ReconfigurableDevice, IRoutingInputsOutputs,
        IUsageTracking, IHasDialer, IHasContentSharing, ICodecAudio, iVideoCodecInfo, IBridgeAdvanced, IHasStandbyMode
    {
        private const int XSigEncoding = 28591;
        protected const int MaxParticipants = 100;
        private readonly byte[] _clearBytes = XSigHelpers.ClearOutputs();

        private IHasDirectory _directoryCodec;
        private BasicTriList _directoryTrilist;
        private VideoCodecControllerJoinMap _directoryJoinmap;

        protected string _timeFormatSpecifier;
        protected string _dateFormatSpecifier;


        protected VideoCodecBase(DeviceConfig config)
            : base(config)
        {
            StandbyIsOnFeedback = new BoolFeedback(StandbyIsOnFeedbackFunc);
            PrivacyModeIsOnFeedback = new BoolFeedback(PrivacyModeIsOnFeedbackFunc);
            VolumeLevelFeedback = new IntFeedback(VolumeLevelFeedbackFunc);
            MuteFeedback = new BoolFeedback(MuteFeedbackFunc);
            SharingSourceFeedback = new StringFeedback(SharingSourceFeedbackFunc);
            SharingContentIsOnFeedback = new BoolFeedback(SharingContentIsOnFeedbackFunc);

            // TODO [ ] hotfix/videocodecbase-max-meeting-xsig-set
            MeetingsToDisplayFeedback = new IntFeedback(() => MeetingsToDisplay);

            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

            ActiveCalls = new List<CodecActiveCallItem>();
        }

        public IBasicCommunication Communication { get; protected set; }

        /// <summary>
        /// An internal pseudo-source that is routable and connected to the osd input
        /// </summary>
        public DummyRoutingInputsDevice OsdSource { get; protected set; }

        public BoolFeedback StandbyIsOnFeedback { get; private set; }

        protected abstract Func<bool> PrivacyModeIsOnFeedbackFunc { get; }
        protected abstract Func<int> VolumeLevelFeedbackFunc { get; }
        protected abstract Func<bool> MuteFeedbackFunc { get; }
        protected abstract Func<bool> StandbyIsOnFeedbackFunc { get; }

        public List<CodecActiveCallItem> ActiveCalls { get; set; }

        public bool ShowSelfViewByDefault { get; protected set; }

        public bool SupportsCameraOff { get; protected set; }
        public bool SupportsCameraAutoMode { get; protected set; }

        public bool IsReady { get; protected set; }

        public virtual List<Feedback> Feedbacks
        {
            get
            {
                return new List<Feedback>
                {
                    PrivacyModeIsOnFeedback,
                    SharingSourceFeedback
                };
            }
        }

        protected abstract Func<string> SharingSourceFeedbackFunc { get; }
        protected abstract Func<bool> SharingContentIsOnFeedbackFunc { get; }

        #region ICodecAudio Members

        public abstract void PrivacyModeOn();
        public abstract void PrivacyModeOff();
        public abstract void PrivacyModeToggle();
        public BoolFeedback PrivacyModeIsOnFeedback { get; private set; }


        public BoolFeedback MuteFeedback { get; private set; }

        public abstract void MuteOff();

        public abstract void MuteOn();

        public abstract void SetVolume(ushort level);

        public IntFeedback VolumeLevelFeedback { get; private set; }

        public abstract void MuteToggle();

        public abstract void VolumeDown(bool pressRelease);


        public abstract void VolumeUp(bool pressRelease);

        #endregion

        #region IHasContentSharing Members

        public abstract void StartSharing();
        public abstract void StopSharing();

        public bool AutoShareContentWhileInCall { get; protected set; }

        public StringFeedback SharingSourceFeedback { get; private set; }
        public BoolFeedback SharingContentIsOnFeedback { get; private set; }

        #endregion

        #region IHasDialer Members

        /// <summary>
        /// Fires when the status of any active, dialing, or incoming call changes or is new
        /// </summary>
        public event EventHandler<CodecCallStatusItemChangeEventArgs> CallStatusChange;

        /// <summary>
        /// Returns true when any call is not in state Unknown, Disconnecting, Disconnected
        /// </summary>
        public bool IsInCall
        {
            get
            {
                bool value = ActiveCalls != null && ActiveCalls.Any(c => c.IsActiveCall);
                return value;
            }
        }

        public abstract void Dial(string number);
        public abstract void EndCall(CodecActiveCallItem call);
        public abstract void EndAllCalls();
        public abstract void AcceptCall(CodecActiveCallItem call);
        public abstract void RejectCall(CodecActiveCallItem call);
        public abstract void SendDtmf(string s);

        public virtual void SendDtmf(string s, CodecActiveCallItem call)
        {
        }

        #endregion

        #region IRoutingInputsOutputs Members

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }

        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

        #endregion

        #region IUsageTracking Members

        /// <summary>
        /// This object can be added by outside users of this class to provide usage tracking
        /// for various services
        /// </summary>
        public UsageTracking UsageTracker { get; set; }

        #endregion

        #region iVideoCodecInfo Members

        public VideoCodecInfo CodecInfo { get; protected set; }

        #endregion

        public event EventHandler<EventArgs> IsReadyChange;
        public abstract void Dial(Meeting meeting);

        public virtual void Dial(IInvitableContact contact)
        {
        }

        public abstract void ExecuteSwitch(object selector);

        /// <summary>
        /// Helper method to fire CallStatusChange event with old and new status
        /// </summary>
        protected void SetNewCallStatusAndFireCallStatusChange(eCodecCallStatus newStatus, CodecActiveCallItem call)
        {
            call.Status = newStatus;

            OnCallStatusChange(call);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="previousStatus"></param>
        /// <param name="newStatus"></param>
        /// <param name="item"></param>
        protected virtual void OnCallStatusChange(CodecActiveCallItem item)
        {
            EventHandler<CodecCallStatusItemChangeEventArgs> handler = CallStatusChange;
            if (handler != null)
            {
                handler(this, new CodecCallStatusItemChangeEventArgs(item));
            }

            PrivacyModeIsOnFeedback.FireUpdate();

            if (AutoShareContentWhileInCall)
            {
                StartSharing();
            }

            if (UsageTracker != null)
            {
                if (IsInCall && !UsageTracker.UsageTrackingStarted)
                {
                    UsageTracker.StartDeviceUsage();
                }
                else if (UsageTracker.UsageTrackingStarted && !IsInCall)
                {
                    UsageTracker.EndDeviceUsage();
                }
            }
        }

        /// <summary>
        /// Sets IsReady property and fires the event. Used for dependent classes to sync up their data.
        /// </summary>
        protected void SetIsReady()
        {
            CrestronInvoke.BeginInvoke((o) =>
            {
                try
                {
                    IsReady = true;
                    EventHandler<EventArgs> h = IsReadyChange;
                    if (h != null)
                    {
                        h(this, new EventArgs());
                    }
                }
                catch (Exception e)
                {
                    Debug.Console(2, this, "Error in SetIsReady() : {0}", e);
                }
            });
        }

        // **** DEBUGGING THINGS ****
        /// <summary>
        /// 
        /// </summary>
        public virtual void ListCalls()
        {
            Debug.Console(1, this, "Active Calls:");

            StringBuilder sb = new StringBuilder();
            foreach (CodecActiveCallItem c in ActiveCalls)
            {
                sb.AppendFormat("id: {0} number: {1} -- name: {2} status: {3} onHold: {4}\r\n", c.Id, c.Number, c.Name,
                    c.Status, c.IsOnHold);
            }

            Debug.Console(1, this, "\n{0}\n", sb.ToString());
        }

        public abstract void StandbyActivate();

        public abstract void StandbyDeactivate();

        #region Implementation of IBridgeAdvanced

        public abstract void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge);

        /// <summary>
        /// Use this method when using a plain VideoCodecControllerJoinMap
        /// </summary>
        /// <param name="codec"></param>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
        /// <param name="bridge"></param>
        protected void LinkVideoCodecToApi(VideoCodecBase codec, BasicTriList trilist, uint joinStart,
            string joinMapKey,
            EiscApiAdvanced bridge)
        {
            VideoCodecControllerJoinMap joinMap = new VideoCodecControllerJoinMap(joinStart);

            Dictionary<string, JoinData> customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

            if (customJoins != null)
            {
                joinMap.SetCustomJoinData(customJoins);
            }

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            LinkVideoCodecToApi(codec, trilist, joinMap);

            trilist.OnlineStatusChange += (device, args) =>
            {
                if (!args.DeviceOnLine) return;
            };
        }

        /// <summary>
        /// Use this method when you need to pass in a join map that extends VideoCodecControllerJoinMap
        /// </summary>
        /// <param name="codec"></param>
        /// <param name="trilist"></param>
        /// <param name="joinMap"></param>
        protected void LinkVideoCodecToApi(VideoCodecBase codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            Debug.Console(1, this, "Linking to Trilist {0}", trilist.ID.ToString("X"));

            LinkVideoCodecDtmfToApi(trilist, joinMap);

            LinkVideoCodecCallControlsToApi(trilist, joinMap);

            LinkVideoCodecContentSharingToApi(trilist, joinMap);

            LinkVideoCodecPrivacyToApi(trilist, joinMap);

            LinkVideoCodecVolumeToApi(trilist, joinMap);

            LinkVideoCodecInfoToApi(trilist, joinMap);

            // Register for this event to link any functions that require the codec to be ready first
            codec.IsReadyChange += (o, a) =>
            {
                if (codec is IHasCodecCameras)
                {
                    LinkVideoCodecCameraToApi(codec as IHasCodecCameras, trilist, joinMap);
                }
            };

            if (codec is ICommunicationMonitor)
            {
                LinkVideoCodecCommMonitorToApi(codec as ICommunicationMonitor, trilist, joinMap);
            }


            if (codec is IHasCodecSelfView)
            {
                LinkVideoCodecSelfviewToApi(codec as IHasCodecSelfView, trilist, joinMap);
            }

            if (codec is IHasCameraAutoMode)
            {
                trilist.SetBool(joinMap.CameraSupportsAutoMode.JoinNumber, SupportsCameraAutoMode);
                LinkVideoCodecCameraModeToApi(codec as IHasCameraAutoMode, trilist, joinMap);
            }

            if (codec is IHasCameraOff)
            {
                trilist.SetBool(joinMap.CameraSupportsOffMode.JoinNumber, SupportsCameraOff);
                LinkVideoCodecCameraOffToApi(codec as IHasCameraOff, trilist, joinMap);
            }

            if (codec is IHasCodecLayouts)
            {
                LinkVideoCodecCameraLayoutsToApi(codec as IHasCodecLayouts, trilist, joinMap);
            }


            if (codec is IHasSelfviewPosition)
            {
                LinkVideoCodecSelfviewPositionToApi(codec as IHasSelfviewPosition, trilist, joinMap);
            }

            if (codec is IHasDirectory)
            {
                LinkVideoCodecDirectoryToApi(codec as IHasDirectory, trilist, joinMap);
            }

            if (codec is IHasScheduleAwareness)
            {
                LinkVideoCodecScheduleToApi(codec as IHasScheduleAwareness, trilist, joinMap);
            }

            if (codec is IHasParticipants)
            {
                LinkVideoCodecParticipantsToApi(codec as IHasParticipants, trilist, joinMap);
            }

            if (codec is IHasFarEndContentStatus)
            {
                (codec as IHasFarEndContentStatus).ReceivingContent.LinkInputSig(
                    trilist.BooleanInput[joinMap.RecievingContent.JoinNumber]);
            }

            if (codec is IHasPhoneDialing)
            {
                LinkVideoCodecPhoneToApi(codec as IHasPhoneDialing, trilist, joinMap);
            }

            if (codec is IHasCallHistory)
            {
                LinkVideoCodecCallHistoryToApi(codec as IHasCallHistory, trilist, joinMap);
            }

            trilist.OnlineStatusChange += (device, args) =>
            {
                if (!args.DeviceOnLine) return;

                if (codec is IHasDirectory)
                {
                    (codec as IHasDirectory).SetCurrentDirectoryToRoot();
                }

                if (codec is IHasScheduleAwareness)
                {
                    (codec as IHasScheduleAwareness).GetSchedule();
                }

                if (codec is IHasParticipants)
                {
                    UpdateParticipantsXSig((codec as IHasParticipants).Participants.CurrentParticipants);
                }

                if (codec is IHasCameraAutoMode)
                {
                    trilist.SetBool(joinMap.CameraSupportsAutoMode.JoinNumber, SupportsCameraAutoMode);

                    (codec as IHasCameraAutoMode).CameraAutoModeIsOnFeedback.FireUpdate();
                }

                if (codec is IHasCodecSelfView)
                {
                    (codec as IHasCodecSelfView).SelfviewIsOnFeedback.FireUpdate();
                }

                if (codec is IHasCameraAutoMode)
                {
                    (codec as IHasCameraAutoMode).CameraAutoModeIsOnFeedback.FireUpdate();
                }

                if (codec is IHasCameraOff)
                {
                    (codec as IHasCameraOff).CameraIsOffFeedback.FireUpdate();
                }

                if (codec is IHasPhoneDialing)
                {
                    (codec as IHasPhoneDialing).PhoneOffHookFeedback.FireUpdate();
                }

                if (codec is IHasCallHistory)
                {
                    UpdateCallHistory((codec as IHasCallHistory), trilist, joinMap);
                }

                SharingContentIsOnFeedback.FireUpdate();

                trilist.SetBool(joinMap.HookState.JoinNumber, IsInCall);
            };
        }

        private void LinkVideoCodecInfoToApi(BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetBool(joinMap.MultiSiteOptionIsEnabled.JoinNumber, this.CodecInfo.MultiSiteOptionIsEnabled);
            trilist.SetBool(joinMap.AutoAnswerEnabled.JoinNumber, this.CodecInfo.AutoAnswerEnabled);
            trilist.SetString(joinMap.DeviceIpAddresss.JoinNumber, this.CodecInfo.IpAddress);
            trilist.SetString(joinMap.SipPhoneNumber.JoinNumber, this.CodecInfo.SipPhoneNumber);
            trilist.SetString(joinMap.E164Alias.JoinNumber, this.CodecInfo.E164Alias);
            trilist.SetString(joinMap.H323Id.JoinNumber, this.CodecInfo.H323Id);
            trilist.SetString(joinMap.SipUri.JoinNumber, this.CodecInfo.SipUri);

            trilist.OnlineStatusChange += (o, a) =>
            {
                if (a.DeviceOnLine)
                {
                    trilist.SetBool(joinMap.MultiSiteOptionIsEnabled.JoinNumber,
                        this.CodecInfo.MultiSiteOptionIsEnabled);
                    trilist.SetBool(joinMap.AutoAnswerEnabled.JoinNumber, this.CodecInfo.AutoAnswerEnabled);
                    trilist.SetString(joinMap.DeviceIpAddresss.JoinNumber, this.CodecInfo.IpAddress);
                    trilist.SetString(joinMap.SipPhoneNumber.JoinNumber, this.CodecInfo.SipPhoneNumber);
                    trilist.SetString(joinMap.E164Alias.JoinNumber, this.CodecInfo.E164Alias);
                    trilist.SetString(joinMap.H323Id.JoinNumber, this.CodecInfo.H323Id);
                    trilist.SetString(joinMap.SipUri.JoinNumber, this.CodecInfo.SipUri);
                }
            };
        }

        private void LinkVideoCodecPhoneToApi(IHasPhoneDialing codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            codec.PhoneOffHookFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PhoneHookState.JoinNumber]);

            trilist.SetSigFalseAction(joinMap.DialPhone.JoinNumber,
                () => codec.DialPhoneCall(trilist.StringOutput[joinMap.PhoneDialString.JoinNumber].StringValue));

            trilist.SetSigFalseAction(joinMap.HangUpPhone.JoinNumber, codec.EndPhoneCall);
        }

        private void LinkVideoCodecSelfviewPositionToApi(IHasSelfviewPosition codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.SelfviewPosition.JoinNumber, codec.SelfviewPipPositionToggle);

            codec.SelfviewPipPositionFeedback.LinkInputSig(trilist.StringInput[joinMap.SelfviewPositionFb.JoinNumber]);
        }

        private void LinkVideoCodecCameraOffToApi(IHasCameraOff codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.CameraModeOff.JoinNumber, codec.CameraOff);

            codec.CameraIsOffFeedback.OutputChange += (o, a) =>
            {
                if (a.BoolValue)
                {
                    trilist.SetBool(joinMap.CameraModeOff.JoinNumber, true);
                    trilist.SetBool(joinMap.CameraModeManual.JoinNumber, false);
                    trilist.SetBool(joinMap.CameraModeAuto.JoinNumber, false);
                    return;
                }

                trilist.SetBool(joinMap.CameraModeOff.JoinNumber, false);

                IHasCameraAutoMode autoCodec = codec as IHasCameraAutoMode;

                if (autoCodec == null) return;

                trilist.SetBool(joinMap.CameraModeAuto.JoinNumber, autoCodec.CameraAutoModeIsOnFeedback.BoolValue);
                trilist.SetBool(joinMap.CameraModeManual.JoinNumber, !autoCodec.CameraAutoModeIsOnFeedback.BoolValue);
            };

            if (codec.CameraIsOffFeedback.BoolValue)
            {
                trilist.SetBool(joinMap.CameraModeOff.JoinNumber, true);
                trilist.SetBool(joinMap.CameraModeManual.JoinNumber, false);
                trilist.SetBool(joinMap.CameraModeAuto.JoinNumber, false);
                return;
            }

            trilist.SetBool(joinMap.CameraModeOff.JoinNumber, false);

            IHasCameraAutoMode autoModeCodec = codec as IHasCameraAutoMode;

            if (autoModeCodec == null) return;

            trilist.SetBool(joinMap.CameraModeAuto.JoinNumber, autoModeCodec.CameraAutoModeIsOnFeedback.BoolValue);
            trilist.SetBool(joinMap.CameraModeManual.JoinNumber, !autoModeCodec.CameraAutoModeIsOnFeedback.BoolValue);
        }

        private void LinkVideoCodecVolumeToApi(BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.VolumeMuteOn.JoinNumber]);
            MuteFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.VolumeMuteOff.JoinNumber]);

            trilist.SetSigFalseAction(joinMap.VolumeMuteOn.JoinNumber, MuteOn);
            trilist.SetSigFalseAction(joinMap.VolumeMuteOff.JoinNumber, MuteOff);
            trilist.SetSigFalseAction(joinMap.VolumeMuteToggle.JoinNumber, MuteToggle);

            VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[joinMap.VolumeLevel.JoinNumber]);

            trilist.SetBoolSigAction(joinMap.VolumeUp.JoinNumber, VolumeUp);
            trilist.SetBoolSigAction(joinMap.VolumeDown.JoinNumber, VolumeDown);

            trilist.SetUShortSigAction(joinMap.VolumeLevel.JoinNumber, SetVolume);
        }

        private void LinkVideoCodecPrivacyToApi(BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            PrivacyModeIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.MicMuteOn.JoinNumber]);
            PrivacyModeIsOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.MicMuteOff.JoinNumber]);

            trilist.SetSigFalseAction(joinMap.MicMuteOn.JoinNumber, PrivacyModeOn);
            trilist.SetSigFalseAction(joinMap.MicMuteOff.JoinNumber, PrivacyModeOff);
            trilist.SetSigFalseAction(joinMap.MicMuteToggle.JoinNumber, PrivacyModeToggle);
        }

        private void LinkVideoCodecCommMonitorToApi(ICommunicationMonitor codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            codec.CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
        }

        private void LinkVideoCodecParticipantsToApi(IHasParticipants codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            // make sure to update the values when the EISC comes online
            trilist.OnlineStatusChange += (sender, args) =>
            {
                if (sender.IsOnline)
                {
                    UpdateParticipantsXSig(codec, trilist, joinMap);
                }
            };

            SetParticipantActions(trilist, joinMap);

            codec.Participants.ParticipantAdded += (sender, args) =>
            {
                UpdateParticipantsXSig(codec, trilist, joinMap);
            };

            codec.Participants.ParticipantRemoved += (sender, args) =>
            {
                UpdateParticipantsXSig(codec, trilist, joinMap);
            };

            codec.Participants.ParticipantUpdated += (sender, args) =>
            {
                UpdateParticipantXSig(args.Index, args.Participant, trilist, joinMap);
            };

            // set actions and update the values when the list changes
            codec.Participants.ParticipantsListHasChanged += (sender, args) =>
            {
                UpdateParticipantsXSig(codec, trilist, joinMap);
            };

            trilist.OnlineStatusChange += (device, args) =>
            {
                if (!args.DeviceOnLine) return;

                // TODO [ ] Issue #868
                trilist.SetString(joinMap.CurrentParticipants.JoinNumber, "\xFC");
                UpdateParticipantsXSig(codec, trilist, joinMap);
            };
        }

        private void RefreshXSigs(VideoCodecBase codec, BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetString(joinMap.CurrentCallData.JoinNumber, UpdateCallStatusXSig());
            if (codec is IHasParticipants)
            {
                UpdateParticipantsXSig(codec as IHasParticipants, trilist, joinMap);
            }
        }

        private void UpdateParticipantXSig(int index, Participant participant, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            string participantXSig;

            participantXSig = UpdateParticipantXSig(index, participant);
            trilist.SetString(joinMap.CurrentParticipants.JoinNumber, participantXSig);
        }

        private void UpdateParticipantsXSig(IHasParticipants codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            string participantsXSig;

            if (codec.Participants.CurrentParticipants.Count == 0)
            {
                participantsXSig = Encoding.GetEncoding(XSigEncoding).GetString(_clearBytes, 0, _clearBytes.Length);
                trilist.SetString(joinMap.CurrentParticipants.JoinNumber, participantsXSig);
                trilist.SetUshort(joinMap.ParticipantCount.JoinNumber,
                    (ushort)codec.Participants.CurrentParticipants.Count);
                return;
            }

            participantsXSig = UpdateParticipantsXSig(codec.Participants.CurrentParticipants);
            trilist.SetString(joinMap.CurrentParticipants.JoinNumber, participantsXSig);
            trilist.SetUshort(joinMap.ParticipantCount.JoinNumber,
                (ushort)codec.Participants.CurrentParticipants.Count);
        }

        /// <summary>
        /// Sets the actions for each participant in the list
        /// </summary>
        private void SetParticipantActions(BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            IHasParticipantAudioMute audioMuteCodec = this as IHasParticipantAudioMute;
            IHasParticipantVideoMute videoMuteCodec = this as IHasParticipantVideoMute;
            IHasParticipantPinUnpin pinCodec = this as IHasParticipantPinUnpin;
            IHasWaitingRoom waitingRoomCodec = this as IHasWaitingRoom;

            for (ushort i = 0; i < 100; i++)
            {
                ushort index = i;
                if (audioMuteCodec != null)
                {
                    trilist.SetSigTrueAction(joinMap.ParticipantAudioMuteToggleStart.JoinNumber + index,
                        () => audioMuteCodec.ToggleAudioForParticipantIndex(index));
                }

                if (videoMuteCodec != null)
                {
                    trilist.SetSigTrueAction(joinMap.ParticipantVideoMuteToggleStart.JoinNumber + index,
                        () => audioMuteCodec.ToggleVideoForParticipantIndex(index));
                }

                if (pinCodec != null)
                {
                    trilist.SetSigTrueAction(joinMap.ParticipantPinToggleStart.JoinNumber + index,
                        () => pinCodec.ToggleParticipantPinStateIndex(index, pinCodec.ScreenIndexToPinUserTo));
                }

                if (waitingRoomCodec != null)
                {
                    trilist.SetSigTrueAction(joinMap.AdmitParticipantFromWaitingRoomStart.JoinNumber + index,
                        () => waitingRoomCodec.AdmitParticipantFromWaitingRoomIndex(index));
                }
            }
        }

        private string UpdateParticipantXSig(int index, Participant participant)
        {
            const int maxParticipants = MaxParticipants;
            const int maxDigitals = 9;
            const int maxAnalogs = 1;
            const int maxSerials = 1;
            int digitalIndex = (maxAnalogs + maxSerials) * maxParticipants + index * maxDigitals;
            int analogIndex = maxSerials * maxParticipants + index * maxAnalogs;
            int serialIndex = index * maxSerials;

            if (index >= 0 && index < maxParticipants)
            {
                XSigToken[] tokenArray = new XSigToken[11];

                //digitals
                tokenArray[2] = new XSigDigitalToken(digitalIndex + 1, participant.AudioMuteFb);
                tokenArray[3] = new XSigDigitalToken(digitalIndex + 2, participant.VideoMuteFb);
                tokenArray[4] = new XSigDigitalToken(digitalIndex + 3, participant.CanMuteVideo);
                tokenArray[5] = new XSigDigitalToken(digitalIndex + 4, participant.CanUnmuteVideo);
                tokenArray[6] = new XSigDigitalToken(digitalIndex + 5, participant.IsHost || participant.IsCohost);
                tokenArray[7] = new XSigDigitalToken(digitalIndex + 6, participant.HandIsRaisedFb);
                tokenArray[8] = new XSigDigitalToken(digitalIndex + 7, participant.IsPinnedFb);
                tokenArray[9] = new XSigDigitalToken(digitalIndex + 8, participant.AudioConnected);
                tokenArray[10] = new XSigDigitalToken(digitalIndex + 9, participant.IsInWaitingRoom);

                //serials
                tokenArray[0] = new XSigSerialToken(serialIndex + 1, participant.Name);

                //analogs
                tokenArray[1] = new XSigAnalogToken(analogIndex + 1, (ushort)participant.ScreenIndexIsPinnedToFb);

                string returnString = GetXSigString(tokenArray);
                return returnString;
            }
            else
            {
                return "";
            }
        }

        private string UpdateParticipantsXSig(List<Participant> currentParticipants)
        {
            const int maxParticipants = MaxParticipants;
            const int maxDigitals = 9;
            const int maxAnalogs = 1;
            const int maxSerials = 1;
            const int offset = maxDigitals + maxAnalogs + maxSerials; // 11
            int digitalIndex = (maxAnalogs + maxSerials) * maxParticipants;
            int analogIndex = maxSerials * maxParticipants;
            int serialIndex = 0;
            int arrayIndex = 0;

            XSigToken[] tokenArray = new XSigToken[maxParticipants * offset];

            foreach (Participant participant in currentParticipants)
            {
                if (arrayIndex >= maxParticipants * offset) break;

                //digitals
                tokenArray[digitalIndex] = new XSigDigitalToken(digitalIndex + 1, participant.AudioMuteFb);
                tokenArray[digitalIndex + 1] = new XSigDigitalToken(digitalIndex + 2, participant.VideoMuteFb);
                tokenArray[digitalIndex + 2] = new XSigDigitalToken(digitalIndex + 3, participant.CanMuteVideo);
                tokenArray[digitalIndex + 3] = new XSigDigitalToken(digitalIndex + 4, participant.CanUnmuteVideo);
                tokenArray[digitalIndex + 4] =
                    new XSigDigitalToken(digitalIndex + 5, participant.IsHost || participant.IsCohost);
                tokenArray[digitalIndex + 5] = new XSigDigitalToken(digitalIndex + 6, participant.HandIsRaisedFb);
                tokenArray[digitalIndex + 6] = new XSigDigitalToken(digitalIndex + 7, participant.IsPinnedFb);
                tokenArray[digitalIndex + 7] = new XSigDigitalToken(digitalIndex + 8, participant.AudioConnected);
                tokenArray[digitalIndex + 8] = new XSigDigitalToken(digitalIndex + 9, participant.IsInWaitingRoom);

                //serials
                tokenArray[serialIndex] = new XSigSerialToken(serialIndex + 1, participant.Name);

                //analogs
                tokenArray[analogIndex] =
                    new XSigAnalogToken(analogIndex + 1, (ushort)participant.ScreenIndexIsPinnedToFb);

                digitalIndex += maxDigitals;
                analogIndex += maxAnalogs;
                serialIndex += maxSerials;
                arrayIndex += offset;
            }

            while (arrayIndex < maxParticipants * offset)
            {
                //digitals
                tokenArray[digitalIndex] = new XSigDigitalToken(digitalIndex + 1, false);
                tokenArray[digitalIndex + 1] = new XSigDigitalToken(digitalIndex + 2, false);
                tokenArray[digitalIndex + 2] = new XSigDigitalToken(digitalIndex + 3, false);
                tokenArray[digitalIndex + 3] = new XSigDigitalToken(digitalIndex + 4, false);
                tokenArray[digitalIndex + 4] = new XSigDigitalToken(digitalIndex + 5, false);
                tokenArray[digitalIndex + 5] = new XSigDigitalToken(digitalIndex + 6, false);
                tokenArray[digitalIndex + 6] = new XSigDigitalToken(digitalIndex + 7, false);
                tokenArray[digitalIndex + 7] = new XSigDigitalToken(digitalIndex + 8, false);
                tokenArray[digitalIndex + 8] = new XSigDigitalToken(digitalIndex + 9, false);

                //serials
                tokenArray[serialIndex] = new XSigSerialToken(serialIndex + 1, string.Empty);

                //analogs
                tokenArray[analogIndex] = new XSigAnalogToken(analogIndex + 1, 0);

                digitalIndex += maxDigitals;
                analogIndex += maxAnalogs;
                serialIndex += maxSerials;
                arrayIndex += offset;
            }

            string returnString = GetXSigString(tokenArray);

            return returnString;
        }

        private void LinkVideoCodecContentSharingToApi(BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            SharingContentIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.SourceShareStart.JoinNumber]);
            SharingContentIsOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.SourceShareEnd.JoinNumber]);

            SharingSourceFeedback.LinkInputSig(trilist.StringInput[joinMap.CurrentSource.JoinNumber]);

            trilist.SetSigFalseAction(joinMap.SourceShareStart.JoinNumber, StartSharing);
            trilist.SetSigFalseAction(joinMap.SourceShareEnd.JoinNumber, StopSharing);

            trilist.SetBoolSigAction(joinMap.SourceShareAutoStart.JoinNumber, b => AutoShareContentWhileInCall = b);
        }

        private List<Meeting> _currentMeetings = new List<Meeting>();

        private void LinkVideoCodecScheduleToApi(IHasScheduleAwareness codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.UpdateMeetings.JoinNumber, codec.GetSchedule);

            trilist.SetUShortSigAction(joinMap.MinutesBeforeMeetingStart.JoinNumber,
                (i) => { codec.CodecSchedule.MeetingWarningMinutes = i; });

            for (uint i = 0; i < joinMap.DialMeetingStart.JoinSpan; i++)
            {
                Debug.Console(1, this, "Setting action to Dial Meeting {0} to digital join {1}", i + 1,
                    joinMap.DialMeetingStart.JoinNumber + i);
                uint joinNumber = joinMap.DialMeetingStart.JoinNumber + i;
                uint mtg = i + 1;
                int index = (int)i;

                trilist.SetSigFalseAction(joinNumber, () =>
                {
                    Debug.Console(1, this,
                        "Meeting {0} Selected (EISC dig-o{1}) > _currentMeetings[{2}].Id: {3}, Title: {4}",
                        mtg, joinMap.DialMeetingStart.JoinNumber + i, index, _currentMeetings[index].Id,
                        _currentMeetings[index].Title);
                    if (_currentMeetings[index] != null)
                        Dial(_currentMeetings[index]);
                });
            }

            codec.CodecSchedule.MeetingsListHasChanged += (sender, args) => UpdateMeetingsList(codec, trilist, joinMap);
            codec.CodecSchedule.MeetingEventChange += (sender, args) =>
            {
                if (args.ChangeType == eMeetingEventChangeType.MeetingStartWarning)
                {
                    UpdateMeetingsList(codec, trilist, joinMap);
                }
            };

            trilist.SetUShortSigAction(joinMap.MeetingsToDisplay.JoinNumber, m => MeetingsToDisplay = m);
            MeetingsToDisplayFeedback.LinkInputSig(trilist.UShortInput[joinMap.MeetingsToDisplay.JoinNumber]);

            trilist.OnlineStatusChange += (device, args) =>
            {
                if (!args.DeviceOnLine) return;

                // TODO [ ] Issue #868
                trilist.SetString(joinMap.Schedule.JoinNumber, "\xFC");
                UpdateMeetingsList(codec, trilist, joinMap);
                // TODO [ ] hotfix/videocodecbase-max-meeting-xsig-set
                MeetingsToDisplayFeedback.LinkInputSig(trilist.UShortInput[joinMap.MeetingsToDisplay.JoinNumber]);
            };
        }

        private void UpdateMeetingsList(IHasScheduleAwareness codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            DateTime currentTime = DateTime.Now;

            _currentMeetings = codec.CodecSchedule.Meetings
                .Where(m => m.StartTime >= currentTime || m.EndTime >= currentTime).ToList();

            if (_currentMeetings.Count == 0)
            {
                byte[] emptyXSigByteArray = XSigHelpers.ClearOutputs();
                string emptyXSigString = Encoding.GetEncoding(XSigEncoding)
                    .GetString(emptyXSigByteArray, 0, emptyXSigByteArray.Length);

                trilist.SetString(joinMap.Schedule.JoinNumber, emptyXSigString);
                return;
            }

            string meetingsData = UpdateMeetingsListXSig(_currentMeetings);
            trilist.SetString(joinMap.Schedule.JoinNumber, meetingsData);
            trilist.SetUshort(joinMap.MeetingCount.JoinNumber, (ushort)_currentMeetings.Count);

            trilist.OnlineStatusChange += (device, args) =>
            {
                if (!args.DeviceOnLine) return;

                // TODO [ ] Issue #868
                trilist.SetString(joinMap.Schedule.JoinNumber, "\xFC");
                UpdateMeetingsListXSig(_currentMeetings);
            };
        }


        // TODO [ ] hotfix/videocodecbase-max-meeting-xsig-set
        private int _meetingsToDisplay = 3;

        // TODO [ ] hotfix/videocodecbase-max-meeting-xsig-set
        protected int MeetingsToDisplay
        {
            get { return _meetingsToDisplay; }
            set
            {
                _meetingsToDisplay = (ushort)(value == 0 ? 3 : value);
                MeetingsToDisplayFeedback.FireUpdate();
            }
        }

        // TODO [ ] hotfix/videocodecbase-max-meeting-xsig-set
        public IntFeedback MeetingsToDisplayFeedback { get; set; }

        private string UpdateMeetingsListXSig(List<Meeting> meetings)
        {
            // TODO [ ] hotfix/videocodecbase-max-meeting-xsig-set
            //const int _meetingsToDisplay = 3;            
            const int maxDigitals = 2;
            const int maxAnalogs = 7;
            const int offset = maxDigitals + maxAnalogs;
            int digitalIndex = maxAnalogs * _meetingsToDisplay;
            int analogIndex = 0;
            int arrayIndex = 0;

            XSigToken[] tokenArray = new XSigToken[_meetingsToDisplay * offset];
            /*
             * Digitals
             * IsJoinable - 1
             * IsDialable - 2
             *
             * Serials
             * Organizer - 1
             * Title - 2
             * Start Date - 3
             * Start Time - 4
             * End Date - 5
             * End Time - 6
             * Id - 7
             */


            foreach (Meeting meeting in meetings)
            {
                DateTime currentTime = DateTime.Now;

                if (meeting.StartTime < currentTime && meeting.EndTime < currentTime) continue;

                if (arrayIndex >= _meetingsToDisplay * offset)
                {
                    Debug.Console(2, this, "Max Meetings reached");
                    break;
                }

                //digitals
                tokenArray[digitalIndex] = new XSigDigitalToken(digitalIndex + 1, meeting.Joinable);
                tokenArray[digitalIndex + 1] = new XSigDigitalToken(digitalIndex + 2, meeting.Id != "0");

                //serials
                tokenArray[analogIndex] = new XSigSerialToken(analogIndex + 1, meeting.Organizer);
                tokenArray[analogIndex + 1] = new XSigSerialToken(analogIndex + 2, meeting.Title);
                tokenArray[analogIndex + 2] =
                    new XSigSerialToken(analogIndex + 3, meeting.StartTime.ToString("t", Global.Culture));
                tokenArray[analogIndex + 3] =
                    new XSigSerialToken(analogIndex + 4, meeting.StartTime.ToString("t", Global.Culture));
                tokenArray[analogIndex + 4] =
                    new XSigSerialToken(analogIndex + 5, meeting.EndTime.ToString("t", Global.Culture));
                tokenArray[analogIndex + 5] =
                    new XSigSerialToken(analogIndex + 6, meeting.EndTime.ToString("t", Global.Culture));
                tokenArray[analogIndex + 6] = new XSigSerialToken(analogIndex + 7, meeting.Id);

                digitalIndex += maxDigitals;
                arrayIndex += offset;
                analogIndex += maxAnalogs;
            }

            while (arrayIndex < _meetingsToDisplay * offset)
            {
                Debug.Console(2, this, "Clearing unused data. Meeting Index: {0} MaxMeetings * Offset: {1}",
                    arrayIndex, _meetingsToDisplay * offset);

                //digitals
                tokenArray[digitalIndex] = new XSigDigitalToken(digitalIndex + 1, false);
                tokenArray[digitalIndex + 1] = new XSigDigitalToken(digitalIndex + 2, false);

                //serials
                tokenArray[analogIndex] = new XSigSerialToken(analogIndex + 1, string.Empty);
                tokenArray[analogIndex + 1] = new XSigSerialToken(analogIndex + 2, string.Empty);
                tokenArray[analogIndex + 2] = new XSigSerialToken(analogIndex + 3, string.Empty);
                tokenArray[analogIndex + 3] = new XSigSerialToken(analogIndex + 4, string.Empty);
                tokenArray[analogIndex + 4] = new XSigSerialToken(analogIndex + 5, string.Empty);
                tokenArray[analogIndex + 5] = new XSigSerialToken(analogIndex + 6, string.Empty);
                tokenArray[analogIndex + 6] = new XSigSerialToken(analogIndex + 7, string.Empty);

                digitalIndex += maxDigitals;
                arrayIndex += offset;
                analogIndex += maxAnalogs;
            }

            return GetXSigString(tokenArray);
        }

        private void LinkVideoCodecDirectoryToApi(IHasDirectory codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            codec.CurrentDirectoryResultIsNotDirectoryRoot.LinkComplementInputSig(
                trilist.BooleanInput[joinMap.DirectoryIsRoot.JoinNumber]);

            trilist.SetStringSigAction(joinMap.DirectorySearchString.JoinNumber, codec.SearchDirectory);

            trilist.SetUShortSigAction(joinMap.DirectorySelectRow.JoinNumber,
                (i) => SelectDirectoryEntry(codec, i, trilist, joinMap));

            //Special Change for protected directory clear

            trilist.SetBoolSigAction(joinMap.DirectoryClearSelected.JoinNumber,
                (b) => SelectDirectoryEntry(_directoryCodec, 0, _directoryTrilist, _directoryJoinmap));

            // Report feedback for number of contact methods for selected contact

            trilist.SetSigFalseAction(joinMap.DirectoryRoot.JoinNumber, codec.SetCurrentDirectoryToRoot);

            trilist.SetSigFalseAction(joinMap.DirectoryFolderBack.JoinNumber, codec.GetDirectoryParentFolderContents);

            if (codec.DirectoryRoot != null)
            {
                trilist.SetUshort(joinMap.DirectoryRowCount.JoinNumber,
                    (ushort)codec.DirectoryRoot.CurrentDirectoryResults.Count);

                byte[] clearBytes = XSigHelpers.ClearOutputs();

                trilist.SetString(joinMap.DirectoryEntries.JoinNumber,
                    Encoding.GetEncoding(XSigEncoding).GetString(clearBytes, 0, clearBytes.Length));
                string directoryXSig = UpdateDirectoryXSig(codec.DirectoryRoot,
                    codec.CurrentDirectoryResultIsNotDirectoryRoot.BoolValue == false);

                Debug.Console(2, this, "Directory XSig Length: {0}", directoryXSig.Length);

                trilist.SetString(joinMap.DirectoryEntries.JoinNumber, directoryXSig);
            }

            codec.DirectoryResultReturned += (sender, args) =>
            {
                trilist.SetUshort(joinMap.DirectoryRowCount.JoinNumber,
                    (ushort)args.Directory.CurrentDirectoryResults.Count);

                byte[] clearBytes = XSigHelpers.ClearOutputs();

                trilist.SetString(joinMap.DirectoryEntries.JoinNumber,
                    Encoding.GetEncoding(XSigEncoding).GetString(clearBytes, 0, clearBytes.Length));
                string directoryXSig = UpdateDirectoryXSig(args.Directory,
                    codec.CurrentDirectoryResultIsNotDirectoryRoot.BoolValue == false);
                Debug.Console(2, this, "Directory XSig Length: {0}", directoryXSig.Length);

                trilist.SetString(joinMap.DirectoryEntries.JoinNumber, directoryXSig);
            };

            trilist.OnlineStatusChange += (device, args) =>
            {
                if (!args.DeviceOnLine) return;

                byte[] clearBytes = XSigHelpers.ClearOutputs();
                trilist.SetString(joinMap.DirectoryEntries.JoinNumber,
                    Encoding.GetEncoding(XSigEncoding).GetString(clearBytes, 0, clearBytes.Length));
                string directoryXSig = UpdateDirectoryXSig(codec.DirectoryRoot,
                    codec.CurrentDirectoryResultIsNotDirectoryRoot.BoolValue == false);
                trilist.SetString(joinMap.DirectoryEntries.JoinNumber, directoryXSig);
            };
        }

        private void SelectDirectoryEntry(IHasDirectory codec, ushort i, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            if (i > codec.CurrentDirectoryResult.CurrentDirectoryResults.Count) return;
            _selectedDirectoryItem = i == 0 ? null : codec.CurrentDirectoryResult.CurrentDirectoryResults[i - 1];
            trilist.SetUshort(joinMap.DirectorySelectRowFeedback.JoinNumber, i);

            if (_selectedDirectoryItem == null) trilist.SetBool(joinMap.DirectoryEntryIsContact.JoinNumber, false);


            if (_selectedDirectoryItem is DirectoryFolder)
            {
                codec.GetDirectoryFolderContents(_selectedDirectoryItem.FolderId);
                trilist.SetUshort(joinMap.SelectedContactMethodCount.JoinNumber, 0);
                trilist.SetString(joinMap.DirectorySelectedFolderName.JoinNumber, _selectedDirectoryItem.Name);
                trilist.SetString(joinMap.DirectoryEntrySelectedName.JoinNumber, string.Empty);
                trilist.ClearUShortSigAction(joinMap.SelectContactMethod.JoinNumber);
                trilist.ClearBoolSigAction(joinMap.DirectoryDialSelectedLine.JoinNumber);
                trilist.ClearBoolSigAction(joinMap.DirectoryDialSelectedContactMethod.JoinNumber);
                trilist.SetBool(joinMap.DirectoryEntryIsContact.JoinNumber, false);
                return;
            }

            // not a folder.  Clear this value
            trilist.SetString(joinMap.DirectorySelectedFolderName.JoinNumber, string.Empty);

            DirectoryContact selectedContact = _selectedDirectoryItem as DirectoryContact;

            if (selectedContact != null && selectedContact.ContactMethods.Count >= 1)
            {
                trilist.SetBool(joinMap.DirectoryEntryIsContact.JoinNumber, true);
            }

            trilist.SetString(joinMap.DirectoryEntrySelectedName.JoinNumber,
                selectedContact != null ? selectedContact.Name : string.Empty);

            // Allow auto dial of selected line.  Always dials first contact method
            if (!trilist.GetBool(joinMap.DirectoryDisableAutoDialSelectedLine.JoinNumber))
            {
                IInvitableContact invitableEntry = _selectedDirectoryItem as IInvitableContact;

                if (invitableEntry != null)
                {
                    Dial(invitableEntry);
                    return;
                }

                DirectoryContact entryToDial = _selectedDirectoryItem as DirectoryContact;

                trilist.SetString(joinMap.DirectoryEntrySelectedNumber.JoinNumber,
                    selectedContact != null ? selectedContact.ContactMethods[0].Number : string.Empty);

                if (entryToDial == null) return;

                Dial(entryToDial.ContactMethods[0].Number);
            }
            else
            {
                // If auto dial is disabled...
                DirectoryContact entryToDial = _selectedDirectoryItem as DirectoryContact;

                if (entryToDial == null)
                {
                    // Clear out values and actions from last selected item
                    trilist.SetUshort(joinMap.SelectedContactMethodCount.JoinNumber, 0);
                    trilist.SetString(joinMap.DirectoryEntrySelectedName.JoinNumber, string.Empty);
                    trilist.ClearUShortSigAction(joinMap.SelectContactMethod.JoinNumber);
                    trilist.ClearBoolSigAction(joinMap.DirectoryDialSelectedLine.JoinNumber);
                    trilist.ClearBoolSigAction(joinMap.DirectoryDialSelectedContactMethod.JoinNumber);
                    return;
                }

                trilist.SetUshort(joinMap.SelectedContactMethodCount.JoinNumber,
                    (ushort)entryToDial.ContactMethods.Count);

                // Update the action to dial the selected contact method
                trilist.SetUShortSigAction(joinMap.SelectContactMethod.JoinNumber, (u) =>
                {
                    if (u < 1 || u > entryToDial.ContactMethods.Count) return;

                    trilist.SetSigFalseAction(joinMap.DirectoryDialSelectedContactMethod.JoinNumber,
                        () => Dial(entryToDial.ContactMethods[u - 1].Number));
                });

                // Sets DirectoryDialSelectedLine join action to dial first contact method
                trilist.SetSigFalseAction(joinMap.DirectoryDialSelectedLine.JoinNumber,
                    () => Dial(entryToDial.ContactMethods[0].Number));

                byte[] clearBytes = XSigHelpers.ClearOutputs();

                trilist.SetString(joinMap.ContactMethods.JoinNumber,
                    Encoding.GetEncoding(XSigEncoding).GetString(clearBytes, 0, clearBytes.Length));
                string contactMethodsXSig = UpdateContactMethodsXSig(entryToDial);

                trilist.SetString(joinMap.ContactMethods.JoinNumber, contactMethodsXSig);
            }
        }

        /// <summary>
        /// Generates the XSig data representing the available contact methods for the selected DirectoryContact
        /// </summary>
        /// <param name="contact"></param>
        /// <returns></returns>
        private string UpdateContactMethodsXSig(DirectoryContact contact)
        {
            const int maxMethods = 10;
            const int maxStrings = 3;
            const int offset = maxStrings;
            int stringIndex = 0;
            int arrayIndex = 0;
            // Create a new token array and set the size to the number of methods times the total number of signals
            XSigToken[] tokenArray = new XSigToken[maxMethods * offset];

            Debug.Console(2, this, "Creating XSIG token array with size {0}", maxMethods * offset);

            // TODO: Add code to generate XSig data
            foreach (ContactMethod method in contact.ContactMethods)
            {
                if (arrayIndex >= maxMethods * offset)
                    break;

                //serials
                tokenArray[arrayIndex + 1] = new XSigSerialToken(stringIndex + 1, method.Number);
                tokenArray[arrayIndex + 2] = new XSigSerialToken(stringIndex + 2, method.ContactMethodId.ToString());
                tokenArray[arrayIndex + 3] = new XSigSerialToken(stringIndex + 3, method.Device.ToString());

                arrayIndex += offset;
                stringIndex += maxStrings;
            }

            while (arrayIndex < maxMethods)
            {
                tokenArray[arrayIndex + 1] = new XSigSerialToken(stringIndex + 1, string.Empty);
                tokenArray[arrayIndex + 2] = new XSigSerialToken(stringIndex + 2, string.Empty);
                tokenArray[arrayIndex + 3] = new XSigSerialToken(stringIndex + 3, string.Empty);

                arrayIndex += offset;
                stringIndex += maxStrings;
            }

            return GetXSigString(tokenArray);
        }

        private string UpdateDirectoryXSig(CodecDirectory directory, bool isRoot)
        {
            int xSigMaxIndex = 1023;
            XSigToken[] tokenArray = new XSigToken[directory.CurrentDirectoryResults.Count > xSigMaxIndex
                ? xSigMaxIndex
                : directory.CurrentDirectoryResults.Count];

            Debug.Console(2, this, "IsRoot: {0}, Directory Count: {1}, TokenArray.Length: {2}", isRoot,
                directory.CurrentDirectoryResults.Count, tokenArray.Length);

            IEnumerable<DirectoryItem> contacts = directory.CurrentDirectoryResults.Count > xSigMaxIndex
                ? directory.CurrentDirectoryResults.Take(xSigMaxIndex)
                : directory.CurrentDirectoryResults;

            int counterIndex = 1;
            foreach (DirectoryItem entry in contacts)
            {
                int arrayIndex = counterIndex - 1;
                int entryIndex = counterIndex;

                Debug.Console(2, this, "Entry{2:0000} Name: {0}, Folder ID: {1}", entry.Name, entry.FolderId,
                    entryIndex);

                if (entry is DirectoryFolder && entry.ParentFolderId == "root")
                {
                    tokenArray[arrayIndex] = new XSigSerialToken(entryIndex, string.Format("[+] {0}", entry.Name));

                    counterIndex++;
                    counterIndex++;

                    continue;
                }

                tokenArray[arrayIndex] = new XSigSerialToken(entryIndex, entry.Name);

                counterIndex++;
            }

            return GetXSigString(tokenArray);
        }

        private void LinkVideoCodecCallControlsToApi(BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.ManualDial.JoinNumber,
                () => Dial(trilist.StringOutput[joinMap.CurrentDialString.JoinNumber].StringValue));

            //End All calls
            trilist.SetSigFalseAction(joinMap.EndAllCalls.JoinNumber, EndAllCalls);

            //End a specific call, specified by index. Maximum 8 calls supported
            for (int i = 0; i < joinMap.EndCallStart.JoinSpan; i++)
            {
                int callIndex = i;

                trilist.SetSigFalseAction((uint)(joinMap.EndCallStart.JoinNumber + i), () =>
                {
                    if (callIndex < 0 || callIndex >= ActiveCalls.Count)
                    {
                        Debug.Console(2, this, "Cannot end call. No call found at index: {0}", callIndex);
                        return;
                    }

                    CodecActiveCallItem call = ActiveCalls[callIndex];
                    if (call != null)
                    {
                        EndCall(call);
                    }
                    else
                    {
                        Debug.Console(0, this, "[End Call] Unable to find call at index '{0}'", i);
                    }
                });
            }

            trilist.SetBool(joinMap.HookState.JoinNumber, IsInCall);

            CallStatusChange += (sender, args) =>
            {
                trilist.SetBool(joinMap.HookState.JoinNumber, IsInCall);

                Debug.Console(1, this, "Call Direction: {0}", args.CallItem.Direction);
                Debug.Console(1, this, "Call is incoming: {0}",
                    args.CallItem.Direction == eCodecCallDirection.Incoming);
                trilist.SetBool(joinMap.IncomingCall.JoinNumber,
                    args.CallItem.Direction == eCodecCallDirection.Incoming &&
                    args.CallItem.Status == eCodecCallStatus.Ringing);

                if (args.CallItem.Direction == eCodecCallDirection.Incoming)
                {
                    trilist.SetSigFalseAction(joinMap.IncomingAnswer.JoinNumber, () => AcceptCall(args.CallItem));
                    trilist.SetSigFalseAction(joinMap.IncomingReject.JoinNumber, () => RejectCall(args.CallItem));
                    trilist.SetString(joinMap.IncomingCallName.JoinNumber, args.CallItem.Name);
                    trilist.SetString(joinMap.IncomingCallNumber.JoinNumber, args.CallItem.Number);
                }
                else
                {
                    trilist.SetString(joinMap.IncomingCallName.JoinNumber, string.Empty);
                    trilist.SetString(joinMap.IncomingCallNumber.JoinNumber, string.Empty);
                }


                trilist.SetString(joinMap.CurrentCallData.JoinNumber, UpdateCallStatusXSig());

                trilist.SetUshort(joinMap.ConnectedCallCount.JoinNumber, (ushort)ActiveCalls.Count);
            };

            IJoinCalls joinCodec = this as IJoinCalls;
            if (joinCodec != null)
            {
                trilist.SetSigFalseAction(joinMap.JoinAllCalls.JoinNumber, () => joinCodec.JoinAllCalls());

                for (int i = 0; i < joinMap.JoinCallStart.JoinSpan; i++)
                {
                    trilist.SetSigFalseAction((uint)(joinMap.JoinCallStart.JoinNumber + i), () =>
                    {
                        CodecActiveCallItem call = ActiveCalls[i];
                        if (call != null)
                        {
                            joinCodec.JoinCall(call);
                        }
                        else
                        {
                            Debug.Console(0, this, "[Join Call] Unable to find call at index '{0}'", i);
                        }
                    });
                }
            }

            IHasCallHold holdCodec = this as IHasCallHold;
            if (holdCodec != null)
            {
                trilist.SetSigFalseAction(joinMap.HoldAllCalls.JoinNumber, () =>
                {
                    foreach (CodecActiveCallItem call in ActiveCalls)
                    {
                        holdCodec.HoldCall(call);
                    }
                });

                for (int i = 0; i < joinMap.HoldCallsStart.JoinSpan; i++)
                {
                    int index = i;

                    trilist.SetSigFalseAction((uint)(joinMap.HoldCallsStart.JoinNumber + index), () =>
                    {
                        if (index < 0 || index >= ActiveCalls.Count) return;

                        CodecActiveCallItem call = ActiveCalls[index];
                        if (call != null)
                        {
                            holdCodec.HoldCall(call);
                        }
                        else
                        {
                            Debug.Console(0, this, "[Hold Call] Unable to find call at index '{0}'", i);
                        }
                    });

                    trilist.SetSigFalseAction((uint)(joinMap.ResumeCallsStart.JoinNumber + index), () =>
                    {
                        if (index < 0 || index >= ActiveCalls.Count) return;

                        CodecActiveCallItem call = ActiveCalls[index];
                        if (call != null)
                        {
                            holdCodec.ResumeCall(call);
                        }
                        else
                        {
                            Debug.Console(0, this, "[Resume Call] Unable to find call at index '{0}'", i);
                        }
                    });
                }
            }


            trilist.OnlineStatusChange += (device, args) =>
            {
                if (!args.DeviceOnLine) return;

                // TODO [ ] #983
                Debug.Console(0, this, "LinkVideoCodecCallControlsToApi: device is {0}, IsInCall {1}",
                    args.DeviceOnLine ? "online" : "offline", IsInCall);
                trilist.SetBool(joinMap.HookState.JoinNumber, IsInCall);
                trilist.SetString(joinMap.CurrentCallData.JoinNumber, UpdateCallStatusXSig());
            };
        }

        private string UpdateCallStatusXSig()
        {
            const int maxCalls = 8;
            const int maxAnalogs = 6;
            const int maxDigitals = 2;
            const int offset = maxAnalogs + maxDigitals;
            int analogIndex = 0;
            int digitalIndex = maxAnalogs * maxCalls;
            int arrayIndex = 0;

            XSigToken[]
                tokenArray = new XSigToken[maxCalls * offset]; //set array size for number of calls * pieces of info

            foreach (CodecActiveCallItem call in ActiveCalls)
            {
                if (arrayIndex >= maxCalls * offset)
                    break;
                //digitals
                tokenArray[digitalIndex] = new XSigDigitalToken(digitalIndex + 1, call.IsActiveCall);
                tokenArray[digitalIndex + 1] = new XSigDigitalToken(digitalIndex + 2, call.IsOnHold);

                //serials
                tokenArray[analogIndex] = new XSigSerialToken(analogIndex + 1, call.Name ?? string.Empty);
                tokenArray[analogIndex + 1] = new XSigSerialToken(analogIndex + 2, call.Number ?? string.Empty);
                tokenArray[analogIndex + 2] = new XSigSerialToken(analogIndex + 3, call.Direction.ToString());
                tokenArray[analogIndex + 3] = new XSigSerialToken(analogIndex + 4, call.Type.ToString());
                tokenArray[analogIndex + 4] = new XSigSerialToken(analogIndex + 5, call.Status.ToString());
                if (call.Duration != null)
                {
                    // May need to verify correct string format here
                    string dur = string.Format("{0:c}", call.Duration);
                    tokenArray[analogIndex + 5] = new XSigSerialToken(analogIndex + 6, dur);
                }

                arrayIndex += offset;
                analogIndex += maxAnalogs;
                digitalIndex += maxDigitals;
            }

            while (arrayIndex < maxCalls * offset)
            {
                //digitals
                tokenArray[digitalIndex] = new XSigDigitalToken(digitalIndex + 1, false);
                tokenArray[digitalIndex + 1] = new XSigDigitalToken(digitalIndex + 2, false);


                //serials
                tokenArray[analogIndex] = new XSigSerialToken(analogIndex + 1, string.Empty);
                tokenArray[analogIndex + 1] = new XSigSerialToken(analogIndex + 2, string.Empty);
                tokenArray[analogIndex + 2] = new XSigSerialToken(analogIndex + 3, string.Empty);
                tokenArray[analogIndex + 3] = new XSigSerialToken(analogIndex + 4, string.Empty);
                tokenArray[analogIndex + 4] =
                    new XSigSerialToken(analogIndex + 5, eCodecCallStatus.Disconnected.ToString());
                tokenArray[analogIndex + 5] = new XSigSerialToken(analogIndex + 6, string.Empty);

                arrayIndex += offset;
                analogIndex += maxAnalogs;
                digitalIndex += maxDigitals;
            }

            return GetXSigString(tokenArray);
        }

        private void LinkVideoCodecDtmfToApi(BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.Dtmf0.JoinNumber, () => SendDtmfAction("0", trilist, joinMap));
            trilist.SetSigFalseAction(joinMap.Dtmf1.JoinNumber, () => SendDtmfAction("1", trilist, joinMap));
            trilist.SetSigFalseAction(joinMap.Dtmf2.JoinNumber, () => SendDtmfAction("2", trilist, joinMap));
            trilist.SetSigFalseAction(joinMap.Dtmf3.JoinNumber, () => SendDtmfAction("3", trilist, joinMap));
            trilist.SetSigFalseAction(joinMap.Dtmf4.JoinNumber, () => SendDtmfAction("4", trilist, joinMap));
            trilist.SetSigFalseAction(joinMap.Dtmf5.JoinNumber, () => SendDtmfAction("5", trilist, joinMap));
            trilist.SetSigFalseAction(joinMap.Dtmf6.JoinNumber, () => SendDtmfAction("6", trilist, joinMap));
            trilist.SetSigFalseAction(joinMap.Dtmf7.JoinNumber, () => SendDtmfAction("7", trilist, joinMap));
            trilist.SetSigFalseAction(joinMap.Dtmf8.JoinNumber, () => SendDtmfAction("8", trilist, joinMap));
            trilist.SetSigFalseAction(joinMap.Dtmf9.JoinNumber, () => SendDtmfAction("9", trilist, joinMap));
            trilist.SetSigFalseAction(joinMap.DtmfStar.JoinNumber, () => SendDtmfAction("*", trilist, joinMap));
            trilist.SetSigFalseAction(joinMap.DtmfPound.JoinNumber, () => SendDtmfAction("#", trilist, joinMap));
        }

        /// <summary>
        /// Sends the specified string as a DTMF command.
        /// Reads the value of the SendDtmfToSpecificCallInstance digital join and SelectCall analog join to determine
        /// Whther to send to a specific call index or to the last connected call
        /// </summary>
        /// <param name="s"></param>
        /// <param name="trilist"></param>
        /// <param name="joinMap"></param>
        private void SendDtmfAction(string s, BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            if (!trilist.GetBool(joinMap.SendDtmfToSpecificCallIndex.JoinNumber))
            {
                SendDtmf(s);
            }
            else
            {
                ushort callIndex = trilist.GetUshort(joinMap.SelectCall.JoinNumber);
                if (callIndex > 0 && callIndex <= 8)
                {
                    CodecActiveCallItem call = ActiveCalls[callIndex - 1];
                    if (call != null && call.IsActiveCall)
                    {
                        SendDtmf(s, call);
                    }
                    else
                    {
                        Debug.Console(0, this, "Warning: No call found at index {0} or call is not active.", callIndex);
                    }
                }
                else
                {
                    Debug.Console(0, this, "Warning: Invalid call index specified.  Please use a value of 1-8.");
                }
            }
        }

        private void LinkVideoCodecCameraLayoutsToApi(IHasCodecLayouts codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.CameraLayout.JoinNumber, codec.LocalLayoutToggle);

            codec.LocalLayoutFeedback.LinkInputSig(trilist.StringInput[joinMap.CurrentLayoutStringFb.JoinNumber]);
        }

        private void LinkVideoCodecCameraModeToApi(IHasCameraAutoMode codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.CameraModeAuto.JoinNumber, codec.CameraAutoModeOn);
            trilist.SetSigFalseAction(joinMap.CameraModeManual.JoinNumber, codec.CameraAutoModeOff);

            codec.CameraAutoModeIsOnFeedback.OutputChange += (o, a) =>
            {
                IHasCameraOff offCodec = codec as IHasCameraOff;

                if (offCodec != null)
                {
                    if (offCodec.CameraIsOffFeedback.BoolValue)
                    {
                        trilist.SetBool(joinMap.CameraModeAuto.JoinNumber, false);
                        trilist.SetBool(joinMap.CameraModeManual.JoinNumber, false);
                        trilist.SetBool(joinMap.CameraModeOff.JoinNumber, true);
                        return;
                    }

                    trilist.SetBool(joinMap.CameraModeAuto.JoinNumber, a.BoolValue);
                    trilist.SetBool(joinMap.CameraModeManual.JoinNumber, !a.BoolValue);
                    trilist.SetBool(joinMap.CameraModeOff.JoinNumber, false);
                    return;
                }

                trilist.SetBool(joinMap.CameraModeAuto.JoinNumber, a.BoolValue);
                trilist.SetBool(joinMap.CameraModeManual.JoinNumber, !a.BoolValue);
                trilist.SetBool(joinMap.CameraModeOff.JoinNumber, false);
            };

            IHasCameraOff offModeCodec = codec as IHasCameraOff;

            if (offModeCodec != null)
            {
                if (offModeCodec.CameraIsOffFeedback.BoolValue)
                {
                    trilist.SetBool(joinMap.CameraModeAuto.JoinNumber, false);
                    trilist.SetBool(joinMap.CameraModeManual.JoinNumber, false);
                    trilist.SetBool(joinMap.CameraModeOff.JoinNumber, true);
                    return;
                }

                trilist.SetBool(joinMap.CameraModeAuto.JoinNumber, codec.CameraAutoModeIsOnFeedback.BoolValue);
                trilist.SetBool(joinMap.CameraModeManual.JoinNumber, !codec.CameraAutoModeIsOnFeedback.BoolValue);
                trilist.SetBool(joinMap.CameraModeOff.JoinNumber, false);
                return;
            }

            trilist.SetBool(joinMap.CameraModeAuto.JoinNumber, codec.CameraAutoModeIsOnFeedback.BoolValue);
            trilist.SetBool(joinMap.CameraModeManual.JoinNumber, !codec.CameraAutoModeIsOnFeedback.BoolValue);
            trilist.SetBool(joinMap.CameraModeOff.JoinNumber, false);
        }

        private void LinkVideoCodecSelfviewToApi(IHasCodecSelfView codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            trilist.SetSigFalseAction(joinMap.CameraSelfView.JoinNumber, codec.SelfViewModeToggle);

            codec.SelfviewIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.CameraSelfView.JoinNumber]);
        }

        private void LinkVideoCodecCameraToApi(IHasCodecCameras codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            //Camera PTZ
            trilist.SetBoolSigAction(joinMap.CameraTiltUp.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                IHasCameraPtzControl camera = codec.SelectedCamera as IHasCameraPtzControl;

                if (camera == null) return;

                if (b) camera.TiltUp();
                else camera.TiltStop();
            });

            trilist.SetBoolSigAction(joinMap.CameraTiltDown.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                IHasCameraPtzControl camera = codec.SelectedCamera as IHasCameraPtzControl;

                if (camera == null) return;

                if (b) camera.TiltDown();
                else camera.TiltStop();
            });
            trilist.SetBoolSigAction(joinMap.CameraPanLeft.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                IHasCameraPtzControl camera = codec.SelectedCamera as IHasCameraPtzControl;

                if (camera == null) return;

                if (b) camera.PanLeft();
                else camera.PanStop();
            });
            trilist.SetBoolSigAction(joinMap.CameraPanRight.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                IHasCameraPtzControl camera = codec.SelectedCamera as IHasCameraPtzControl;

                if (camera == null) return;

                if (b) camera.PanRight();
                else camera.PanStop();
            });

            trilist.SetBoolSigAction(joinMap.CameraZoomIn.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                IHasCameraPtzControl camera = codec.SelectedCamera as IHasCameraPtzControl;

                if (camera == null) return;

                if (b) camera.ZoomIn();
                else camera.ZoomStop();
            });

            trilist.SetBoolSigAction(joinMap.CameraZoomOut.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                IHasCameraPtzControl camera = codec.SelectedCamera as IHasCameraPtzControl;

                if (camera == null) return;

                if (b) camera.ZoomOut();
                else camera.ZoomStop();
            });


            trilist.SetBoolSigAction(joinMap.CameraFocusNear.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                IHasCameraFocusControl camera = codec.SelectedCamera as IHasCameraFocusControl;

                if (camera == null) return;

                if (b) camera.FocusNear();
                else camera.FocusStop();
            });

            trilist.SetBoolSigAction(joinMap.CameraFocusFar.JoinNumber, (b) =>
            {
                if (codec.SelectedCamera == null) return;
                IHasCameraFocusControl camera = codec.SelectedCamera as IHasCameraFocusControl;

                if (camera == null) return;

                if (b) camera.FocusFar();
                else camera.FocusStop();
            });

            trilist.SetSigFalseAction(joinMap.CameraFocusAuto.JoinNumber, () =>
            {
                if (codec.SelectedCamera == null) return;
                IHasCameraFocusControl camera = codec.SelectedCamera as IHasCameraFocusControl;

                if (camera == null) return;

                camera.TriggerAutoFocus();
            });

            // Camera count
            trilist.SetUshort(joinMap.CameraCount.JoinNumber, (ushort)codec.Cameras.Count);

            // Camera names
            for (uint i = 0; i < joinMap.CameraNamesFb.JoinSpan; i++)
            {
                //Check the count first
                if (i < codec.Cameras.Count && codec.Cameras[(int)i] != null)
                {
                    trilist.SetString(joinMap.CameraNamesFb.JoinNumber + i, codec.Cameras[(int)i].Name);
                }
                else
                {
                    trilist.SetString(joinMap.CameraNamesFb.JoinNumber + i, "");
                }
            }

            //Camera Select
            trilist.SetUShortSigAction(joinMap.CameraNumberSelect.JoinNumber, (i) =>
            {
                if (i > 0 && i <= codec.Cameras.Count)
                {
                    codec.SelectCamera(codec.Cameras[i - 1].Key);
                }
                else
                {
                    Debug.Console(0, this, "Unable to select.  No camera found at index {0}", i);
                }
            });

            // Set initial selected camera feedback
            if (codec.SelectedCamera != null)
            {
                trilist.SetUshort(joinMap.CameraNumberSelect.JoinNumber,
                    (ushort)codec.Cameras.FindIndex((c) => c.Key == codec.SelectedCamera.Key));
            }

            codec.CameraSelected += (sender, args) =>
            {
                ushort i = (ushort)codec.Cameras.FindIndex((c) => c.Key == args.SelectedCamera.Key);

                trilist.SetUshort(joinMap.CameraNumberSelect.JoinNumber, (ushort)(i + 1));

                if (codec is IHasCodecRoomPresets)
                {
                    return;
                }

                if (!(args.SelectedCamera is IHasCameraPresets))
                {
                    return;
                }

                IHasCameraPresets cam = args.SelectedCamera as IHasCameraPresets;
                SetCameraPresetNames(cam.Presets);

                (args.SelectedCamera as IHasCameraPresets).PresetsListHasChanged +=
                    (o, eventArgs) => SetCameraPresetNames(cam.Presets);

                trilist.SetUShortSigAction(joinMap.CameraPresetSelect.JoinNumber,
                    (a) =>
                    {
                        cam.PresetSelect(a);
                        trilist.SetUshort(joinMap.CameraPresetSelect.JoinNumber, a);
                    });

                trilist.SetSigFalseAction(joinMap.CameraPresetSave.JoinNumber,
                    () =>
                    {
                        cam.PresetStore(trilist.UShortOutput[joinMap.CameraPresetSelect.JoinNumber].UShortValue,
                            string.Empty);
                        trilist.PulseBool(joinMap.CameraPresetSave.JoinNumber, 3000);
                    });
            };

            if (!(codec is IHasCodecRoomPresets)) return;

            IHasCodecRoomPresets presetCodec = codec as IHasCodecRoomPresets;

            presetCodec.CodecRoomPresetsListHasChanged +=
                (sender, args) => SetCameraPresetNames(presetCodec.NearEndPresets);

            //Camera Presets
            trilist.SetUShortSigAction(joinMap.CameraPresetSelect.JoinNumber,
                (i) => { presetCodec.CodecRoomPresetSelect(i); });


            // Far End Presets
            trilist.SetUShortSigAction(joinMap.FarEndPresetSelect.JoinNumber,
                (i) => { presetCodec.SelectFarEndPreset(i); });


            trilist.SetSigFalseAction(joinMap.CameraPresetSave.JoinNumber,
                () =>
                {
                    presetCodec.CodecRoomPresetStore(
                        trilist.UShortOutput[joinMap.CameraPresetSelect.JoinNumber].UShortValue, string.Empty);
                    trilist.PulseBool(joinMap.CameraPresetSave.JoinNumber, 3000);
                });

            trilist.OnlineStatusChange += (device, args) =>
            {
                if (!args.DeviceOnLine) return;

                // TODO [ ] Issue #868
                trilist.SetString(joinMap.CameraPresetNames.JoinNumber, "\xFC");
                SetCameraPresetNames(presetCodec.NearEndPresets);
            };
        }

        // Following fields only used for Bridging
        private int _selectedRecentCallItemIndex;
        private CodecCallHistory.CallHistoryEntry _selectedRecentCallItem;
        private DirectoryItem _selectedDirectoryItem;

        private void LinkVideoCodecCallHistoryToApi(IHasCallHistory codec, BasicTriList trilist,
            VideoCodecControllerJoinMap joinMap)
        {
            codec.CallHistory.RecentCallsListHasChanged += (o, a) => { UpdateCallHistory(codec, trilist, joinMap); };

            // Selected item action and feedback
            trilist.SetUShortSigAction(joinMap.SelectRecentCallItem.JoinNumber, (u) =>
            {
                if (u == 0 || u > codec.CallHistory.RecentCalls.Count)
                {
                    Debug.Console(2, this, "Recent Call History index out of range");
                    return;
                }

                _selectedRecentCallItemIndex = (int)(u - 1);
                trilist.SetUshort(joinMap.SelectRecentCallItem.JoinNumber, u);

                CodecCallHistory.CallHistoryEntry _selectedRecentCallItem =
                    codec.CallHistory.RecentCalls[_selectedRecentCallItemIndex];

                if (_selectedRecentCallItem != null)
                {
                    trilist.SetString(joinMap.SelectedRecentCallName.JoinNumber, _selectedRecentCallItem.Name);
                    trilist.SetString(joinMap.SelectedRecentCallNumber.JoinNumber, _selectedRecentCallItem.Number);
                    trilist.SetSigFalseAction(joinMap.RemoveSelectedRecentCallItem.JoinNumber,
                        () => codec.RemoveCallHistoryEntry(_selectedRecentCallItem));
                    trilist.SetSigFalseAction(joinMap.DialSelectedRecentCallItem.JoinNumber,
                        () => this.Dial(_selectedRecentCallItem.Number));
                }
                else
                {
                    trilist.SetString(joinMap.SelectedRecentCallName.JoinNumber, string.Empty);
                    trilist.SetString(joinMap.SelectedRecentCallNumber.JoinNumber, string.Empty);
                    trilist.ClearBoolSigAction(joinMap.RemoveSelectedRecentCallItem.JoinNumber);
                    trilist.ClearBoolSigAction(joinMap.DialSelectedRecentCallItem.JoinNumber);
                }
            });
        }


        private void UpdateCallHistory(IHasCallHistory codec, BasicTriList trilist, VideoCodecControllerJoinMap joinMap)
        {
            // Clear out selected item
            _selectedRecentCallItemIndex = 0;
            _selectedRecentCallItem = null;
            trilist.SetUshort(joinMap.SelectRecentCallItem.JoinNumber, 0);
            trilist.SetString(joinMap.SelectedRecentCallName.JoinNumber, string.Empty);
            trilist.SetString(joinMap.SelectedRecentCallNumber.JoinNumber, string.Empty);
            trilist.ClearBoolSigAction(joinMap.RemoveSelectedRecentCallItem.JoinNumber);
            //

            trilist.SetUshort(joinMap.RecentCallCount.JoinNumber, (ushort)codec.CallHistory.RecentCalls.Count);

            // Update the call history joins
            uint maxItems = joinMap.RecentCallNamesStart.JoinSpan;

            // Create history
            uint index = 0;
            for (uint i = 0; i < maxItems && i < codec.CallHistory.RecentCalls.Count; i++)
            {
                trilist.SetString(joinMap.RecentCallNamesStart.JoinNumber + i,
                    codec.CallHistory.RecentCalls[(int)i].Name);
                trilist.SetString(joinMap.RecentCallTimesStart.JoinNumber + i,
                    codec.CallHistory.RecentCalls[(int)i].StartTime.ToShortTimeString());
                trilist.SetUshort(joinMap.RecentCallOccurrenceType.JoinNumber + i,
                    (ushort)codec.CallHistory.RecentCalls[(int)i].OccurrenceType);
                //i++;
                index = i;
            }

            //foreach(var item in codec.CallHistory.RecentCalls)
            //{
            //    trilist.SetString(joinMap.RecentCallNamesStart.JoinNumber + i, item.Name);
            //    trilist.SetString(joinMap.RecentCallTimesStart.JoinNumber + i, item.StartTime.ToShortTimeString());
            //    trilist.SetUshort(joinMap.RecentCallOccurrenceType.JoinNumber + i, (ushort)item.OccurrenceType);
            //    i++;
            //}

            // Clears existing items 
            for (uint j = index; j < maxItems; j++)
            {
                trilist.SetString(joinMap.RecentCallNamesStart.JoinNumber + j, string.Empty);
                trilist.SetString(joinMap.RecentCallTimesStart.JoinNumber + j, string.Empty);
                trilist.SetUshort(joinMap.RecentCallOccurrenceType.JoinNumber + j, 0);
            }
        }

        private string SetCameraPresetNames(IEnumerable<CodecRoomPreset> presets)
        {
            return SetCameraPresetNames(presets.Select(p => p.Description).ToList());
        }

        private string SetCameraPresetNames(IEnumerable<CameraPreset> presets)
        {
            return SetCameraPresetNames(presets.Select(p => p.Description).ToList());
        }

        private string SetCameraPresetNames(ICollection<string> presets)
        {
            int i = 1; //start index for xsig;

            XSigToken[] tokenArray = new XSigToken[presets.Count];

            foreach (string preset in presets)
            {
                XSigSerialToken cameraPreset = new XSigSerialToken(i, preset);
                tokenArray[i - 1] = cameraPreset;
                i++;
            }

            return GetXSigString(tokenArray);
        }

        private string GetXSigString(XSigToken[] tokenArray)
        {
            string returnString;
            using (MemoryStream s = new MemoryStream())
            {
                using (XSigTokenStreamWriter tw = new XSigTokenStreamWriter(s, true))
                {
                    tw.WriteXSigData(tokenArray);
                }

                byte[] xSig = s.ToArray();

                returnString = Encoding.GetEncoding(XSigEncoding).GetString(xSig, 0, xSig.Length);
            }

            return returnString;
        }

        #endregion
    }


    /// <summary>
    /// Used to track the status of syncronizing the phonebook values when connecting to a codec or refreshing the phonebook info
    /// </summary>
    public class CodecPhonebookSyncState : IKeyed
    {
        private bool _InitialSyncComplete;

        public CodecPhonebookSyncState(string key)
        {
            Key = key;

            CodecDisconnected();
        }

        public bool InitialSyncComplete
        {
            get { return _InitialSyncComplete; }
            private set
            {
                if (value == true)
                {
                    EventHandler<EventArgs> handler = InitialSyncCompleted;
                    if (handler != null)
                    {
                        handler(this, new EventArgs());
                    }
                }

                _InitialSyncComplete = value;
            }
        }

        public bool InitialPhonebookFoldersWasReceived { get; private set; }

        public bool NumberOfContactsWasReceived { get; private set; }

        public bool PhonebookRootEntriesWasRecieved { get; private set; }

        public bool PhonebookHasFolders { get; private set; }

        public int NumberOfContacts { get; private set; }

        #region IKeyed Members

        public string Key { get; private set; }

        #endregion

        public event EventHandler<EventArgs> InitialSyncCompleted;

        public void InitialPhonebookFoldersReceived()
        {
            InitialPhonebookFoldersWasReceived = true;

            CheckSyncStatus();
        }

        public void PhonebookRootEntriesReceived()
        {
            PhonebookRootEntriesWasRecieved = true;

            CheckSyncStatus();
        }

        public void SetPhonebookHasFolders(bool value)
        {
            PhonebookHasFolders = value;

            Debug.Console(1, this, "Phonebook has folders: {0}", PhonebookHasFolders);
        }

        public void SetNumberOfContacts(int contacts)
        {
            NumberOfContacts = contacts;
            NumberOfContactsWasReceived = true;

            Debug.Console(1, this, "Phonebook contains {0} contacts.", NumberOfContacts);

            CheckSyncStatus();
        }

        public void CodecDisconnected()
        {
            InitialPhonebookFoldersWasReceived = false;
            PhonebookHasFolders = false;
            NumberOfContacts = 0;
            NumberOfContactsWasReceived = false;
        }

        private void CheckSyncStatus()
        {
            if (InitialPhonebookFoldersWasReceived && NumberOfContactsWasReceived && PhonebookRootEntriesWasRecieved)
            {
                InitialSyncComplete = true;
                Debug.Console(1, this, "Initial Phonebook Sync Complete!");
            }
            else
            {
                InitialSyncComplete = false;
            }
        }
    }

    /// <summary>
    /// Represents a codec command that might need to have a friendly label applied for UI feedback purposes
    /// </summary>
    public class CodecCommandWithLabel
    {
        public string Command { get; private set; }
        public string Label { get; private set; }

        public CodecCommandWithLabel(string command, string label)
        {
            Command = command;
            Label = label;
        }
    }
}