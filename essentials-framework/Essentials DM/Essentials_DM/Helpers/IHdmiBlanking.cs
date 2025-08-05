using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.DM
{
    public interface IHdmiBlanking
    {
        BoolFeedback HdmiOutputBlankedFeedback { get; }
        void BlankOutput();
        void UnblankOutput();
    }
}