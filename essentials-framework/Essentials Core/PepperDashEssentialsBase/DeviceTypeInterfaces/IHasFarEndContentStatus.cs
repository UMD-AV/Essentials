namespace UmdEssentials.Core.DeviceTypeInterfaces
{
    public interface IHasFarEndContentStatus
    {
        BoolFeedback ReceivingContent { get; }
    }
}