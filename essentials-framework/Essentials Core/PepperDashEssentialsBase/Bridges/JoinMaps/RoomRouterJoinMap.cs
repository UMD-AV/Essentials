using System;

namespace PepperDash.Essentials.Core.Bridges
{
    public class RoomRouterJoinMap : JoinMapBaseAdvanced
    {
        //Digital

        [JoinName("OverflowModeOn")] public JoinDataComplete OverflowModeOn = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Room is in overflow mode", JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        //Analog

        [JoinName("RoomActionGo")] public JoinDataComplete RoomActionGo = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Fire a room action immediately",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Analog
            }
        );

        [JoinName("CodecInputFb")] public JoinDataComplete CodecInputFb = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Routing codec select input feedback",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Analog
            }
        );

        [JoinName("RoutingInput")] public JoinDataComplete RoutingInput = new JoinDataComplete(
            new JoinData { JoinNumber = 11, JoinSpan = 20 },
            new JoinMetadata
            {
                Description = "Routing destination select input and feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            }
        );

        [JoinName("DisplayInputFb")] public JoinDataComplete DisplayInputFb = new JoinDataComplete(
            new JoinData { JoinNumber = 31, JoinSpan = 16 },
            new JoinMetadata
            {
                Description = "Routing display select input feedback",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Analog
            }
        );

        //Serial

        [JoinName("SourceDevKey")] public JoinDataComplete SourceDevKey = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 32 },
            new JoinMetadata
            {
                Description = "Device keys for the sources",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("DestDevKey")] public JoinDataComplete DestDevKey = new JoinDataComplete(
            new JoinData { JoinNumber = 33, JoinSpan = 20 },
            new JoinMetadata
            {
                Description = "Device keys for the destinations",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentSource")] public JoinDataComplete CurrentSource = new JoinDataComplete(
            new JoinData { JoinNumber = 53, JoinSpan = 20 },
            new JoinMetadata
            {
                Description = "Current source feedback in text",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CodecInputCmd")] public JoinDataComplete CodecInputCmd = new JoinDataComplete(
            new JoinData { JoinNumber = 73, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Routing codec select input command",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            }
        );

        [JoinName("DisplayInputCmd")] public JoinDataComplete DisplayInputCmd = new JoinDataComplete(
            new JoinData { JoinNumber = 74, JoinSpan = 16 },
            new JoinMetadata
            {
                Description = "Routing display select input command",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            }
        );

        /// <summary>
        /// Constructor to use when instantiating this Join Map without inheriting from it
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        public RoomRouterJoinMap(uint joinStart)
            : this(joinStart, typeof(RoomRouterJoinMap))
        {
        }

        /// <summary>
        /// Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
        protected RoomRouterJoinMap(uint joinStart, Type type) : base(joinStart, type)
        {
        }
    }
}