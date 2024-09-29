using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Newtonsoft.Json;

namespace PepperDash.Essentials.EpiphanPearl
{
    public class EpiphanPearlControllerConfiguration
    {
        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("secure")]
        public bool Secure { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }
    }
}