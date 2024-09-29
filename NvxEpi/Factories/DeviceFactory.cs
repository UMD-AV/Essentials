using System;
using System.Linq;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharp.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace NvxEpi.Factories
{
    /// <summary>
    /// Responsible for loading the type factories for this library
    /// </summary>
    public class DeviceFactory
    {
        public DeviceFactory()
        {
            var assy = Assembly.GetExecutingAssembly();
            PepperDash.Essentials.PluginLoader.SetEssentialsAssembly(assy.GetName().Name, assy);
            
            var types = assy.GetTypes().Where(ct => typeof(IDeviceFactory).IsAssignableFrom(ct) && !ct.IsInterface && !ct.IsAbstract);

            if (types != null)
            {
                foreach (var type in types)
                {
                    try
                    {
                        var factory = (IDeviceFactory)Crestron.SimplSharp.Reflection.Activator.CreateInstance(type);
                        factory.LoadTypeFactories();
                    }
                    catch (Exception e)
                    {
                        Debug.Console(0, Debug.ErrorLogLevel.Error, "Unable to load type: '{1}' DeviceFactory: {0}", e, type.Name);
                    }
                }
            }
        }
    }
}