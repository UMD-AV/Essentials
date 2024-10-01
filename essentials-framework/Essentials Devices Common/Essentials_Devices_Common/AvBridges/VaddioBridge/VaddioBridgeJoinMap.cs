using PepperDash.Essentials.Core;

namespace VaddioBridgePlugin
{
    public class VaddioBridgeJoinMap : JoinMapBaseAdvanced
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
                Description = "Is Online Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PowerOn")] public JoinDataComplete PowerOn = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 7,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Power On",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PowerOff")] public JoinDataComplete PowerOff = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 8,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Power Off",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PipOn")] public JoinDataComplete PipOn = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 11,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Pip On Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PipOff")] public JoinDataComplete PipOff = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 12,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Pip Off Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PipToggle")] public JoinDataComplete PipToggle = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 13,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Pip Toggle Layouts",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PipUpperLeft")] public JoinDataComplete PipUpperLeft = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 14,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Pip Upper Left Get/Set",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PipUpperRight")] public JoinDataComplete PipUpperRight = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 15,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Pip Upper Right Get/Set",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PipLowerLeftt")] public JoinDataComplete PipLowerLeft = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 16,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Pip Lower Left Get/Set",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PipLowerRight")] public JoinDataComplete PipLowerRight = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 17,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Pip Lower Right Get/Set",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PipTopBottom")] public JoinDataComplete PipTopBottom = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 18,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Pip Top Bottom Split Get/Set",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PipLeftRight")] public JoinDataComplete PipLeftRight = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 19,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Pip Left Right Split Get/Set",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        #endregion


        #region Analog

        [JoinName("VideoSource")] public JoinDataComplete VideoSource = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 11,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Video Source Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        #endregion


        #region Serial

        [JoinName("DeviceName")] public JoinDataComplete DeviceName = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("IpAddress")] public JoinDataComplete IpAddress = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Ip Address Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("FirmwareVersion")] public JoinDataComplete FirmwareVersion = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 3,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Firmware Version Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        #endregion

        public VaddioBridgeJoinMap(uint joinStart)
            : base(joinStart, typeof(VaddioBridgeJoinMap))
        {
        }
    }
}