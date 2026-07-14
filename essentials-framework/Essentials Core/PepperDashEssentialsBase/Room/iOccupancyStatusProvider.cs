namespace UmdEssentials.Core
{
    public interface IOccupancyStatusProvider
    {
        BoolFeedback RoomIsOccupiedFeedback { get; }
    }
}