using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.WebScripting;
using PepperDash.Core;

namespace PepperDash.Essentials.EpiphanPearl.Utilities
{
    /// <summary>
    /// Polls an image from a URL using the provided EpiphanPearlSecureClient
    /// and serves the latest frame via Crestron Web Scripting.
    /// </summary>
    public sealed class VideoPreview : IKeyed, IDisposable
    {
        private readonly CTimer _previewPollTimer;
        private readonly EpiphanPearlSecureClient _client;
        private readonly HttpCwsServer _cwsServer;
        private readonly string _routePattern;

        private byte[] _latestJpeg;
        private byte[] _blackJpeg;

        private readonly string _imageUrl;
        private readonly int _minPollIntervalMs;

        public string Key { get; private set; }
        public bool EnablePreviewFeedback { get; private set; }

        public string PreviewUrl
        {
            get { return "/" + _routePattern; }
        }

        public VideoPreview(EpiphanPearlSecureClient httpsClient, string name, string imageUrl)
        {
            if (httpsClient == null) throw new ArgumentNullException("httpsClient");
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            if (string.IsNullOrEmpty(imageUrl)) throw new ArgumentNullException("imageUrl");

            _client = httpsClient;
            Key = "videoPreview-" + name;
            _imageUrl = imageUrl;
            _minPollIntervalMs = 1000;
            _routePattern = string.Format("preview/{0}.jpg", name);

            // Optional: assign a real black jpeg here if you want the handler
            // to return black instead of 503 when no live image is available.
            _blackJpeg = null;

            _cwsServer = new HttpCwsServer("/");
            var route = new HttpCwsRoute(_routePattern);
            route.RouteHandler = new PreviewRequestHandler(this);
            _cwsServer.AddRoute(route);
            _cwsServer.Register();

            _previewPollTimer = new CTimer(PreviewPoll, Timeout.Infinite);

            Debug.Console(1, this, "VideoPreview created. ImageUrl={0}, Route={1}",
                _imageUrl, PreviewUrl);
        }

        public void EnablePreview()
        {
            EnablePreviewFeedback = true;
            _previewPollTimer.Reset(0);
        }

        public void DisablePreview()
        {
            EnablePreviewFeedback = false;
            _previewPollTimer.Reset(Timeout.Infinite);
            HandlePreviewFailure();
        }

        private void PreviewPoll(object o)
        {
            DateTime pollStart = DateTime.Now;

            try
            {
                if (!EnablePreviewFeedback)
                    return;

                byte[] img = _client.Get(_imageUrl);
                if (img == null || img.Length == 0)
                {
                    Debug.Console(0, this, "Image response was null/empty.");
                    HandlePreviewFailure();
                    return;
                }

                _latestJpeg = img;
                Debug.Console(1, this, "Image cached. Size={0} bytes", img.Length);
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Error in PreviewPoll: {0}", ex);
                HandlePreviewFailure();
            }
            finally
            {
                if (EnablePreviewFeedback)
                {
                    double elapsed = (DateTime.Now - pollStart).TotalMilliseconds;
                    int delay = elapsed < _minPollIntervalMs ? (int)(_minPollIntervalMs - elapsed) : 0;
                    _previewPollTimer.Reset(delay);
                }
            }
        }

        private void HandlePreviewFailure()
        {
            // Option A: make route return 503 if no image is available
            _latestJpeg = null;

            // Option B: uncomment this to serve black instead
            // _latestJpeg = _blackJpeg;
        }

        private byte[] GetCurrentFrame()
        {
            return _latestJpeg;
        }

        public void Dispose()
        {
            _previewPollTimer.Reset(Timeout.Infinite);
            _previewPollTimer.Dispose();

            try
            {
                _cwsServer.Unregister();
            }
            catch
            {
            }
        }

        private sealed class PreviewRequestHandler : IHttpCwsHandler
        {
            private readonly VideoPreview _parent;

            public PreviewRequestHandler(VideoPreview parent)
            {
                _parent = parent;
            }

            public void ProcessRequest(HttpCwsContext context)
            {
                try
                {
                    var method = context.Request.HttpMethod;
                    if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = 405;
                        context.Response.StatusDescription = "Method Not Allowed";
                        context.Response.Write("Method Not Allowed", true);
                        return;
                    }

                    byte[] jpeg = _parent.GetCurrentFrame();

                    if (jpeg == null || jpeg.Length == 0)
                    {
                        context.Response.StatusCode = 503;
                        context.Response.StatusDescription = "Service Unavailable";
                        context.Response.Write("No frame available", true);
                        return;
                    }

                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "image/jpeg";

                    // Depending on firmware/API version, one of these patterns is usually available.
                    // Use the one your SDK exposes:
                    context.Response.OutputStream.Write(jpeg,0,jpeg.Length);

                    // Some SDKs may also need:
                    context.Response.End();
                }
                catch (Exception ex)
                {
                    Debug.Console(0, _parent, "Error handling CWS request: {0}", ex);
                    context.Response.StatusCode = 500;
                    context.Response.StatusDescription = "Internal Server Error";
                    context.Response.Write("Internal Server Error", true);
                }
            }
        }
    }
}