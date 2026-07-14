using UmdEssentials.Core;

namespace UmdEssentials.DM
{
    public interface IHdmiBlanking
    {
        BoolFeedback HdmiOutputBlankedFeedback { get; }
        void BlankOutput();
        void UnblankOutput();
    }
}