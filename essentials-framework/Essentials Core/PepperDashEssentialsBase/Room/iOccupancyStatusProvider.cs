namespace PepperDash.Essentials.Core
{
    public interface IOccupancyStatusProvider
    {
        BoolFeedback RoomIsOccupiedFeedback { get; }
    }
}