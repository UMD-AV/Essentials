using System;
using System.Linq;
using System.Collections.Generic;
using Crestron.SimplSharp.Reflection;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace NvxEpi.Factories
{
    /// <summary>
    /// Responsible for loading the type factories for this library
    /// </summary>
    public class DeviceFactory
    {
        public DeviceFactory()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            PepperDash.Essentials.PluginLoader.SetEssentialsAssembly(assembly.GetName().Name, assembly);

            IEnumerable<CType> types = assembly.GetTypes().Where(ct =>
                typeof(IDeviceFactory).IsAssignableFrom(ct) && !ct.IsInterface && !ct.IsAbstract);

            foreach (CType type in types)
            {
                try
                {
                    IDeviceFactory factory =
                        (IDeviceFactory)Crestron.SimplSharp.Reflection.Activator.CreateInstance(type);
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