using System;

namespace PepperDash.Essentials.Core.Bridges
{
    public class HdMdNxM4kEControllerJoinMap : JoinMapBaseAdvanced
    {
        [JoinName("Name")]
        public JoinDataComplete Name = new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata { Description = "Device Name", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });

        [JoinName("EnableAutoRoute")]
        public JoinDataComplete EnableAutoRoute = new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata { Description = "Enable Automatic Routing on 4x1 Switchers", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Digital });

        [JoinName("InputNames")]
        public JoinDataComplete InputNames = new JoinDataComplete(new JoinData { JoinNumber = 101, JoinSpan = 32 },
            new JoinMetadata { Description = "Input Names", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });

        [JoinName("OutputNames")]
        public JoinDataComplete OutputNames = new JoinDataComplete(new JoinData { JoinNumber = 301, JoinSpan = 32 },
            new JoinMetadata { Description = "Output Names", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });

        [JoinName("VideoSyncStatus")]
        public JoinDataComplete VideoSyncStatus = new JoinDataComplete(new JoinData { JoinNumber = 101, JoinSpan = 32 },
            new JoinMetadata { Description = "DM Input Video Sync", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Digital });

        [JoinName("OutputRoute")]
        public JoinDataComplete OutputRoute = new JoinDataComplete(new JoinData { JoinNumber = 101, JoinSpan = 32 },
            new JoinMetadata { Description = "Output Route Set / Get", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Analog });

        [JoinName("OutputCurrentInputNames")]
        public JoinDataComplete OutputCurrentInputNames = new JoinDataComplete(new JoinData { JoinNumber = 1201, JoinSpan = 32 },
            new JoinMetadata { Description = "Output Currently Routed Input Names", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });

        [JoinName("HdcpSupportState")]
        public JoinDataComplete HdcpSupportState = new JoinDataComplete(new JoinData { JoinNumber = 1001, JoinSpan = 32 },
            new JoinMetadata { Description = "DM Chassis Input HDCP Support State", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Analog });

        [JoinName("HdcpSupportCapability")]
        public JoinDataComplete HdcpSupportCapability = new JoinDataComplete(new JoinData { JoinNumber = 1201, JoinSpan = 32 },
            new JoinMetadata { Description = "DM Chassis Input HDCP Support Capability", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Analog });

        [JoinName("IsOnline")]
        public JoinDataComplete IsOnline = new JoinDataComplete(new JoinData { JoinNumber = 11, JoinSpan = 1 },
            new JoinMetadata { Description = "Device Onlne", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Digital });

        /// <summary>
        /// Constructor to use when instantiating this Join Map without inheriting from it
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        public HdMdNxM4kEControllerJoinMap(uint joinStart)
            : this(joinStart, typeof(HdMdNxM4kEControllerJoinMap))
        {
        }

        /// <summary>
        /// Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
        protected HdMdNxM4kEControllerJoinMap(uint joinStart, Type type)
            : base(joinStart, type)
        {
        }
    }
}