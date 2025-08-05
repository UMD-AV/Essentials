using Crestron.SimplSharpPro.DM;
using PepperDash.Essentials.Core;
using PepperDash.Core;

namespace PepperDash.Essentials.DM
{
    public class RoutingOutputPortWithBlanking : RoutingOutputPort, IHdmiBlanking
    {
        private readonly OutputCardHdmiOutBasicPort _output;

        public RoutingOutputPortWithBlanking(string key, eRoutingSignalType type, eRoutingPortConnectionType connType,
            object selector,
            IRoutingOutputs parent, OutputCardHdmiOutBasicPort output) : base(key, type, connType,
            selector,
            parent)
        {
            _output = output;
            HdmiOutputBlankedFeedback = new BoolFeedback(() => _output.BlankEnabledFeedback.BoolValue);
        }

        public RoutingOutputPortWithBlanking(string key, eRoutingSignalType type, eRoutingPortConnectionType connType,
            object selector,
            IRoutingOutputs parent, bool isInternal, OutputCardHdmiOutBasicPort output) : base(key,
            type, connType,
            selector, parent, isInternal)
        {
            _output = output;
            HdmiOutputBlankedFeedback = new BoolFeedback(() => _output.BlankEnabledFeedback.BoolValue);
        }

        public BoolFeedback HdmiOutputBlankedFeedback { get; private set; }

        public void BlankOutput()
        {
            _output.BlankEnabled();
        }

        public void UnblankOutput()
        {
            _output.BlankDisabled();
        }
    }
}