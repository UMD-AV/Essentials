using System;

namespace PepperDash.Essentials.Core.Bridges
{
    public class RoutingInterfaceJoinMap : JoinMapBaseAdvanced
    {
        //Digital

        [JoinName("SourceVisible")] public JoinDataComplete SourceVisible = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 32 },
            new JoinMetadata
            {
                Description = "Source visible feedback", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("DestVisible")] public JoinDataComplete DestVisible = new JoinDataComplete(
            new JoinData { JoinNumber = 33, JoinSpan = 16 },
            new JoinMetadata
            {
                Description = "Dest visible feedback", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("DestEnable")] public JoinDataComplete DestEnable = new JoinDataComplete(
            new JoinData { JoinNumber = 49, JoinSpan = 16 },
            new JoinMetadata
            {
                Description = "Dest enable feedback", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("SourceAudioVisible")] public JoinDataComplete SourceAudioVisible = new JoinDataComplete(
            new JoinData { JoinNumber = 65, JoinSpan = 32 },
            new JoinMetadata
            {
                Description = "Source audio visible feedback", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("SourceContentVisible")] public JoinDataComplete SourceContentVisible = new JoinDataComplete(
            new JoinData { JoinNumber = 97, JoinSpan = 32 },
            new JoinMetadata
            {
                Description = "Source content visible feedback", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        //Analog

        [JoinName("SourceSelect")] public JoinDataComplete SourceSelect = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Source select set and feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            }
        );

        [JoinName("DestSelect")] public JoinDataComplete DestSelect = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Destination select",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Analog
            }
        );

        [JoinName("AdvancedMode")] public JoinDataComplete AdvancedMode = new JoinDataComplete(
            new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Advanced mode set and feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            }
        );

        [JoinName("OverflowMode")] public JoinDataComplete OverflowMode = new JoinDataComplete(
            new JoinData { JoinNumber = 4, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Overflow mode set",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            }
        );

        [JoinName("OverridePreview")] public JoinDataComplete OverridePreview = new JoinDataComplete(
            new JoinData { JoinNumber = 5, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Override preview window route set",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Analog
            }
        );

        [JoinName("RoomSelect")] public JoinDataComplete RoomSelect = new JoinDataComplete(
            new JoinData { JoinNumber = 6, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Room selection from simpl",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Analog
            });

        //Serial

        [JoinName("AddVisibilityMode")] public JoinDataComplete AddVisibilityMode = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Add a visibility mode",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("RemoveVisibilityMode")] public JoinDataComplete RemoveVisibilityMode = new JoinDataComplete(
            new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Remove a visibility mode",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("SourceName")] public JoinDataComplete SourceName = new JoinDataComplete(
            new JoinData { JoinNumber = 4, JoinSpan = 32 },
            new JoinMetadata
            {
                Description = "Source name text",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("DestName")] public JoinDataComplete DestName = new JoinDataComplete(
            new JoinData { JoinNumber = 36, JoinSpan = 16 },
            new JoinMetadata
            {
                Description = "Destination name text",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("DestRouteName")] public JoinDataComplete DestRouteName = new JoinDataComplete(
            new JoinData { JoinNumber = 52, JoinSpan = 16 },
            new JoinMetadata
            {
                Description = "Destination route name text",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("SourceDeviceKey")] public JoinDataComplete SourceDeviceKey = new JoinDataComplete(
            new JoinData { JoinNumber = 68, JoinSpan = 32 },
            new JoinMetadata
            {
                Description = "Source device key",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            }
        );

        [JoinName("SourceVideoSyncKey")] public JoinDataComplete SourceVideoSyncKey = new JoinDataComplete(
            new JoinData { JoinNumber = 100, JoinSpan = 32 },
            new JoinMetadata
            {
                Description = "Source video sync key",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            }
        );

        /// <summary>
        /// Constructor to use when instantiating this Join Map without inheriting from it
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        public RoutingInterfaceJoinMap(uint joinStart)
            : this(joinStart, typeof(RoutingInterfaceJoinMap))
        {
        }

        /// <summary>
        /// Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
        protected RoutingInterfaceJoinMap(uint joinStart, Type type) : base(joinStart, type)
        {
        }
    }
}