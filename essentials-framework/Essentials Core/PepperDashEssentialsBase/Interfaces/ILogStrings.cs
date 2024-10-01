using PepperDash.Core;

namespace PepperDash.Essentials.Core.Interfaces
{
    public interface ILogStrings : IKeyed
    {
        /// <summary>
        /// Defines a class that is capable of logging a string
        /// </summary>
        void SendToLog(IKeyed device, string logMessage);
    }
}