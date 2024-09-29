using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;

using PepperDash.Core;

namespace PepperDash.Essentials.Core.Config
{
	/// <summary>
	/// Loads the ConfigObject from the file
	/// </summary>
	public class EssentialsConfig : BasicConfig
	{
        [JsonProperty("systemUuid")]
        public string SystemUuid { get; set; }

		[JsonProperty("rooms")]
        public List<DeviceConfig> Rooms { get; set; }


        public EssentialsConfig()
            : base()
        {
            Rooms = new List<DeviceConfig>();
        }
	}
		
	/// <summary>
	/// 
	/// </summary>
	public class SystemTemplateConfigs
	{
		public EssentialsConfig System { get; set; }

		public EssentialsConfig Template { get; set; }
	}
}