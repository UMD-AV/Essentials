using System;
using PepperDash.Essentials.Core;

namespace PepperDash_Essentials_Core.Bridges
{
    public class HdPsXxxControllerJoinMap : JoinMapBaseAdvanced
    {
        #region Digital

        [JoinName("IsOnline")] public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Onlne",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("InputSync")] public JoinDataComplete InputSync = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 100,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Device Input Sync",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("OutputEndpointOnline")] public JoinDataComplete OutputEndpointOnline = new JoinDataComplete(
            new JoinData { JoinNumber = 700, JoinSpan = 32 },
            new JoinMetadata
            {
                Description = "DM Chassis Output Endpoint Online", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        #endregion


        #region Analog

        [JoinName("OutputRoute")] public JoinDataComplete OutputRoute = new JoinDataComplete(
            new JoinData { JoinNumber = 100, JoinSpan = 32 },
            new JoinMetadata
            {
                Description = "DM Chassis Output Route Set / Get", JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        #endregion


        #region Serial

        [JoinName("Name")] public JoinDataComplete Name = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("InputNames")] public JoinDataComplete InputNames = new JoinDataComplete(
            new JoinData { JoinNumber = 100, JoinSpan = 32 },
            new JoinMetadata
            {
                Description = "DM Chassis Input Name", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("OutputNames")] public JoinDataComplete OutputNames = new JoinDataComplete(
            new JoinData { JoinNumber = 300, JoinSpan = 32 },
            new JoinMetadata
            {
                Description = "DM Chassis Output Name", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("InputVideoNames")] public JoinDataComplete InputVideoNames =
            new JoinDataComplete(new JoinData { JoinNumber = 500, JoinSpan = 32 },
                new JoinMetadata
                {
                    Description = "DM Chassis Video Input Names",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("OutputVideoNames")] public JoinDataComplete OutputVideoNames =
            new JoinDataComplete(new JoinData { JoinNumber = 900, JoinSpan = 32 },
                new JoinMetadata
                {
                    Description = "DM Chassis Video Output Names",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("OutputCurrentVideoInputNames")]
        public JoinDataComplete OutputCurrentVideoInputNames = new JoinDataComplete(
            new JoinData { JoinNumber = 1200, JoinSpan = 32 },
            new JoinMetadata
            {
                Description = "DM Chassis Video Output Currently Routed Video Input Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial
            });

        #endregion

        /// <summary>
        /// Constructor to use when instantiating this join map without inheriting from it
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        public HdPsXxxControllerJoinMap(uint joinStart)
            : this(joinStart, typeof(HdPsXxxControllerJoinMap))
        {
        }

        /// <summary>
        /// Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
        protected HdPsXxxControllerJoinMap(uint joinStart, Type type)
            : base(joinStart, type)
        {
        }
    }
}