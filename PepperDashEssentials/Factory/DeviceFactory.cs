using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp.Reflection;
using PepperDash.Essentials.Core;
using PepperDash.Core;

namespace PepperDash.Essentials
{
    /// <summary>
    /// Responsible for loading all of the device types for this library
    /// </summary>
    public class DeviceFactory
    {
        public DeviceFactory()
        {
            Assembly assy = Assembly.GetExecutingAssembly();
            PluginLoader.SetEssentialsAssembly(assy.GetName().Name, assy);

            IEnumerable<CType> types = assy.GetTypes().Where(ct =>
                typeof(IDeviceFactory).IsAssignableFrom(ct) && !ct.IsInterface && !ct.IsAbstract);

            if (types != null)
            {
                foreach (CType type in types)
                {
                    try
                    {
                        IDeviceFactory factory = (IDeviceFactory)Crestron.SimplSharp.Reflection.Activator.CreateInstance(type);
                        factory.LoadTypeFactories();
                    }
                    catch (Exception e)
                    {
                        Debug.Console(0, Debug.ErrorLogLevel.Error, "Unable to load type: '{1}' DeviceFactory: {0}", e,
                            type.Name);
                    }
                }
            }
        }
    }
}