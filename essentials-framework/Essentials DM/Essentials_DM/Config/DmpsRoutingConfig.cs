﻿using System;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DM;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.DM.Cards;

namespace PepperDash.Essentials.DM.Config
{
	/// <summary>
	/// Represents the "properties" property of a DM device config
	/// </summary>
	public class DmpsRoutingPropertiesConfig
	{
		[JsonProperty("inputNames")]
		public Dictionary<uint, string> InputNames { get; set; }

		[JsonProperty("outputNames")]
		public Dictionary<uint, string> OutputNames { get; set; }

        public DmpsRoutingPropertiesConfig()
        {
            InputNames = new Dictionary<uint, string>();
            OutputNames = new Dictionary<uint, string>();
        }
	}


}