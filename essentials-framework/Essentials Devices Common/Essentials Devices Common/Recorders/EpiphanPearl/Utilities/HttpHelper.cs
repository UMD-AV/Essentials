using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;

namespace PepperDash.Essentials.EpiphanPearl.Utilities
{
    public static class HttpHelpers
    {
        public static HttpHeader GetAuthorizationHeader(string username, string password)
        {
            return new HttpHeader("Authorization", string.Format("Basic {0}", GetCredentialsForHeader(username, password)));
        }

        public static HttpsHeader GetSecureAuthorizationHeader(string username, string password)
        {
            return new HttpsHeader("Authorization", string.Format("Basic {0}", GetCredentialsForHeader(username, password)));
        }

        private static string GetCredentialsForHeader(string username, string password)
        {
            return Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", username, password)));
        }
    }
}