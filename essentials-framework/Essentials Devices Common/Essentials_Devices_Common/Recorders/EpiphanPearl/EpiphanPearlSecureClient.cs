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
            request.ContentString = body != null ? JsonConvert.SerializeObject(body) : string.Empty;

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
                Debug.Console(0, "[TResponse Post<TBody, TResponse>] Exception sending to {0}: {1}\r{2}", request.Url,
                    ex.Message, response);
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
            return Post<TResponse>(path, Crestron.SimplSharp.Net.Https.RequestType.Post);
        }

        public TResponse Put<TResponse>(string path)
            where TResponse : class
        {
            return Post<TResponse>(path, Crestron.SimplSharp.Net.Https.RequestType.Put);
        }

        public TResponse Put<TBody, TResponse>(string path, TBody body) where TBody : class where TResponse : class
        {
            HttpsClientRequest request = CreateRequest(path, Crestron.SimplSharp.Net.Https.RequestType.Put);

            request.Header.ContentType = "application/json";
            request.ContentString = body != null ? JsonConvert.SerializeObject(body) : string.Empty;

            Debug.Console(2, "Put request: {0} - {1}", request.Url, request.ContentString);

            string response = SendRequest(request);

            if (response == null)
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<TResponse>(response);
            }
            catch (Exception ex)
            {
                Debug.Console(0, "[TResponse Put<TBody, TResponse>] Exception sending to {0}: {1}", request.Url,
                    ex.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.StackTrace);

                if (ex.InnerException == null) return null;

                Debug.Console(0, "[TResponse Put<TBody, TResponse>] Exception sending to {0}: {1}", request.Url,
                    ex.InnerException.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.InnerException.StackTrace);

                return null;
            }
        }

        private TResponse Post<TResponse>(string path, Crestron.SimplSharp.Net.Https.RequestType requestType)
            where TResponse : class
        {
            HttpsClientRequest request = CreateRequest(path, requestType);

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
            if (request == null)
            {
                Debug.Console(0, "[SendRequest] Request is null");
                return null;
            }

            if (_client == null)
            {
                Debug.Console(0, "[SendRequest] HttpClient is null");
                return null;
            }

            try
            {
                Debug.Console(1, "Request to {0}: {1}", request.Url, request.ContentString);
                HttpsClientResponse response = _client.Dispatch(request);

                if (response == null)
                {
                    Debug.Console(2, "[SendRequest] Response is null after dispatching request to {0}", request.Url);
                    return null;
                }

                Debug.Console(1, "Response from request to {0}: {1} {2}", request.Url, response.Code,
                    response.ContentString);

                try
                {
                    // Attempt to parse the response content as a string
                    string contentString = response.ContentString;
                    return contentString;
                }
                catch (Exception ex)
                {
                    Debug.Console(2, "[SendRequest] Error converting response to string for URL {0}: {1}", request.Url,
                        ex.Message);
                    return null;
                }
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

        public void Dispose()
        {
            if (_client != null) _client.Dispose();
        }
    }
}