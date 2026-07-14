using NvxEpi.Abstractions;
using NvxEpi.Enums;
using UmdEssentials.Core;

namespace NvxEpi.Services.InputPorts
{
    public class SecondaryAudioInput
    {
        public static void AddRoutingPort(INvxDevice device)
        {
            RoutingInputPort port = new RoutingInputPort(
                DeviceInputEnum.SecondaryAudio.Name,
                eRoutingSignalType.Audio,
                eRoutingPortConnectionType.Streaming,
                DeviceInputEnum.SecondaryAudio,
                device);

            device.InputPorts.Add(port);
        }
    }
}