using System;

namespace PepperDash.Essentials.Core
{
    public class IsReadyEventArgs : EventArgs
    {
        public bool IsReady { get; set; }

        public IsReadyEventArgs(bool data)
        {
            IsReady = data;
        }
    }

    public interface IHasReady
    {
        event EventHandler<IsReadyEventArgs> IsReadyEvent;
        bool IsReady { get; }
    }
}