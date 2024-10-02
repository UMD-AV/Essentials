using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Core.Shades
{
    /// <summary>
    /// Class that contains the shades to be controlled in a room
    /// </summary>
    public class ShadeController : EssentialsDevice, IShades
    {
        private ShadeControllerConfigProperties Config;

        public List<ShadeBase> Shades { get; private set; }

        public ShadeController(string key, string name, ShadeControllerConfigProperties config)
            : base(key, name)
        {
            Config = config;

            Shades = new List<ShadeBase>();
        }

        public override bool CustomActivate()
        {
            foreach (ShadeControllerConfigProperties.ShadeConfig shadeConfig in Config.Shades)
            {
                ShadeBase shade = DeviceManager.GetDeviceForKey(shadeConfig.Key) as ShadeBase;

                if (shade != null)
                {
                    AddShade(shade);
                }
            }

            return base.CustomActivate();
        }

        private void AddShade(ShadeBase shade)
        {
            Shades.Add(shade);
        }
    }

    public class ShadeControllerConfigProperties
    {
        public List<ShadeConfig> Shades { get; set; }


        public class ShadeConfig
        {
            public string Key { get; set; }
        }
    }

    public class ShadeControllerFactory : EssentialsDeviceFactory<ShadeController>
    {
        public ShadeControllerFactory()
        {
            TypeNames = new List<string>() { "shadecontroller" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new ShadeController Device");
            ShadeControllerConfigProperties props =
                Newtonsoft.Json.JsonConvert.DeserializeObject<Core.Shades.ShadeControllerConfigProperties>(
                    dc.Properties.ToString());

            return new Core.Shades.ShadeController(dc.Key, dc.Name, props);
        }
    }
}