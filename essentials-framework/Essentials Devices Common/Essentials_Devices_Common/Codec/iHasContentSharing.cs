using UmdEssentials.Core;


namespace UmdEssentials.Devices.Common.Codec
{
    public interface IHasContentSharing
    {
        BoolFeedback SharingContentIsOnFeedback { get; }
        StringFeedback SharingSourceFeedback { get; }

        bool AutoShareContentWhileInCall { get; }

        void StartSharing();
        void StopSharing();
    }
}