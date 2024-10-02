using System;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.EpiphanPearl.Interfaces;
using PepperDash.Essentials.EpiphanPearl.Utilities;

namespace PepperDash.Essentials.EpiphanPearl
{
    public class EpiphanPearlSecureClient : IEpiphanPearlClient
    {
        private readonly HttpsClient _client;

        private readonly HttpsHeader _authHeader;

        private string _basePath;

        public EpiphanPearlSecureClient(string host, string username, string password)
        {
            _client = new HttpsClient();

            _client.HostVerification = false;
            _client.PeerVerification = false;

            _basePath = string.Format("https://{0}", host);

            _authHeader = HttpHelpers.GetSecureAuthorizationHeader(username, password);
        }

        public T Get<T>(string path) where T : class
        {
            HttpsClientRequest request = CreateRequest(path, Crestron.SimplSharp.Net.Https.RequestType.Get);

            string response = SendRequest(request);

            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(response);
            }
            catch (Exception ex)
            {
                Debug.Console(0, "[T Get<T>] Exception sending to {0}: {1}", request.Url, ex.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.StackTrace);

                if (ex.InnerException == null) return null;

                Debug.Console(0, "[T Get<T>] Exception sending to {0}: {1}", request.Url, ex.InnerException.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.InnerException.StackTrace);

                return null;
            }
        }

        public TResponse Post<TBody, TResponse>(string path, TBody body) where TBody : class where TResponse : class
        {
            HttpsClientRequest request = CreateRequest(path, Crestron.SimplSharp.Net.Https.RequestType.Post);

            request.Header.ContentType = "application/json";
            request.ContentString = body != null ? JsonConvert.ToString(body) : string.Empty;

            string response = SendRequest(request);

            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<TResponse>(response);
            }
            catch (Exception ex)
            {
                Debug.Console(0, "[TResponse Post<TBody, TResponse>] Exception sending to {0}: {1}", request.Url,
                    ex.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.StackTrace);

                if (ex.InnerException == null) return null;

                Debug.Console(0, "[TResponse Post<TBody, TResponse>] Exception sending to {0}: {1}", request.Url,
                    ex.InnerException.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.InnerException.StackTrace);

                return null;
            }
        }

        public TResponse Post<TResponse>(string path)
            where TResponse : class
        {
            HttpsClientRequest request = CreateRequest(path, Crestron.SimplSharp.Net.Https.RequestType.Post);

            request.Header.ContentType = "application/json";

            string response = SendRequest(request);

            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<TResponse>(response);
            }
            catch (Exception ex)
            {
                Debug.Console(0, "[TResponse Post<TResponse>] Exception sending to {0}: {1}", request.Url, ex.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.StackTrace);

                if (ex.InnerException == null) return null;

                Debug.Console(0, "[TResponse Post<TResponse>] Exception sending to {0}: {1}", request.Url,
                    ex.InnerException.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.InnerException.StackTrace);

                return null;
            }
        }

        public string Delete(string path)
        {
            HttpsClientRequest request = CreateRequest(path, Crestron.SimplSharp.Net.Https.RequestType.Delete);

            return SendRequest(request);
        }

        public void setHost(string host)
        {
            _basePath = string.Format("https://{0}/api", host);
        }

        private string SendRequest(HttpsClientRequest request)
        {
            try
            {
                //Debug.Console(0, "Request to {0): {1}", request.Url, request.ContentString);
                HttpsClientResponse response = _client.Dispatch(request);

                //Debug.Console(0, "Response from request to {0}: {1} {2}", request.Url, response.Code,
                //response.ContentString);

                return response.ContentString;
            }
            catch (Exception ex)
            {
                Debug.Console(0, "[SendRequest] Exception sending to {0}: {1}", request.Url, ex.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.StackTrace);

                if (ex.InnerException == null) return null;

                Debug.Console(0, "[SendRequest] Exception sending to {0}: {1}", request.Url, ex.InnerException.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.InnerException.StackTrace);

                return null;
            }
        }

        private HttpsClientRequest CreateRequest(string path, Crestron.SimplSharp.Net.Https.RequestType requestType)
        {
            HttpsClientRequest request = new HttpsClientRequest
            {
                Url = new UrlParser(string.Format("{0}/api{1}", _basePath, path)),
                RequestType = requestType
            };

            request.Header.AddHeader(_authHeader);

            return request;
        }
    }
}