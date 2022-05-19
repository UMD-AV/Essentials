using System.Linq;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace QscQsysDspPlugin
{
	/// <summary>
	/// QSC DSP api extensions
	/// </summary>
	public static class QscDspDeviceApiExtensions
	{
        public static void LinkToApiExt(this QscDsp DspDevice, BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
		{
			var joinMap = new QscDspDeviceJoinMap(joinStart);

			Debug.Console(1, DspDevice, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
			ushort x = 1;
			var comm = DspDevice as ICommunicationMonitor;

			// from Plugin > to SiMPL
			DspDevice.IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = DspDevice.Name;

			foreach (var channel in DspDevice.LevelControlPoints)
			{
				//var QscChannel = channel.Value as QSC.DSP.EPI.QscDspLevelControl;
				Debug.Console(2, "QscChannel {0} connect", x);

				var genericChannel = channel.Value as IBasicVolumeWithFeedback;
				if (channel.Value.Enabled)
				{
					// from SiMPL > to Plugin
					trilist.StringInput[joinMap.ChannelName.JoinNumber + x].StringValue = channel.Value.LevelCustomName;
                    trilist.UShortInput[joinMap.ChannelType.JoinNumber + x].UShortValue = (ushort)channel.Value.Type;
                    trilist.BooleanInput[joinMap.ChannelVisible.JoinNumber + x].BoolValue = true;

					// from Plugin > to SiMPL
                    genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.ChannelMuteToggle.JoinNumber + x]);
                    genericChannel.VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[joinMap.ChannelVolume.JoinNumber + x]);

					// from SiMPL > to Plugin
                    trilist.SetSigTrueAction(joinMap.ChannelMuteToggle.JoinNumber + x, () => genericChannel.MuteToggle());
                    trilist.SetSigTrueAction(joinMap.ChannelMuteOn.JoinNumber + x, () => genericChannel.MuteOn());
                    trilist.SetSigTrueAction(joinMap.ChannelMuteOff.JoinNumber + x, () => genericChannel.MuteOff());
					// from SiMPL > to Plugin
                    trilist.SetBoolSigAction(joinMap.ChannelVolumeUp.JoinNumber + x, b => genericChannel.VolumeUp(b));
                    trilist.SetBoolSigAction(joinMap.ChannelVolumeDown.JoinNumber + x, b => genericChannel.VolumeDown(b));
					// from SiMPL > to Plugin
                    trilist.SetUShortSigAction(joinMap.ChannelVolume.JoinNumber + x, u =>
                    {
                        if(trilist.BooleanOutput[joinMap.EnableLevelSend.JoinNumber].BoolValue == true)
                        { 
                            genericChannel.SetVolume(u);
                        }
                    });
				}
				x++;
			}

			// Presets 
			x = 0;
			// from SiMPL > to Plugin
			trilist.SetStringSigAction(joinMap.Presets.JoinNumber, s => DspDevice.RunPreset(s));
			foreach (var preset in DspDevice.PresetList)
			{
				var temp = x;
				// from SiMPL > to Plugin
				trilist.StringInput[joinMap.Presets.JoinNumber + temp + 1].StringValue = preset.Label;
				trilist.SetSigTrueAction(joinMap.Presets.JoinNumber + temp + 1, () => DspDevice.RunPresetNumber(temp));
				x++;
			}

			// VoIP Dialer
			uint lineOffset = 0;
			foreach (var line in DspDevice.Dialers)
			{
				var dialer = line;

				var dialerLineOffset = lineOffset;
				Debug.Console(2, "AddingDialerBRidge {0} {1} Offset", dialer.Key, dialerLineOffset);

				// from SiMPL > to Plugin
				trilist.SetSigTrueAction((joinMap.Keypad0.JoinNumber + dialerLineOffset), () => DspDevice.Dialers[dialer.Key].SendKeypad(QscDspDialer.EKeypadKeys.Num0));
                trilist.SetSigTrueAction((joinMap.Keypad1.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Num1));
                trilist.SetSigTrueAction((joinMap.Keypad2.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Num2));
                trilist.SetSigTrueAction((joinMap.Keypad3.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Num3));
                trilist.SetSigTrueAction((joinMap.Keypad4.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Num4));
                trilist.SetSigTrueAction((joinMap.Keypad5.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Num5));
                trilist.SetSigTrueAction((joinMap.Keypad6.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Num6));
                trilist.SetSigTrueAction((joinMap.Keypad7.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Num7));
                trilist.SetSigTrueAction((joinMap.Keypad8.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Num8));
                trilist.SetSigTrueAction((joinMap.Keypad9.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Num9));
                trilist.SetSigTrueAction((joinMap.KeypadStar.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Star));
                trilist.SetSigTrueAction((joinMap.KeypadPound.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Pound));
                trilist.SetSigTrueAction((joinMap.KeypadClear.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Clear));
                trilist.SetSigTrueAction((joinMap.KeypadBackspace.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(QscDspDialer.EKeypadKeys.Backspace));
				// from SiMPL > to Plugin
                trilist.SetSigTrueAction(joinMap.Dial.JoinNumber + dialerLineOffset, () => dialer.Value.Dial());
                trilist.SetSigTrueAction(joinMap.DoNotDisturbToggle.JoinNumber + dialerLineOffset, () => dialer.Value.DoNotDisturbToggle());
                trilist.SetSigTrueAction(joinMap.DoNotDisturbOn.JoinNumber + dialerLineOffset, () => dialer.Value.DoNotDisturbOn());
                trilist.SetSigTrueAction(joinMap.DoNotDisturbOff.JoinNumber + dialerLineOffset, () => dialer.Value.DoNotDisturbOff());
				trilist.SetSigTrueAction(joinMap.AutoAnswerToggle.JoinNumber + dialerLineOffset, () => dialer.Value.AutoAnswerToggle());
                trilist.SetSigTrueAction(joinMap.AutoAnswerOn.JoinNumber + dialerLineOffset, () => dialer.Value.AutoAnswerOn());
                trilist.SetSigTrueAction(joinMap.AutoAnswerOff.JoinNumber + dialerLineOffset, () => dialer.Value.AutoAnswerOff());
                trilist.SetSigTrueAction(joinMap.EndCall.JoinNumber + dialerLineOffset, () => dialer.Value.EndAllCalls());
				trilist.SetSigTrueAction(joinMap.IncomingCallAccept.JoinNumber + dialerLineOffset, () => dialer.Value.AcceptCall());
				trilist.SetSigTrueAction(joinMap.IncomingCallReject.JoinNumber + dialerLineOffset, () => dialer.Value.RejectCall());

				// from Plugin > to SiMPL
                dialer.Value.DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DoNotDisturbToggle.JoinNumber + dialerLineOffset]);
                dialer.Value.DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DoNotDisturbOn.JoinNumber + dialerLineOffset]);
                dialer.Value.DoNotDisturbFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.DoNotDisturbOff.JoinNumber + dialerLineOffset]);

				// from Plugin > to SiMPL
				dialer.Value.AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoAnswerToggle.JoinNumber + dialerLineOffset]);
                dialer.Value.AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoAnswerOn.JoinNumber + dialerLineOffset]);
                dialer.Value.AutoAnswerFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.AutoAnswerOff.JoinNumber + dialerLineOffset]);
                dialer.Value.CallerIdNumberFb.LinkInputSig(trilist.StringInput[joinMap.CallerIdNumberFb.JoinNumber + dialerLineOffset]);

				// from Plugin > to SiMPL
                dialer.Value.OffHookFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Dial.JoinNumber + dialerLineOffset]);
				dialer.Value.OffHookFeedback.LinkInputSig(trilist.BooleanInput[joinMap.OffHook.JoinNumber + dialerLineOffset]);
				dialer.Value.OffHookFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.OnHook.JoinNumber + dialerLineOffset]);
                dialer.Value.DialStringFeedback.LinkInputSig(trilist.StringInput[joinMap.DialStringCmd.JoinNumber + dialerLineOffset]);

				// from Plugin > to SiMPL
                dialer.Value.IncomingCallFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IncomingCall.JoinNumber + dialerLineOffset]);

				lineOffset = lineOffset + 50;
			}
		}
	}

	/// <summary>
	/// QSC DSP Join Map
	/// </summary>
	public class QscDspDeviceJoinMap : JoinMapBaseAdvanced
	{
        [JoinName("IsOnline")]
        public JoinDataComplete IsOnline =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Online Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Name")]
        public JoinDataComplete Name =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Name Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("EnableLevelSend")]
        public JoinDataComplete EnableLevelSend =
            new JoinDataComplete(new JoinData { JoinNumber = 11, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Enable Level Sending from SIMPL",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelVisible")]
        public JoinDataComplete ChannelVisible =
            new JoinDataComplete(new JoinData { JoinNumber = 200, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Visible Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelMuteToggle")]
        public JoinDataComplete ChannelMuteToggle =
            new JoinDataComplete(new JoinData { JoinNumber = 400, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Mute Toggle Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelMuteOn")]
        public JoinDataComplete ChannelMuteOn =
            new JoinDataComplete(new JoinData { JoinNumber = 600, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Mute On",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelMuteOff")]
        public JoinDataComplete ChannelMuteOff =
            new JoinDataComplete(new JoinData { JoinNumber = 800, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Mute Off",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelVolumeUp")]
        public JoinDataComplete ChannelVolumeUp =
            new JoinDataComplete(new JoinData { JoinNumber = 1000, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Volume Up",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelVolumeDown")]
        public JoinDataComplete ChannelVolumeDown =
            new JoinDataComplete(new JoinData { JoinNumber = 1200, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Volume Down",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Presets")]
        public JoinDataComplete Presets =
            new JoinDataComplete(new JoinData { JoinNumber = 100, JoinSpan = 100 },
            new JoinMetadata
            {
                Description = "Preset Recall with Name Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.DigitalSerial
            });

        [JoinName("ChannelVolume")]
        public JoinDataComplete ChannelVolume =
            new JoinDataComplete(new JoinData { JoinNumber = 200, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Volume Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.DigitalAnalog
            });

        [JoinName("ChannelType")]
        public JoinDataComplete ChannelType =
            new JoinDataComplete(new JoinData { JoinNumber = 400, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Type Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("ChannelName")]
        public JoinDataComplete ChannelName =
            new JoinDataComplete(new JoinData { JoinNumber = 200, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Name Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        //*****Dialer Region*****//

        [JoinName("IncomingCall")]
        public JoinDataComplete IncomingCall =
            new JoinDataComplete(new JoinData { JoinNumber = 3100, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Incoming Call Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("DialStringCmd")]
        public JoinDataComplete DialStringCmd =
            new JoinDataComplete(new JoinData { JoinNumber = 3100, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Dial String Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CallerIdNumberFb")]
        public JoinDataComplete CallerIdNumberFb =
            new JoinDataComplete(new JoinData { JoinNumber = 3104, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Caller ID Number Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("EndCall")]
        public JoinDataComplete EndCall =
            new JoinDataComplete(new JoinData { JoinNumber = 3107, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "End Call",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Keypad0")]
        public JoinDataComplete Keypad0 =
            new JoinDataComplete(new JoinData { JoinNumber = 3110, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad 0",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Keypad1")]
        public JoinDataComplete Keypad1 =
            new JoinDataComplete(new JoinData { JoinNumber = 3111, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad 1",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Keypad2")]
        public JoinDataComplete Keypad2 =
            new JoinDataComplete(new JoinData { JoinNumber = 3112, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad 2",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Keypad3")]
        public JoinDataComplete Keypad3 =
            new JoinDataComplete(new JoinData { JoinNumber = 3113, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad 3",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Keypad4")]
        public JoinDataComplete Keypad4 =
            new JoinDataComplete(new JoinData { JoinNumber = 3114, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad 4",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Keypad5")]
        public JoinDataComplete Keypad5 =
            new JoinDataComplete(new JoinData { JoinNumber = 3115, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad 5",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Keypad6")]
        public JoinDataComplete Keypad6 =
            new JoinDataComplete(new JoinData { JoinNumber = 3116, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad 6",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Keypad7")]
        public JoinDataComplete Keypad7 =
            new JoinDataComplete(new JoinData { JoinNumber = 3117, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad 7",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Keypad8")]
        public JoinDataComplete Keypad8 =
            new JoinDataComplete(new JoinData { JoinNumber = 3118, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad 8",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Keypad9")]
        public JoinDataComplete Keypad9 =
            new JoinDataComplete(new JoinData { JoinNumber = 3119, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad 9",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("KeypadStar")]
        public JoinDataComplete KeypadStar =
            new JoinDataComplete(new JoinData { JoinNumber = 3120, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad Star",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("KeypadPound")]
        public JoinDataComplete KeypadPound =
            new JoinDataComplete(new JoinData { JoinNumber = 3121, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad Pound",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("KeypadClear")]
        public JoinDataComplete KeypadClear =
            new JoinDataComplete(new JoinData { JoinNumber = 3122, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad Clear",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("KeypadBackspace")]
        public JoinDataComplete KeypadBackspace =
            new JoinDataComplete(new JoinData { JoinNumber = 3123, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Keypad Backspace",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Dial")]
        public JoinDataComplete Dial =
            new JoinDataComplete(new JoinData { JoinNumber = 3124, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Dial Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("AutoAnswerOn")]
        public JoinDataComplete AutoAnswerOn =
            new JoinDataComplete(new JoinData { JoinNumber = 3125, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Auto Answer On Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("AutoAnswerOff")]
        public JoinDataComplete AutoAnswerOff =
            new JoinDataComplete(new JoinData { JoinNumber = 3126, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Auto Answer Off Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("AutoAnswerToggle")]
        public JoinDataComplete AutoAnswerToggle =
            new JoinDataComplete(new JoinData { JoinNumber = 3127, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Auto Answer Toggle Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("OnHook")]
        public JoinDataComplete OnHook =
            new JoinDataComplete(new JoinData { JoinNumber = 3129, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "On Hook Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("OffHook")]
        public JoinDataComplete OffHook =
            new JoinDataComplete(new JoinData { JoinNumber = 3130, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Off Hook Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("DoNotDisturbOn")]
        public JoinDataComplete DoNotDisturbOn =
            new JoinDataComplete(new JoinData { JoinNumber = 3132, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Do Not Disturb On Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("DoNotDisturbOff")]
        public JoinDataComplete DoNotDisturbOff =
            new JoinDataComplete(new JoinData { JoinNumber = 3133, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Do Not Disturb Off Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("DoNotDisturbToggle")]
        public JoinDataComplete DoNotDisturbToggle =
            new JoinDataComplete(new JoinData { JoinNumber = 3134, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Do Not Disturb Toggle Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("IncomingCallAccept")]
        public JoinDataComplete IncomingCallAccept =
            new JoinDataComplete(new JoinData { JoinNumber = 3136, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Incoming Call Accept",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("IncomingCallReject")]
        public JoinDataComplete IncomingCallReject =
            new JoinDataComplete(new JoinData { JoinNumber = 3137, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Incoming Call Reject",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        public QscDspDeviceJoinMap(uint joinStart)
            : base(joinStart, typeof(QscDspDeviceJoinMap))
        {
        }
	}
}