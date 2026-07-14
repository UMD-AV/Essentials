using UmdEssentials.Core;

namespace UmdEssentials.Devices.Common.VideoCodec.Interfaces
{
    public interface IHasMeetingLock
    {
        BoolFeedback MeetingIsLockedFeedback { get; }

        void LockMeeting();
        void UnLockMeeting();
        void ToggleMeetingLock();
    }
}