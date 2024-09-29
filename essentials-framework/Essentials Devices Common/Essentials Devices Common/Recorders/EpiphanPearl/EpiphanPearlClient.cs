using System;
using Crestron.SimplSharp.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.EpiphanPearl.Interfaces;
using PepperDash.Essentials.EpiphanPearl.Utilities;
using System.Text;
using PepperDash.Core.Logging;

namespace PepperDash.Essentials.EpiphanPearl
{
    public class EpiphanPearlClient : IEpiphanPearlClient
    {
        private readonly HttpClient _client;

        private readonly HttpHeader _authHeader;

        private string _basePath;

        public EpiphanPearlClient(string host, string username, string password)
        {
            _client = new HttpClient();

            _basePath = string.Format("http://{0}/api", host);

            _authHeader = HttpHelpers.GetAuthorizationHeader(username, password);
        }

        public T Get<T>(string path) where T:class
        {
            var request = CreateRequest(path, RequestType.Get);

            var response = SendRequest(request);

            if (response == null || response.Length <= 0)
            {
                Debug.Console(2, "[T Get<T>] Response to {0} is null", request.Url, response);
                return null;
            }

            try
            {
                Debug.Console(2, "[T Get<T>] Response to {0}: {1}", request.Url, response);
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

        public TResponse Post<TBody, TResponse> (string path, TBody body) where TBody: class where TResponse: class
        {
            var request = CreateRequest(path, RequestType.Post);

            request.Header.ContentType = "application/json";
            request.ContentString = body != null ? JsonConvert.SerializeObject(body) : string.Empty;

            Debug.Console(2, "Post request: {0} - {1}", request.Url, request.ContentString);

            var response = SendRequest(request);

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
                Debug.Console(0, "[TResponse Post<TBody, TResponse>] Exception sending to {0}: {1}", request.Url, ex.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.StackTrace);

                if (ex.InnerException == null) return null;

                Debug.Console(0, "[TResponse Post<TBody, TResponse>] Exception sending to {0}: {1}", request.Url, ex.InnerException.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.InnerException.StackTrace);

                return null;
            }
        }

        public TResponse Post<TResponse>(string path)
            where TResponse : class
        {
            var request = CreateRequest(path, RequestType.Post);

            request.Header.ContentType = "application/json";

            var response = SendRequest(request);

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
                Debug.Console(0, "[TResponse Post<TResponse>] Exception sending to {0}: {1}", request.Url, ex.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.StackTrace);

                if (ex.InnerException == null) return null;

                Debug.Console(0, "[TResponse Post<TResponse>] Exception sending to {0}: {1}", request.Url, ex.InnerException.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.InnerException.StackTrace);

                return null;
            }
        }

        public string Delete(string path)
        {
            var request = CreateRequest(path, RequestType.Delete);

            return SendRequest(request);
        }

        public void setHost(string host)
        {
            _basePath = string.Format("http://{0}/api", host);
        }

        private string SendRequest(HttpClientRequest request)
        {
            if (request == null)
            {
                Debug.Console(2, "[SendRequest] Request is null");
                return null;
            }

            if (_client == null)
            {
                Debug.Console(2, "[SendRequest] HttpClient is null");
                return null;
            }

            try
            {
                Debug.Console(2, "[SendRequest] Dispatching request to {0}", request.Url); // Log before dispatch
                var response = _client.Dispatch(request);

                if (response == null)
                {
                    Debug.Console(2, "[SendRequest] Response is null after dispatching request to {0}", request.Url);
                    return null;
                }

                //Debug.Console(0, "Raw response bytes: {0}", BitConverter.ToString(response.ContentBytes));

                try
                {
                    // Attempt to parse the response content as a string
                    var contentString = response.ContentString;
                    return contentString;
                }
                catch (Exception ex)
                {
                    Debug.Console(2, "[SendRequest] Error converting response to string for URL {0}: {1}", request.Url, ex.Message);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, "[SendRequest] Exception sending to {0}: {1}", request.Url, ex.Message);
                Debug.Console(2, "Stack Trace: {0}", ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Debug.Console(0, "[SendRequest] Inner Exception: {1}", request.Url, ex.InnerException.Message);
                    Debug.Console(2, "Inner Stack Trace: {0}", ex.InnerException.StackTrace);
                }

                return null;
            }
        }

        private HttpClientRequest CreateRequest(string path, RequestType requestType)
        {
            var request = new HttpClientRequest
            {
                Url = new UrlParser(string.Format("{0}{1}", _basePath, path)),
                RequestType = requestType
            };

            request.Header.AddHeader(_authHeader);

            return request;
        }
    }
}