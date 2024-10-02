using PepperDash.Essentials.Core;

namespace ExtronDmp
{
    /// <summary>
    /// Extron DMP Join Map
    /// </summary>
    public class ExtronDmpJoinMap : JoinMapBaseAdvanced
    {
        #region Digital

        [JoinName("IsOnline")] public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Is Online",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PresetRecall")] public JoinDataComplete PresetRecall = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 100,
                JoinSpan = 100
            },
            new JoinMetadata()
            {
                Description = "Preset Recall",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("EnableLevelSend")] public JoinDataComplete EnableLevelSend =
            new JoinDataComplete(new JoinData { JoinNumber = 200, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Enable Level Sending from SIMPL",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ChannelVisible")] public JoinDataComplete ChannelVisible = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 200,
                JoinSpan = 200
            },
            new JoinMetadata()
            {
                Description = "Channel Visible",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelMuteToggle")] public JoinDataComplete ChannelMuteToggle = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 400,
                JoinSpan = 200
            },
            new JoinMetadata()
            {
                Description = "Channel Mute Toggle",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelMuteOn")] public JoinDataComplete ChannelMuteOn = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 600,
                JoinSpan = 200
            },
            new JoinMetadata()
            {
                Description = "Channel Mute On",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelMuteOff")] public JoinDataComplete ChannelMuteOff = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 800,
                JoinSpan = 200
            },
            new JoinMetadata()
            {
                Description = "Channel Mute Off",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelVolumeUp")] public JoinDataComplete ChannelVolumeUp = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1000,
                JoinSpan = 200
            },
            new JoinMetadata()
            {
                Description = "Channel Volume Up",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelVolumeDown")] public JoinDataComplete ChannelVolumeDown = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1200,
                JoinSpan = 200
            },
            new JoinMetadata()
            {
                Description = "Channel Volume Down",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        #endregion

        #region Analog

        [JoinName("ChannelVolume")] public JoinDataComplete ChannelVolume = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 200,
                JoinSpan = 200
            },
            new JoinMetadata()
            {
                Description = "Channel Volume",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("ChannelType")] public JoinDataComplete ChannelType = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 400,
                JoinSpan = 200
            },
            new JoinMetadata()
            {
                Description = "Channel Type",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        #endregion

        #region Serial

        [JoinName("PresetName")] public JoinDataComplete PresetName = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 100,
                JoinSpan = 100
            },
            new JoinMetadata()
            {
                Description = "Preset Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("ChannelName")] public JoinDataComplete ChannelName = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 200,
                JoinSpan = 200
            },
            new JoinMetadata()
            {
                Description = "Channel Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        #endregion

        [JoinName("IncomingCall")] public JoinDataComplete IncomingCall =
            new JoinDataComplete(new JoinData { JoinNumber = 3100, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Call Incoming",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Answer")] public JoinDataComplete Answer =
            new JoinDataComplete(new JoinData { JoinNumber = 3106, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Answer Incoming Call",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("EndCall")] public JoinDataComplete EndCall =
            new JoinDataComplete(new JoinData { JoinNumber = 3107, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "End Call",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadNumeric")] public JoinDataComplete KeyPadNumeric =
            new JoinDataComplete(new JoinData { JoinNumber = 3110, JoinSpan = 10 },
                new JoinMetadata
                {
                    Description = "Keypad Digits 0-9",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadStar")] public JoinDataComplete KeyPadStar =
            new JoinDataComplete(new JoinData { JoinNumber = 3120, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Keypad *",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadPound")] public JoinDataComplete KeyPadPound =
            new JoinDataComplete(new JoinData { JoinNumber = 3121, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Keypad #",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadClear")] public JoinDataComplete KeyPadClear =
            new JoinDataComplete(new JoinData { JoinNumber = 3122, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Keypad Clear",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadBackspace")] public JoinDataComplete KeyPadBackspace =
            new JoinDataComplete(new JoinData { JoinNumber = 3123, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Keypad Backspace",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadDial")] public JoinDataComplete KeyPadDial =
            new JoinDataComplete(new JoinData { JoinNumber = 3124, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Keypad Dial and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("AutoAnswerOn")] public JoinDataComplete AutoAnswerOn =
            new JoinDataComplete(new JoinData { JoinNumber = 3125, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Auto Answer On and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("AutoAnswerOff")] public JoinDataComplete AutoAnswerOff =
            new JoinDataComplete(new JoinData { JoinNumber = 3126, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Auto Answer Off and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("AutoAnswerToggle")] public JoinDataComplete AutoAnswerToggle =
            new JoinDataComplete(new JoinData { JoinNumber = 3127, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Auto Answer Toggle and On Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("OnHook")] public JoinDataComplete OnHook =
            new JoinDataComplete(new JoinData { JoinNumber = 3129, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "On Hook Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("OffHook")] public JoinDataComplete OffHook =
            new JoinDataComplete(new JoinData { JoinNumber = 3130, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Off Hook Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("DoNotDisturbToggle")] public JoinDataComplete DoNotDisturbToggle =
            new JoinDataComplete(new JoinData { JoinNumber = 3132, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Do Not Disturb Toggle and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("DoNotDisturbOn")] public JoinDataComplete DoNotDisturbOn =
            new JoinDataComplete(new JoinData { JoinNumber = 3133, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Do Not Disturb On Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("DoNotDisturbOff")] public JoinDataComplete DoNotDisturbOff =
            new JoinDataComplete(new JoinData { JoinNumber = 3134, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Do Not Disturb Of Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("CallState")] public JoinDataComplete CallState =
            new JoinDataComplete(new JoinData { JoinNumber = 3100, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Call State Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("DialString")] public JoinDataComplete DialString =
            new JoinDataComplete(new JoinData { JoinNumber = 3100, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Dial String Send and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("Label")] public JoinDataComplete Label =
            new JoinDataComplete(new JoinData { JoinNumber = 3101, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Dialer Label",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("LastNumberDialerFb")] public JoinDataComplete LastNumberDialerFb =
            new JoinDataComplete(new JoinData { JoinNumber = 3102, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Last Number Dialed Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("CallerIdNumberFb")] public JoinDataComplete CallerIdNumberFb =
            new JoinDataComplete(new JoinData { JoinNumber = 3104, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Caller ID Number",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("CallerIdNameFb")] public JoinDataComplete CallerIdNameFb =
            new JoinDataComplete(new JoinData { JoinNumber = 3105, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Caller ID Name",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("DisplayNumber")] public JoinDataComplete DisplayNumber =
            new JoinDataComplete(new JoinData { JoinNumber = 3106, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "This Line's Number",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        public ExtronDmpJoinMap(uint joinStart)
            : base(joinStart, typeof(ExtronDmpJoinMap))
        {
        }
    }
}