﻿using PepperDash.Core;

namespace PepperDash.Essentials.Core.Interfaces
{
    public interface ILogStringsWithLevel : IKeyed
    {
        /// <summary>
        /// Defines a class that is capable of logging a string with an int level
        /// </summary>
        void SendToLog(IKeyed device, Debug.ErrorLogLevel level, string logMessage);
    }
}