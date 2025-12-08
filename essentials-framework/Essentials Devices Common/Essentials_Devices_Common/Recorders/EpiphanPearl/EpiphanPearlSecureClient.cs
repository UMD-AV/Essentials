using System;
using System.Collections.Generic;
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Core.HttpsUtility.Https;
using HttpsClient = PepperDash.Core.HttpsUtility.Https.HttpsClient;

namespace PepperDash.Essentials.EpiphanPearl
{
    public class EpiphanPearlSecureClient : IKeyed
    {
        private readonly HttpsClient _client;
        private readonly List<HttpsHeader> _headers;
        private string _basePath;

        public EpiphanPearlSecureClient(string key, string host, string username, string password)
        {
            Key = key + "-https";
            _client = new HttpsClient();

            _client.HostVerification = false;
            _client.PeerVerification = false;

            _basePath = string.Format("https://{0}", host);
            _headers = new List<HttpsHeader>
            {
                HttpHelpers.GetSecureAuthorizationHeader(username, password),
                new HttpsHeader("Content-Type", "application/json")
            };
        }

        public T Get<T>(string path) where T : class
        {
            string url = string.Format("{0}/api{1}", _basePath, path);
            Debug.Console(1, this, "Getting {0}", url);
            HttpsResult result = _client.Get(url, _headers);
            try
            {
                if (result.Status != 200)
                {
                    Debug.Console(0, this, "Failed to get response from server. Code: {0}", result.Status);
                    return null;
                }

                return JsonConvert.DeserializeObject<T>(result.Content);
            }
            catch (Exception ex)
            {
                Debug.Console(0, "[T Get<T>] Exception sending to {0}: {1}", path, ex.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.StackTrace);

                if (ex.InnerException == null) return null;

                Debug.Console(0, "[T Get<T>] Exception sending to {0}: {1}", path, ex.InnerException.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.InnerException.StackTrace);

                return null;
            }
        }

        public TResponse Post<TResponse>(string path)
            where TResponse : class
        {
            return Post<object, TResponse>(path, null);
        }

        public TResponse Post<TBody, TResponse>(string path, TBody body) where TBody : class where TResponse : class
        {
            string url = string.Format("{0}/api{1}", _basePath, path);
            string requestBody = body != null ? JsonConvert.SerializeObject(body) : string.Empty;
            Debug.Console(1, this, "Posting {0} body: {1}", url, requestBody);
            HttpsResult result = _client.Post(url, _headers, requestBody);
            try
            {
                if (result.Status != 200)
                {
                    Debug.Console(0, this, "Failed to get response from server. Code: {0}", result.Status);
                    return null;
                }

                return JsonConvert.DeserializeObject<TResponse>(result.Content);
            }
            catch (Exception ex)
            {
                Debug.Console(0, "[TResponse Post<TBody, TResponse>] Exception sending to {0}: {1}\r{2}", path,
                    ex.Message, result.Content);
                Debug.Console(2, "Stack Trace: {0}", ex.StackTrace);

                if (ex.InnerException == null) return null;

                Debug.Console(0, "[TResponse Post<TBody, TResponse>] Exception sending to {0}: {1}", path,
                    ex.InnerException.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.InnerException.StackTrace);

                return null;
            }
        }

        public TResponse Put<TResponse>(string path)
            where TResponse : class
        {
            return Put<object, TResponse>(path, null);
        }

        public TResponse Put<TBody, TResponse>(string path, TBody body) where TBody : class where TResponse : class
        {
            string url = string.Format("{0}/api{1}", _basePath, path);
            string requestBody = body != null ? JsonConvert.SerializeObject(body) : string.Empty;
            Debug.Console(1, this, "Putting {0} body: {1}", url, requestBody);
            HttpsResult result = _client.Put(url, _headers, requestBody);
            try
            {
                if (result.Status != 200)
                {
                    Debug.Console(0, this, "Failed to get response from server. Code: {0}", result.Status);
                    Debug.Console(0, this, "Failed to get response from server. Content: {0}", result.Content);
                    return null;
                }

                return JsonConvert.DeserializeObject<TResponse>(result.Content);
            }
            catch (Exception ex)
            {
                Debug.Console(0, "[TResponse Put<TBody, TResponse>] Exception sending to {0}: {1}", path,
                    ex.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.StackTrace);

                if (ex.InnerException == null) return null;

                Debug.Console(0, "[TResponse Put<TBody, TResponse>] Exception sending to {0}: {1}", path,
                    ex.InnerException.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.InnerException.StackTrace);

                return null;
            }
        }

        public void SetHost(string host)
        {
            _basePath = string.Format("https://{0}", host);
        }

        public void Dispose()
        {
            if (_client != null) _client.Dispose();
        }

        public string Key { get; private set; }
    }
}