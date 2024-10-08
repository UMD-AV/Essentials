using System;
using System.Text;
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json;

namespace PepperDash.Essentials.PanoptoCloud
{
    public static class PanoptoOauthClient
    {
        public static TokenResponse GetToken(string url, string username, string password, string clientId, string clientPassword)
        {
            using (HttpsClient client = new HttpsClient())
            {
                HttpsClientRequest request = BuildRequest(url, username, password, clientId, clientPassword);
                HttpsClientResponse response = client.Dispatch(request);
                if (response == null)
                    throw new NullReferenceException("response");
                if (response.Code != 200)
                    throw new Exception(string.Format("Error getting token: {0} {1}", response.Code, response.ContentString));

                return JsonConvert.DeserializeObject<TokenResponse>(response.ContentString);
            }
        }

        public static HttpsClientRequest BuildRequest(string url, string username, string password, string clientId, string clientPassword)
        {
            string auth = Base64Encode(clientId + ":" + clientPassword);
            HttpsHeader authHeader = new HttpsHeader("Authorization", "Basic " + auth);
            HttpsHeader contentHeader = new HttpsHeader("Content-Type", "application/x-www-form-urlencoded");

            HttpsClientRequest request = new HttpsClientRequest
            {
                RequestType = RequestType.Post,
                ContentString = string.Format("Grant_type=password&Username={0}&Password={1}&Scope=api", username, password),
            };

            request.Url.Parse(url);
            request.Header.AddHeader(authHeader);
            request.Header.AddHeader(contentHeader);
            return request;
        }

        public static string Base64Encode(string plainText)
        {
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public class TokenResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }
            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }
            [JsonProperty("token_type")]
            public string TokenType { get; set; }
        }
    }
}