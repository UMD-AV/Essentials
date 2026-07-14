using System.Collections.Generic;
using PepperDash.Core;
using UmdEssentials.Core;
using UmdEssentials.Core.Config;
using UmdEssentials.Core.Routing;

namespace UmdEssentials.Devices.Common
{
    public class Amplifier : EssentialsDevice, IRoutingSink
    {
        public event SourceInfoChangeHandler CurrentSourceChange;

        public string CurrentSourceInfoKey { get; set; }

        public SourceListItem CurrentSourceInfo
        {
            get { return _CurrentSourceInfo; }
            set
            {
                if (value == _CurrentSourceInfo) return;

                SourceInfoChangeHandler handler = CurrentSourceChange;

                if (handler != null)
                    handler(_CurrentSourceInfo, ChangeType.WillChange);

                _CurrentSourceInfo = value;

                if (handler != null)
                    handler(_CurrentSourceInfo, ChangeType.DidChange);
            }
        }

        private SourceListItem _CurrentSourceInfo;

        public RoutingInputPort AudioIn { get; private set; }

        public Amplifier(string key, string name)
            : base(key, name)
        {
            AudioIn = new RoutingInputPort(RoutingPortNames.AnyAudioIn, eRoutingSignalType.Audio,
                eRoutingPortConnectionType.None, null, this);
            InputPorts = new RoutingPortCollection<RoutingInputPort> { AudioIn };
        }

        #region IRoutingInputs Members

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }

        #endregion
    }

    public class AmplifierFactory : EssentialsDeviceFactory<Amplifier>
    {
        public AmplifierFactory()
        {
            TypeNames = new List<string> { "amplifier" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Amplifier Device");
            return new Amplifier(dc.Key, dc.Name);
        }
    }
}