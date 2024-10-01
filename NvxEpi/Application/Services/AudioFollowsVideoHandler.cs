using System.Collections.Generic;
using System.Linq;
using NvxEpi.Abstractions;
using NvxEpi.Features.Routing;
using PepperDash.Essentials.Core;

namespace NvxEpi.Application.Services
{
    public class AudioFollowsVideoHandler
    {
        private readonly Dictionary<int, INvxDevice> _transmitters;
        private readonly Dictionary<int, INvxDevice> _receivers;

        public AudioFollowsVideoHandler(Dictionary<int, INvxDevice> transmitters, Dictionary<int, INvxDevice> receivers)
        {
            _transmitters = transmitters;
            _receivers = receivers;
        }

        public void SetAudioFollowsVideoTrue()
        {
            foreach (KeyValuePair<int, INvxDevice> transmitter in _transmitters)
            {
                INvxDevice tx = transmitter.Value;

                foreach (TieLine tieLine in TieLineCollection
                             .Default
                             .Where(tieLine =>
                                 tieLine.DestinationPort.ParentDevice.Key.Equals(NvxGlobalRouter.Instance
                                     .PrimaryStreamRouter.Key))
                             .Where(tieLine => tieLine.SourcePort.ParentDevice.Key.Equals(tx.Key)))
                {
                    tieLine.OverrideType = eRoutingSignalType.AudioVideo;
                }
            }

            foreach (KeyValuePair<int, INvxDevice> receiver in _receivers)
            {
                INvxDevice rx = receiver.Value;

                foreach (TieLine tieLine in TieLineCollection
                             .Default
                             .Where(tieLine =>
                                 tieLine.SourcePort.ParentDevice.Key.Equals(NvxGlobalRouter.Instance.PrimaryStreamRouter
                                     .Key))
                             .Where(tieLine => tieLine.DestinationPort.ParentDevice.Key.Equals(rx.Key)))
                {
                    tieLine.OverrideType = eRoutingSignalType.AudioVideo;
                }
            }
        }

        public void SetAudioFollowsVideoFalse()
        {
            foreach (KeyValuePair<int, INvxDevice> transmitter in _transmitters)
            {
                INvxDevice tx = transmitter.Value;

                foreach (TieLine tieLine in TieLineCollection
                             .Default
                             .Where(tieLine =>
                                 tieLine.DestinationPort.ParentDevice.Key.Equals(NvxGlobalRouter.Instance
                                     .PrimaryStreamRouter.Key))
                             .Where(tieLine => tieLine.SourcePort.ParentDevice.Key.Equals(tx.Key)))
                {
                    tieLine.OverrideType = eRoutingSignalType.Video;
                }
            }

            foreach (KeyValuePair<int, INvxDevice> receiver in _receivers)
            {
                INvxDevice rx = receiver.Value;

                foreach (TieLine tieLine in TieLineCollection
                             .Default
                             .Where(tieLine =>
                                 tieLine.SourcePort.ParentDevice.Key.Equals(NvxGlobalRouter.Instance.PrimaryStreamRouter
                                     .Key))
                             .Where(tieLine => tieLine.DestinationPort.ParentDevice.Key.Equals(rx.Key)))
                {
                    tieLine.OverrideType = eRoutingSignalType.Video;
                }
            }
        }
    }
}