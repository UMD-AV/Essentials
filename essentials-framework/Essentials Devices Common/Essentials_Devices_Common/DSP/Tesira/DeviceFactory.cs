using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Core;
using System.Collections.Generic;
using PepperDash.Core;

namespace Tesira_DSP_EPI
{
    public class TesiraFactory : EssentialsDeviceFactory<TesiraDsp>
    {
        /// <summary>
        /// Factory for building new TesiraDsp Device
        /// </summary>
        public TesiraFactory()
        {
            TypeNames = new List<string> { "tesira", "tesiradsp" };
        }

        /// <summary>
        /// Build new TesiraDsp Device from Config
        /// </summary>
        /// <param name="dc">TesiraDsp Device Config</param>
        /// <returns></returns>
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Biamp Tesira Device");

            IBasicCommunication comm = CommFactory.CreateCommForDevice(dc);

            return new TesiraDsp(dc.Key, dc.Name, comm, dc);
        }
    }
}