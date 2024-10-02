using System;
using System.Collections.Generic;
using System.Linq;
using NvxEpi.Abstractions;
using NvxEpi.Abstractions.SecondaryAudio;
using NvxEpi.Abstractions.Stream;
using NvxEpi.Enums;
using NvxEpi.Features.Routing;
using NvxEpi.Services.InputSwitching;
using PepperDash.Essentials.Core;

namespace NvxEpi.Services.TieLines
{
    public class TieLineConnector
    {
        public static void AddTieLinesForTransmitters(IEnumerable<INvxDevice> transmitters)
        {
            foreach (INvxDevice item in transmitters)
            {
                INvxDevice tx = item;
                RoutingOutputPort outputPort = tx.OutputPorts[SwitcherForStreamOutput.Key];
                if (outputPort == null)
                    throw new NullReferenceException("outputPort");

                IStream stream = tx as IStream;

                RoutingInputPort streamInput = NvxGlobalRouter
                    .Instance
                    .PrimaryStreamRouter
                    .InputPorts[PrimaryStreamRouter.GetInputPortKeyForTx(stream)];

                if (streamInput == null)
                    throw new NullReferenceException("PrimaryRouterStreamInput");

                TieLineCollection.Default.Add(new TieLine(outputPort, streamInput, eRoutingSignalType.AudioVideo));
            }
        }

        public static void AddTieLinesForReceivers(IEnumerable<INvxDevice> receivers)
        {
            foreach (INvxDevice item in receivers)
            {
                INvxDevice rx = item;
                RoutingInputPort inputPort = rx.InputPorts[DeviceInputEnum.Stream.Name];
                if (inputPort == null)
                    throw new NullReferenceException("inputPort");

                IStream stream = rx as IStream;

                RoutingOutputPort streamOutput = NvxGlobalRouter
                    .Instance
                    .PrimaryStreamRouter
                    .OutputPorts[PrimaryStreamRouter.GetOutputPortKeyForRx(stream)];

                if (streamOutput == null)
                    throw new NullReferenceException("PrimaryRouterStreamOutput");

                TieLineCollection.Default.Add(new TieLine(streamOutput, inputPort, eRoutingSignalType.AudioVideo));
            }
        }

        public static void AddTieLinesForAudioTransmitters(IEnumerable<INvxDevice> transmitters)
        {
            foreach (ISecondaryAudioStream secondaryAudio in transmitters.OfType<ISecondaryAudioStream>())
            {
                RoutingOutputPort secondaryAudioPort = secondaryAudio.OutputPorts[SwitcherForSecondaryAudioOutput.Key];
                if (secondaryAudioPort == null)
                    throw new NullReferenceException("secondaryAudioInput");

                RoutingInputPort secondaryAudioInput = NvxGlobalRouter
                    .Instance
                    .SecondaryAudioRouter
                    .InputPorts[SecondaryAudioRouter.GetInputPortKeyForTx(secondaryAudio)];

                if (secondaryAudioInput == null)
                    throw new NullReferenceException("SecondaryAudioStreamInput");

                TieLineCollection.Default.Add(new TieLine(secondaryAudioPort, secondaryAudioInput,
                    eRoutingSignalType.Audio));
            }
        }

        public static void AddTieLinesForAudioReceivers(IEnumerable<INvxDevice> receivers)
        {
            foreach (ISecondaryAudioStream secondaryAudio in receivers.OfType<ISecondaryAudioStream>())
            {
                RoutingInputPort secondaryAudioPort = secondaryAudio.InputPorts[DeviceInputEnum.SecondaryAudio.Name];
                if (secondaryAudioPort == null)
                    throw new NullReferenceException("SecondaryRouterInput");

                RoutingOutputPort secondaryAudioStreamOutput = NvxGlobalRouter
                    .Instance
                    .SecondaryAudioRouter
                    .OutputPorts[SecondaryAudioRouter.GetOutputPortKeyForRx(secondaryAudio)];

                if (secondaryAudioStreamOutput == null)
                    throw new NullReferenceException("SecondaryRouterStreamInput");

                TieLineCollection.Default.Add(new TieLine(secondaryAudioStreamOutput, secondaryAudioPort,
                    eRoutingSignalType.Audio));
            }
        }
    }
}