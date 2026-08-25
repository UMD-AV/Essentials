using System;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.DM;
using Crestron.SimplSharpPro.DM.Cards;
using PepperDash.Core;
using UmdEssentials.Core;
using UmdEssentials.Core.Bridges;


namespace UmdEssentials.DM
{
    /// <summary>
    /// Exposes the volume levels for Program, Aux1, Aux2, Codec1, Codec2, and Digital outputs on a DMPS3 chassis
    /// </summary>
    public class DmpsAudioOutputController : EssentialsBridgeableDevice
    {
        public DmpsAudioOutput MasterVolumeLevel { get; private set; }
        public DmpsAudioOutput SourceVolumeLevel { get; private set; }
        public DmpsAudioOutput MicsMasterVolumeLevel { get; private set; }
        public DmpsAudioOutput Codec1VolumeLevel { get; private set; }
        public DmpsAudioOutput Codec2VolumeLevel { get; private set; }

        public DmpsAudioOutputController(string key, string name, DMOutput card,
            Card.Dmps3HdmiAudioOutput.Dmps3AudioOutputStream stream)
            : base(key, name)
        {
            card.BaseDevice.DMOutputChange += BaseDevice_DMOutputChange;
            Dmps3AudioOutputWithMixerBase output = new Dmps3AudioOutputWithMixerBase(stream);
            MasterVolumeLevel = new DmpsAudioOutputWithMixer(output, EDmpsLevelType.Master);
            SourceVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.Source);
        }

