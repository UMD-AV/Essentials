using System;
using Crestron.SimplSharpPro.DM.Streaming;
using NvxEpi.Abstractions.Stream;
using NvxEpi.Devices;
using NvxEpi.Features.Streams.Video;
using PepperDash.Core;

namespace NvxEpi.Extensions
{
    public static class StreamExtensions
    {
        public static void ClearStream(this IStream device)
        {
            if (device.IsTransmitter)
                return;

            IStreamWithHardware deviceWithHardware = device as IStreamWithHardware;
            if (deviceWithHardware != null)
            {
                Debug.Console(1, device, "Clearing stream");
                deviceWithHardware.Hardware.Control.ServerUrl.StringValue = string.Empty;
                return;
            }

            NvxMockDevice mock = device as NvxMockDevice;
            if (mock != null)
            {
                mock.ClearStreamMock();
                return;
            }
        }

        public static void RouteStream(this IStream device, IStream tx)
        {
            if (device.IsTransmitter)
                throw new ArgumentException("device");

            if (tx == null)
            {
                device.ClearStream();
                return;
            }

            if (!tx.IsTransmitter)
                throw new ArgumentException("tx");

            Debug.Console(1, device, "Routing device stream : '{0}'", tx.Name);
            tx.StreamUrl.FireUpdate();

            if (string.IsNullOrEmpty(tx.StreamUrl.StringValue))
                device.ClearStream();
            else
            {
                IStreamWithHardware deviceWithHardware = device as IStreamWithHardware;
                if (deviceWithHardware != null)
                {
                    deviceWithHardware.SetStreamUrl(tx.StreamUrl.StringValue);
                    return;
                }

                NvxMockDevice mock = device as NvxMockDevice;
                if (mock != null)
                {
                    mock.SetStreamUrlMock(tx.StreamUrl.StringValue);
                    return;
                }
            }
        }

        public static void SetStreamUrl(this IStream device, string url)
        {
            if (device.IsTransmitter)
                return;

            Debug.Console(1, device, "Setting stream: '{0}'", url);

            IStreamWithHardware deviceWithHardware = device as IStreamWithHardware;
            if (deviceWithHardware != null)
            {
                deviceWithHardware.Hardware.Control.ServerUrl.StringValue = url;
                if (deviceWithHardware.Hardware is DmNvxD3x)
                {
                    Debug.Console(1, device, "Device is DmNvxE3x type, not able to route VideoSource");
                }
                else
                {
                    deviceWithHardware.Hardware.Control.VideoSource = eSfpVideoSourceTypes.Stream;
                }

                return;
            }

            NvxMockDevice mock = device as NvxMockDevice;
            if (mock != null)
            {
                mock.SetStreamUrlMock(url);
                return;
            }
        }
    }
}