using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Newtonsoft.Json;

namespace PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces
{
    /// <summary>
    /// Describes a device that provides a waiting room (like a ZoomRoom)
    /// </summary>
    public interface IHasWaitingRoom
    {
        event EventHandler<WaitingRoomEventArgs> WaitingRoomChanged;

        void AdmitParticipantFromWaitingRoom(int userId);
        void AdmitParticipantFromWaitingRoomIndex(ushort index);
    }

    public class WaitingRoomEventArgs : EventArgs
    {
        public bool InWaitingRoom { get; private set; }

        public WaitingRoomEventArgs(bool inWaitingRoom)
        {
            InWaitingRoom = inWaitingRoom;
        }
    }
}