        public DmpsAudioOutputController(string key, string name, DMOutput card,
            Card.Dmps3HdmiAudioOutput.Dmps3DmHdmiOutputStream stream)
            : base(key, name)
        {
            card.BaseDevice.DMOutputChange += BaseDevice_DMOutputChange;
            Dmps3AudioOutputWithMixerBase output = new Dmps3AudioOutputWithMixerBase(stream);
            MasterVolumeLevel = new DmpsAudioOutputWithMixer(output, EDmpsLevelType.Master);
            SourceVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.Source);
        }

        public DmpsAudioOutputController(string key, string name, Card.Dmps3OutputBase card)
            : base(key, name)
        {
            card.BaseDevice.DMOutputChange += BaseDevice_DMOutputChange;

            if (card is Card.Dmps3ProgramOutput)
            {
                Card.Dmps3ProgramOutput programOutput = card as Card.Dmps3ProgramOutput;
                Dmps3AudioOutputWithMixerBase output =
                    new Dmps3AudioOutputWithMixerBase(card, programOutput.OutputMixer);
                MasterVolumeLevel =
                    new DmpsAudioOutputWithMixerAndEq(output, EDmpsLevelType.Master, programOutput.OutputEqualizer);
                SourceVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.Source);
                MicsMasterVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.MicsMaster);
                Codec1VolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.Codec1);
                Codec2VolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.Codec2);
            }
            else if (card is Card.Dmps3Aux1Output)
            {
                Card.Dmps3Aux1Output auxOutput = card as Card.Dmps3Aux1Output;
                Dmps3AudioOutputWithMixerBase output = new Dmps3AudioOutputWithMixerBase(card, auxOutput.OutputMixer);
                MasterVolumeLevel =
                    new DmpsAudioOutputWithMixerAndEq(output, EDmpsLevelType.Master, auxOutput.OutputEqualizer);
                SourceVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.Source);
                MicsMasterVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.MicsMaster);
                Codec2VolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.Codec2);
            }
            else if (card is Card.Dmps3Aux2Output)
            {
                Card.Dmps3Aux2Output auxOutput = card as Card.Dmps3Aux2Output;
                Dmps3AudioOutputWithMixerBase output = new Dmps3AudioOutputWithMixerBase(card, auxOutput.OutputMixer);
                MasterVolumeLevel =
                    new DmpsAudioOutputWithMixerAndEq(output, EDmpsLevelType.Master, auxOutput.OutputEqualizer);
                SourceVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.Source);
                MicsMasterVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.MicsMaster);
                Codec1VolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.Codec1);
            }
            else if (card is Card.Dmps3DigitalMixOutput)
            {
                Card.Dmps3DigitalMixOutput mixOutput = card as Card.Dmps3DigitalMixOutput;
                Dmps3AudioOutputWithMixerBase output = new Dmps3AudioOutputWithMixerBase(card, mixOutput.OutputMixer);
                MasterVolumeLevel = new DmpsAudioOutputWithMixer(output, EDmpsLevelType.Master);
                SourceVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.Source);
                MicsMasterVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.MicsMaster);
            }
            else if (card is Card.Dmps3HdmiOutput)
            {
                Card.Dmps3HdmiOutput hdmiOutput = card as Card.Dmps3HdmiOutput;
                Dmps3AudioOutputWithMixerBase output = new Dmps3AudioOutputWithMixerBase(card, hdmiOutput.OutputMixer);
                MasterVolumeLevel = new DmpsAudioOutputWithMixer(output, EDmpsLevelType.Master);
                SourceVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.Source);
                MicsMasterVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.MicsMaster);
            }
            else if (card is Card.Dmps3DmOutput)
            {
                Card.Dmps3DmOutput dmOutput = card as Card.Dmps3DmOutput;
                Dmps3AudioOutputWithMixerBase output = new Dmps3AudioOutputWithMixerBase(card, dmOutput.OutputMixer);
                MasterVolumeLevel = new DmpsAudioOutputWithMixer(output, EDmpsLevelType.Master);
                SourceVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.Source);
                MicsMasterVolumeLevel = new DmpsAudioOutput(output, EDmpsLevelType.MicsMaster);
            }
        }

        private void BaseDevice_DMOutputChange(Switch device, DMOutputEventArgs args)
        {
            Debug.Console(2, this, "Dmps Audio Controller Event Output: {0} EventId: {1}", args.Number,
                args.EventId.ToString());
            switch (args.EventId)
            {
                case DMOutputEventIds.OutputVuFeedBackEventId:
                {
                    //Frequently called event that isn't needed
                    return;
                }
                case DMOutputEventIds.MasterVolumeFeedBackEventId:
                {
                    MasterVolumeLevel.VolumeLevelFeedback.FireUpdate();
                    MasterVolumeLevel.VolumeLevelScaledFeedback.FireUpdate();
                    break;
                }
                case DMOutputEventIds.MasterMuteOnFeedBackEventId:
                {
                    MasterVolumeLevel.MuteFeedback.FireUpdate();
                    break;
                }
                case DMOutputEventIds.SourceLevelFeedBackEventId:
                {
                    SourceVolumeLevel.VolumeLevelFeedback.FireUpdate();
                    SourceVolumeLevel.VolumeLevelScaledFeedback.FireUpdate();
                    break;
                }
                case DMOutputEventIds.SourceMuteOnFeedBackEventId:
                {
                    SourceVolumeLevel.MuteFeedback.FireUpdate();
                    break;
                }
                case DMOutputEventIds.MicMasterLevelFeedBackEventId:
                {
                    MicsMasterVolumeLevel.VolumeLevelFeedback.FireUpdate();
                    MicsMasterVolumeLevel.VolumeLevelScaledFeedback.FireUpdate();
                    break;
                }
                case DMOutputEventIds.MicMasterMuteOnFeedBackEventId:
                {
                    MicsMasterVolumeLevel.MuteFeedback.FireUpdate();
                    break;
                }
                case DMOutputEventIds.Codec1LevelFeedBackEventId:
                {
                    if (Codec1VolumeLevel != null)
                    {
                        Codec1VolumeLevel.VolumeLevelFeedback.FireUpdate();
                        Codec1VolumeLevel.VolumeLevelScaledFeedback.FireUpdate();
                    }

                    break;
                }
                case DMOutputEventIds.Codec1MuteOnFeedBackEventId:
                {
                    if (Codec1VolumeLevel != null)
                        Codec1VolumeLevel.MuteFeedback.FireUpdate();
                    break;
                }
                case DMOutputEventIds.Codec2LevelFeedBackEventId:
                {
                    if (Codec2VolumeLevel != null)
                    {
                        Codec2VolumeLevel.VolumeLevelFeedback.FireUpdate();
                        Codec2VolumeLevel.VolumeLevelScaledFeedback.FireUpdate();
                    }

                    break;
                }
                case DMOutputEventIds.Codec2MuteOnFeedBackEventId:
                {
                    if (Codec2VolumeLevel != null)
                        Codec2VolumeLevel.MuteFeedback.FireUpdate();
                    break;
                }
                case DMOutputEventIds.MinVolumeFeedBackEventId:
                {
                    Debug.Console(2, this, "MinVolumeFeedBackEventId: {0}", args.Index);
                    DmpsAudioOutputWithMixer level = MasterVolumeLevel as DmpsAudioOutputWithMixer;
                    if (level != null) level.GetVolumeMin();

                    break;
                }
                case DMOutputEventIds.MaxVolumeFeedBackEventId:
                {
                    Debug.Console(2, this, "MaxVolumeFeedBackEventId: {0}", args.Index);
                    DmpsAudioOutputWithMixer level = MasterVolumeLevel as DmpsAudioOutputWithMixer;
                    if (level != null) level.GetVolumeMax();

                    break;
                }
            }
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            DmpsAudioOutputControllerJoinMap joinMap = new DmpsAudioOutputControllerJoinMap(joinStart);

            if (bridge != null)
                bridge.AddJoinMap(Key, joinMap);
            else
                Debug.Console(0, this,
                    "Please update config to use 'eiscapiadvanced' to get all join map features for this device.");

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            if (MasterVolumeLevel != null)
            {
                SetUpDmpsAudioOutputJoins(trilist, MasterVolumeLevel, joinMap.MasterVolumeLevel.JoinNumber);
                DmpsAudioOutputWithMixer mixer = MasterVolumeLevel as DmpsAudioOutputWithMixer;
                if (mixer != null) trilist.SetUShortSigAction(joinMap.MixerPresetRecall.JoinNumber, mixer.RecallPreset);

                DmpsAudioOutputWithMixerAndEq eq = MasterVolumeLevel as DmpsAudioOutputWithMixerAndEq;
                if (eq != null) trilist.SetUShortSigAction(joinMap.MixerEqPresetRecall.JoinNumber, eq.RecallEqPreset);
            }

            if (SourceVolumeLevel != null)
                SetUpDmpsAudioOutputJoins(trilist, SourceVolumeLevel, joinMap.SourceVolumeLevel.JoinNumber);

            if (MicsMasterVolumeLevel != null)
                SetUpDmpsAudioOutputJoins(trilist, MicsMasterVolumeLevel, joinMap.MicsMasterVolumeLevel.JoinNumber);

            if (Codec1VolumeLevel != null)
                SetUpDmpsAudioOutputJoins(trilist, Codec1VolumeLevel, joinMap.Codec1VolumeLevel.JoinNumber);

            if (Codec2VolumeLevel != null)
                SetUpDmpsAudioOutputJoins(trilist, Codec2VolumeLevel, joinMap.Codec2VolumeLevel.JoinNumber);
        }

        private static void SetUpDmpsAudioOutputJoins(BasicTriList trilist, DmpsAudioOutput output, uint joinStart)
        {
            uint volumeLevelJoin = joinStart;
            uint volumeLevelScaledJoin = joinStart + 1;
            uint muteOnJoin = joinStart;
            uint muteOffJoin = joinStart + 1;
            uint volumeUpJoin = joinStart + 2;
            uint volumeDownJoin = joinStart + 3;
            uint sendScaledVolumeJoin = joinStart + 4;

            output.VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[volumeLevelJoin]);
            output.VolumeLevelScaledFeedback.LinkInputSig(trilist.UShortInput[volumeLevelScaledJoin]);

            trilist.SetSigTrueAction(muteOnJoin, output.MuteOn);
            output.MuteFeedback.LinkInputSig(trilist.BooleanInput[muteOnJoin]);
            trilist.SetSigTrueAction(muteOffJoin, output.MuteOff);
            output.MuteFeedback.LinkComplementInputSig(trilist.BooleanInput[muteOffJoin]);

            trilist.SetBoolSigAction(volumeUpJoin, output.VolumeUp);
            trilist.SetBoolSigAction(volumeDownJoin, output.VolumeDown);
            trilist.SetBoolSigAction(sendScaledVolumeJoin, output.SendScaledVolume);
            trilist.SetUShortSigAction(volumeLevelJoin, output.SetVolume);
            trilist.SetUShortSigAction(volumeLevelScaledJoin, output.SetVolumeScaled);
        }
    }

    public class DmpsAudioOutputWithMixerAndEq : DmpsAudioOutputWithMixer
    {
        private readonly CrestronControlSystem.Dmps3OutputEqualizer _eq;

        public DmpsAudioOutputWithMixerAndEq(Dmps3AudioOutputWithMixerBase output, EDmpsLevelType type,
            CrestronControlSystem.Dmps3OutputEqualizer eq)
            : base(output, type)
        {
            _eq = eq;
        }

        public void RecallEqPreset(ushort preset)
        {
            _eq.PresetNumber.UShortValue = preset;
            _eq.RecallPreset();
        }
    }

    public class DmpsAudioOutputWithMixer : DmpsAudioOutput
    {
        private readonly Dmps3AudioOutputWithMixerBase _output;

        public DmpsAudioOutputWithMixer(Dmps3AudioOutputWithMixerBase output, EDmpsLevelType type)
            : base(output, type)
        {
            _output = output;
            GetVolumeMax();
            GetVolumeMin();
        }

        public void GetVolumeMin()
        {
            MinLevel = (short)_output.MinVolumeFeedback.UShortValue;
            if (VolumeLevelScaledFeedback != null) VolumeLevelScaledFeedback.FireUpdate();
        }

        public void GetVolumeMax()
        {
            MaxLevel = (short)_output.MaxVolumeFeedback.UShortValue;
            if (VolumeLevelScaledFeedback != null) VolumeLevelScaledFeedback.FireUpdate();
        }

        public void RecallPreset(ushort preset)
        {
            _output.PresetNumber.UShortValue = preset;
            _output.RecallPreset();

            if (!Global.ControlSystemIsDmps4k3xxType)
                //Recall startup volume for main volume level as DMPS3(non-4K) presets don't affect the main volume
                RecallStartupVolume();
        }

        public void RecallStartupVolume()
        {
            ushort startupVol = _output.StartupVolumeFeedback.UShortValue;
            //Reset startup vol due to bug on DMPS3 where getting the value from above method clears the startup volume
            _output.StartupVolume.UShortValue = startupVol;
            Debug.Console(1, "DMPS Recalling Startup Volume {0}", startupVol);
            SetVolume(startupVol);
            MuteOff();
        }
    }

    public class DmpsAudioOutput : IBasicVolumeWithFeedback
    {
        private readonly UShortInputSig _level;
        private bool _enableVolumeSend;
        private ushort _volumeLevelInput;
        protected short MinLevel { get; set; }
        protected short MaxLevel { get; set; }

        public EDmpsLevelType Type { get; private set; }
        public BoolFeedback MuteFeedback { get; private set; }
        public IntFeedback VolumeLevelFeedback { get; private set; }
        public IntFeedback VolumeLevelScaledFeedback { get; private set; }

        private readonly Action _muteOnAction;
        private readonly Action _muteOffAction;
        private readonly Action<bool> _volumeUpAction;
        private readonly Action<bool> _volumeDownAction;

        public DmpsAudioOutput(Dmps3AudioOutputBase output, EDmpsLevelType type)
        {
            _volumeLevelInput = 0;
            _enableVolumeSend = false;
            Type = type;
            MinLevel = -800;
            MaxLevel = 100;

            switch (type)
            {
                case EDmpsLevelType.Master:
                {
                    _level = output.MasterVolume;
                    MuteFeedback = new BoolFeedback(() => output.MasterMuteOnFeedBack.BoolValue);
                    VolumeLevelFeedback = new IntFeedback(() => output.MasterVolumeFeedBack.UShortValue);
                    _muteOnAction = output.MasterMuteOn;
                    _muteOffAction = output.MasterMuteOff;
                    _volumeUpAction = (b) => output.MasterVolumeUp.BoolValue = b;
                    _volumeDownAction = (b) => output.MasterVolumeDown.BoolValue = b;
                    break;
                }
                case EDmpsLevelType.MicsMaster:
                {
                    if (output.Card is Card.Dmps3OutputBase)
                    {
                        Card.Dmps3OutputBase micOutput = output.Card as Card.Dmps3OutputBase;
                        if (micOutput != null)
                        {
                            _level = micOutput.MicMasterLevel;
                            MuteFeedback =
                                new BoolFeedback(() => micOutput.MicMasterMuteOnFeedBack.BoolValue);
                            VolumeLevelFeedback =
                                new IntFeedback(() => micOutput.MicMasterLevelFeedBack.UShortValue);
                            _muteOnAction = micOutput.MicMasterMuteOn;
                            _muteOffAction = micOutput.MicMasterMuteOff;
                            _volumeUpAction = (b) => micOutput.MicMasterLevelUp.BoolValue = b;
                            _volumeDownAction = (b) => micOutput.MicMasterLevelDown.BoolValue = b;
                        }
                    }

                    break;
                }
                case EDmpsLevelType.Source:
                {
                    _level = output.SourceLevel;
                    MuteFeedback = new BoolFeedback(() => output.SourceMuteOnFeedBack.BoolValue);
                    VolumeLevelFeedback = new IntFeedback(() => output.SourceLevelFeedBack.UShortValue);
                    _muteOnAction = output.SourceMuteOn;
                    _muteOffAction = output.SourceMuteOff;
                    _volumeUpAction = (b) => output.SourceLevelUp.BoolValue = b;
                    _volumeDownAction = (b) => output.SourceLevelDown.BoolValue = b;
                    break;
                }
                case EDmpsLevelType.Codec1:
                {
                    if (output.Card is Card.Dmps3ProgramOutput)
                    {
                        Card.Dmps3ProgramOutput programOutput = output.Card as Card.Dmps3ProgramOutput;
                        if (programOutput != null)
                        {
                            _level = programOutput.Codec1Level;
                            MuteFeedback =
                                new BoolFeedback(() => programOutput.CodecMute1OnFeedback.BoolValue);
                            VolumeLevelFeedback =
                                new IntFeedback(() => programOutput.Codec1LevelFeedback.UShortValue);
                            _muteOnAction = programOutput.Codec1MuteOn;
                            _muteOffAction = programOutput.Codec1MuteOff;
                            _volumeUpAction = (b) => programOutput.Codec1LevelUp.BoolValue = b;
                            _volumeDownAction = (b) => programOutput.Codec1LevelDown.BoolValue = b;
                        }
                    }
                    else if (output.Card is Card.Dmps3Aux2Output)
                    {
                        Card.Dmps3Aux2Output auxOutput = output.Card as Card.Dmps3Aux2Output;
                        if (auxOutput != null)
                        {
                            _level = auxOutput.Codec1Level;
                            MuteFeedback = new BoolFeedback(() => auxOutput.CodecMute1OnFeedback.BoolValue);
                            VolumeLevelFeedback =
                                new IntFeedback(() => auxOutput.Codec1LevelFeedback.UShortValue);
                            _muteOnAction = auxOutput.Codec1MuteOn;
                            _muteOffAction = auxOutput.Codec1MuteOff;
                            _volumeUpAction = (b) => auxOutput.Codec1LevelUp.BoolValue = b;
                            _volumeDownAction = (b) => auxOutput.Codec1LevelDown.BoolValue = b;
                        }
                    }

                    break;
                }
                case EDmpsLevelType.Codec2:
                {
                    if (output.Card is Card.Dmps3ProgramOutput)
                    {
                        Card.Dmps3ProgramOutput programOutput = output.Card as Card.Dmps3ProgramOutput;
                        if (programOutput != null)
                        {
                            _level = programOutput.Codec2Level;
                            MuteFeedback =
                                new BoolFeedback(() => programOutput.CodecMute1OnFeedback.BoolValue);
                            VolumeLevelFeedback =
                                new IntFeedback(() => programOutput.Codec2LevelFeedback.UShortValue);
                            _muteOnAction = programOutput.Codec2MuteOn;
                            _muteOffAction = programOutput.Codec2MuteOff;
                            _volumeUpAction = (b) => programOutput.Codec2LevelUp.BoolValue = b;
                            _volumeDownAction = (b) => programOutput.Codec2LevelDown.BoolValue = b;
                        }
                    }
                    else if (output.Card is Card.Dmps3Aux1Output)
                    {
                        Card.Dmps3Aux1Output auxOutput = output.Card as Card.Dmps3Aux1Output;

                        if (auxOutput != null)
                        {
                            _level = auxOutput.Codec2Level;
                            MuteFeedback = new BoolFeedback(() => auxOutput.CodecMute2OnFeedback.BoolValue);
                            VolumeLevelFeedback =
                                new IntFeedback(() => auxOutput.Codec2LevelFeedback.UShortValue);
                            _muteOnAction = auxOutput.Codec2MuteOn;
                            _muteOffAction = auxOutput.Codec2MuteOff;
                            _volumeUpAction = (b) => auxOutput.Codec2LevelUp.BoolValue = b;
                            _volumeDownAction = (b) => auxOutput.Codec2LevelDown.BoolValue = b;
                        }
                    }

                    break;
                }
            }

            if (VolumeLevelFeedback != null)
            {
                VolumeLevelScaledFeedback =
                    new IntFeedback(() => ScaleVolumeFeedback(VolumeLevelFeedback.UShortValue));
                VolumeLevelFeedback.FireUpdate();
                VolumeLevelScaledFeedback.FireUpdate();
            }
        }

        public void SetVolumeScaled(ushort level)
        {
            if (MuteFeedback.BoolValue) MuteOff();

            _volumeLevelInput = (ushort)(level * (MaxLevel - MinLevel) / ushort.MaxValue + MinLevel);
            if (_enableVolumeSend) _level.UShortValue = _volumeLevelInput;
        }

        public ushort ScaleVolumeFeedback(ushort level)
        {
            short signedLevel = (short)level;

            if (MaxLevel - MinLevel != 0)
                return (ushort)((signedLevel - MinLevel) * ushort.MaxValue / (MaxLevel - MinLevel));
            else
                return (ushort)MinLevel;
        }

        public void SendScaledVolume(bool pressRelease)
        {
            if (MuteFeedback.BoolValue) MuteOff();
            _enableVolumeSend = pressRelease;
            if (!pressRelease) SetVolumeScaled(_volumeLevelInput);
        }

        #region IBasicVolumeWithFeedback Members

        public void SetVolume(ushort level)
        {
            _level.UShortValue = level;
        }

        public void MuteOn()
        {
            _muteOnAction();
        }

        public void MuteOff()
        {
            _muteOffAction();
        }

        #endregion

        #region IBasicVolumeControls Members

        public void VolumeUp(bool pressRelease)
        {
            _volumeUpAction(pressRelease);
        }

        public void VolumeDown(bool pressRelease)
        {
            _volumeDownAction(pressRelease);
        }

        public void MuteToggle()
        {
            if (MuteFeedback.BoolValue)
                MuteOff();
            else
                MuteOn();
        }

        #endregion
    }

    public class Dmps3AudioOutputWithMixerBase : Dmps3AudioOutputBase
    {
        public UShortOutputSig MinVolumeFeedback { get; private set; }
        public UShortOutputSig MaxVolumeFeedback { get; private set; }
        public UShortInputSig StartupVolume { get; private set; }
        public UShortOutputSig StartupVolumeFeedback { get; private set; }
        public UShortInputSig PresetNumber { get; private set; }

        public Action RecallPreset { get; private set; }

        public Dmps3AudioOutputWithMixerBase(Card.Dmps3OutputBase card, CrestronControlSystem.Dmps3OutputMixer mixer)
            : base(card)
        {
            MinVolumeFeedback = mixer.MinVolumeFeedback;
            MaxVolumeFeedback = mixer.MaxVolumeFeedback;
            StartupVolume = mixer.StartupVolume;
            StartupVolumeFeedback = mixer.StartupVolumeFeedback;
            PresetNumber = mixer.PresetNumber;

            RecallPreset = mixer.RecallPreset;
        }

        public Dmps3AudioOutputWithMixerBase(Card.Dmps3OutputBase card,
            CrestronControlSystem.Dmps3AttachableOutputMixer mixer)
            : base(card)
        {
            MinVolumeFeedback = mixer.MinVolumeFeedback;
            MaxVolumeFeedback = mixer.MaxVolumeFeedback;
            StartupVolume = mixer.StartupVolume;
            StartupVolumeFeedback = mixer.StartupVolumeFeedback;
            PresetNumber = mixer.PresetNumber;

            RecallPreset = mixer.RecallPreset;
        }

        public Dmps3AudioOutputWithMixerBase(Card.Dmps3HdmiAudioOutput.Dmps3AudioOutputStream stream)
            : base(stream)
        {
            Dmps3AudioOutputMixer mixer = stream.OutputMixer;
            MinVolumeFeedback = mixer.MinVolumeFeedback;
            MaxVolumeFeedback = mixer.MaxVolumeFeedback;
            StartupVolume = mixer.StartupVolume;
            StartupVolumeFeedback = mixer.StartupVolumeFeedback;
            PresetNumber = stream.PresetNumber;
            RecallPreset = stream.RecallPreset;
        }

        public Dmps3AudioOutputWithMixerBase(Card.Dmps3HdmiAudioOutput.Dmps3DmHdmiOutputStream stream)
            : base(stream)
        {
            Dmps3DmHdmiOutputMixer mixer = stream.OutputMixer;
            MinVolumeFeedback = mixer.MinVolumeFeedback;
            MaxVolumeFeedback = mixer.MaxVolumeFeedback;
            StartupVolume = mixer.StartupVolume;
            StartupVolumeFeedback = mixer.StartupVolumeFeedback;
            PresetNumber = stream.PresetNumber;
            RecallPreset = stream.RecallPreset;
        }
    }

    public class Dmps3AudioOutputBase
    {
        public DMOutput Card { get; private set; }
        public BoolOutputSig MasterMuteOffFeedBack { get; private set; }
        public BoolOutputSig MasterMuteOnFeedBack { get; private set; }
        public UShortInputSig MasterVolume { get; private set; }
        public UShortOutputSig MasterVolumeFeedBack { get; private set; }
        public BoolInputSig MasterVolumeUp { get; private set; }
        public BoolInputSig MasterVolumeDown { get; private set; }
        public BoolOutputSig SourceMuteOffFeedBack { get; private set; }
        public BoolOutputSig SourceMuteOnFeedBack { get; private set; }
        public UShortInputSig SourceLevel { get; private set; }
        public UShortOutputSig SourceLevelFeedBack { get; private set; }
        public BoolInputSig SourceLevelUp { get; private set; }
        public BoolInputSig SourceLevelDown { get; private set; }

        public Action MasterMuteOff { get; private set; }
        public Action MasterMuteOn { get; private set; }
        public Action SourceMuteOff { get; private set; }
        public Action SourceMuteOn { get; private set; }

        protected Dmps3AudioOutputBase(Card.Dmps3OutputBase card)
        {
            Card = card;
            MasterMuteOffFeedBack = card.MasterMuteOffFeedBack;
            MasterMuteOnFeedBack = card.MasterMuteOnFeedBack;
            MasterVolume = card.MasterVolume;
            MasterVolumeFeedBack = card.MasterVolumeFeedBack;
            MasterVolumeUp = card.MasterVolumeUp;
            MasterVolumeDown = card.MasterVolumeDown;
            SourceMuteOffFeedBack = card.SourceMuteOffFeedBack;
            SourceMuteOnFeedBack = card.SourceMuteOnFeedBack;
            SourceLevel = card.SourceLevel;
            SourceLevelFeedBack = card.SourceLevelFeedBack;
            SourceLevelUp = card.SourceLevelUp;
            SourceLevelDown = card.SourceLevelDown;

            MasterMuteOff = card.MasterMuteOff;
            MasterMuteOn = card.MasterMuteOn;
            SourceMuteOff = card.SourceMuteOff;
            SourceMuteOn = card.SourceMuteOn;
        }

        protected Dmps3AudioOutputBase(Card.Dmps3HdmiAudioOutput.Dmps3AudioOutputStream stream)
        {
            MasterMuteOffFeedBack = stream.MasterMuteOffFeedBack;
            MasterMuteOnFeedBack = stream.MasterMuteOnFeedBack;
            MasterVolume = stream.MasterVolume;
            MasterVolumeFeedBack = stream.MasterVolumeFeedBack;
            MasterVolumeUp = stream.MasterVolumeUp;
            MasterVolumeDown = stream.MasterVolumeDown;
            SourceMuteOffFeedBack = stream.SourceMuteOffFeedBack;
            SourceMuteOnFeedBack = stream.SourceMuteOnFeedBack;
            SourceLevel = stream.SourceLevel;
            SourceLevelFeedBack = stream.SourceLevelFeedBack;
            SourceLevelUp = stream.SourceLevelUp;
            SourceLevelDown = stream.SourceLevelDown;

            MasterMuteOff = stream.MasterMuteOff;
            MasterMuteOn = stream.MasterMuteOn;
            SourceMuteOff = stream.SourceMuteOff;
            SourceMuteOn = stream.SourceMuteOn;
        }

        protected Dmps3AudioOutputBase(Card.Dmps3HdmiAudioOutput.Dmps3DmHdmiOutputStream stream)
        {
            MasterMuteOffFeedBack = stream.MasterMuteOffFeedBack;
            MasterMuteOnFeedBack = stream.MasterMuteOnFeedBack;
            MasterVolume = stream.MasterVolume;
            MasterVolumeFeedBack = stream.MasterVolumeFeedBack;
            MasterVolumeUp = stream.MasterVolumeUp;
            MasterVolumeDown = stream.MasterVolumeDown;
            SourceMuteOffFeedBack = stream.SourceMuteOffFeedBack;
            SourceMuteOnFeedBack = stream.SourceMuteOnFeedBack;
            SourceLevel = stream.SourceLevel;
            SourceLevelFeedBack = stream.SourceLevelFeedBack;
            SourceLevelUp = stream.SourceLevelUp;
            SourceLevelDown = stream.SourceLevelDown;

            MasterMuteOff = stream.MasterMuteOff;
            MasterMuteOn = stream.MasterMuteOn;
            SourceMuteOff = stream.SourceMuteOff;
            SourceMuteOn = stream.SourceMuteOn;
        }
    }

    public enum EDmpsLevelType
    {
        Master,
        Source,
        MicsMaster,
        Codec1,
        Codec2,
        Mic
    }
}