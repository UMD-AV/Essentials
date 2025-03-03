using System;

namespace PepperDash.Essentials.Core.Bridges
{
    public class RoomJoinMap : JoinMapBaseAdvanced
    {
        //Digital

        [JoinName("WallplateCapable")] public JoinDataComplete WallplateCapable = new JoinDataComplete(
            new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Room has wallplates to show/hide from tech page",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            }
        );

        [JoinName("ServiceNowEnable")] public JoinDataComplete ServiceNowEnable = new JoinDataComplete(
            new JoinData { JoinNumber = 4, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Enable ServiceNow help desk feature",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            }
        );

        [JoinName("PhysicsDivisible")] public JoinDataComplete PhysicsDivisible = new JoinDataComplete(
            new JoinData { JoinNumber = 5, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Room is part of the Physics Divisible special room type",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            }
        );

        [JoinName("AdvancedModeDefaultOn")] public JoinDataComplete AdvancedModeDefaultOn = new JoinDataComplete(
            new JoinData { JoinNumber = 6, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Advanced mode is enabled by default when room starts",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            }
        );

        [JoinName("AdvancedModeToggleVisible")]
        public JoinDataComplete AdvancedModeToggleVisible = new JoinDataComplete(
            new JoinData { JoinNumber = 7, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Show the advanced mode toggle on the UI for this room",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            }
        );

        [JoinName("CameraHide")] public JoinDataComplete CameraHide = new JoinDataComplete(
            new JoinData { JoinNumber = 21, JoinSpan = 8 },
            new JoinMetadata
            {
                Description = "Hide the camera on the UI",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            }
        );

        [JoinName("FaderVisibleUser")] public JoinDataComplete FaderVisibleUser = new JoinDataComplete(
            new JoinData { JoinNumber = 101, JoinSpan = 40 },
            new JoinMetadata
            {
                Description = "Show the audio fader on the user UI",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            }
        );

        [JoinName("FaderVisibleTech")] public JoinDataComplete FaderVisibleTech = new JoinDataComplete(
            new JoinData { JoinNumber = 141, JoinSpan = 40 },
            new JoinMetadata
            {
                Description = "Show the audio fader on the technician UI",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            }
        );

        //Analog

        [JoinName("OccShutdownTimeoutMinutes")]
        public JoinDataComplete OccShutdownTimeoutMinutes = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Default timeout in minutes for idle shutdown logic (fusion can override)",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("DefaultSystemPreset")] public JoinDataComplete DefaultSystemPreset = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Default audio preset for room",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("DefaultMicPreset")] public JoinDataComplete DefaultMicPreset = new JoinDataComplete(
            new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Default microphone preset for room",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("NumberOfMicBatteries")] public JoinDataComplete NumberOfMicBatteries = new JoinDataComplete(
            new JoinData { JoinNumber = 4, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Number of mic batteries to monitor for (fusion can override)",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("OccShutdownEnable")] public JoinDataComplete OccShutdownEnable = new JoinDataComplete(
            new JoinData { JoinNumber = 5, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Occupancy shutdown enable feedback from SIMPL",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("CameraSource")] public JoinDataComplete CameraSource = new JoinDataComplete(
            new JoinData { JoinNumber = 21, JoinSpan = 8 },
            new JoinMetadata
            {
                Description = "Routing source index for each camera device",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        //Serial

        [JoinName("RoomName")] public JoinDataComplete RoomName = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Room name (fusion can override)",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("HelpText")] public JoinDataComplete HelpText = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Text for help menu if not ServiceNow enabled",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("LightsKey")] public JoinDataComplete LightsKey = new JoinDataComplete(
            new JoinData { JoinNumber = 3, JoinSpan = 2 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("FusionKey")] public JoinDataComplete FusionKey = new JoinDataComplete(
            new JoinData { JoinNumber = 5, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("OccSensorKey")] public JoinDataComplete OccSensorKey = new JoinDataComplete(
            new JoinData { JoinNumber = 6, JoinSpan = 2 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CodecKey")] public JoinDataComplete CodecKey = new JoinDataComplete(
            new JoinData { JoinNumber = 8, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("AvBridgeKey")] public JoinDataComplete AvBridgeKey = new JoinDataComplete(
            new JoinData { JoinNumber = 9, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CameraControllerKey")] public JoinDataComplete CameraControllerKey = new JoinDataComplete(
            new JoinData { JoinNumber = 10, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("ShadesKey")] public JoinDataComplete ShadesKey = new JoinDataComplete(
            new JoinData { JoinNumber = 11, JoinSpan = 2 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MainFaderKey")] public JoinDataComplete MainFaderKey = new JoinDataComplete(
            new JoinData { JoinNumber = 13, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("PrivacyFaderKey")] public JoinDataComplete PrivacyFaderKey = new JoinDataComplete(
            new JoinData { JoinNumber = 14, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("LecternMicFaderKey")] public JoinDataComplete LecternMicFaderKey = new JoinDataComplete(
            new JoinData { JoinNumber = 15, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("OverflowInFaderKey")] public JoinDataComplete OverflowInFaderKey = new JoinDataComplete(
            new JoinData { JoinNumber = 16, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("OverflowOutFaderKey")] public JoinDataComplete OverflowOutFaderKey = new JoinDataComplete(
            new JoinData { JoinNumber = 17, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("OverflowKey")] public JoinDataComplete OverflowKey = new JoinDataComplete(
            new JoinData { JoinNumber = 18, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("ScheduleKey")] public JoinDataComplete ScheduleKey = new JoinDataComplete(
            new JoinData { JoinNumber = 19, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CameraKey")] public JoinDataComplete CameraKey = new JoinDataComplete(
            new JoinData { JoinNumber = 20, JoinSpan = 8 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MicDockKey")] public JoinDataComplete MicDockKey = new JoinDataComplete(
            new JoinData { JoinNumber = 28, JoinSpan = 2 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MicRxKey")] public JoinDataComplete MicRxKey = new JoinDataComplete(
            new JoinData { JoinNumber = 30, JoinSpan = 4 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("RecorderKey")] public JoinDataComplete RecorderKey = new JoinDataComplete(
            new JoinData { JoinNumber = 34, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device keys for various devices",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("FaderName")] public JoinDataComplete FaderName = new JoinDataComplete(
            new JoinData { JoinNumber = 101, JoinSpan = 40 },
            new JoinMetadata
            {
                Description = "UI names for audio faders",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("FaderKey")] public JoinDataComplete FaderKey = new JoinDataComplete(
            new JoinData { JoinNumber = 141, JoinSpan = 40 },
            new JoinMetadata
            {
                Description = "Dsp keys for audio faders",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        /// Constructor to use when instantiating this Join Map without inheriting from it
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        public RoomJoinMap(uint joinStart)
            : this(joinStart, typeof(RoomJoinMap))
        {
        }

        /// <summary>
        /// Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
        protected RoomJoinMap(uint joinStart, Type type) : base(joinStart, type)
        {
        }
    }
}