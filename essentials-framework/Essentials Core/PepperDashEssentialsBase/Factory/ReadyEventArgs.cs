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

namespace PepperDash_Essentials_Core
{
    [Obsolete("Use PepperDash.Essentials.Core")]
    public class IsReadyEventArgs : EventArgs
    {
        public bool IsReady { get; set; }

        public IsReadyEventArgs(bool data)
        {
            IsReady = data;
        }
    }

    [Obsolete("Use PepperDash.Essentials.Core")]
    public interface IHasReady
    {
        event EventHandler<IsReadyEventArgs> IsReadyEvent;
        bool IsReady { get; }
    }
}