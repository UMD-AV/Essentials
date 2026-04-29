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
    public sealed class VideoPreview : IKeyed, IDisposable, IHttpCwsHandler
    {
        private readonly CTimer _previewPollTimer;
        private readonly EpiphanPearlSecureClient _client;
        private readonly string _routePattern;

        private byte[] _latestJpeg;

        private readonly string _imageUrl;
        private readonly int _minPollIntervalMs;
        private bool _enablePreviewFeedback;
        
        public string Key { get; private set; }

        public VideoPreview(EpiphanPearlSecureClient httpsClient, string name, string imageUrl, HttpCwsServer previewApi)
        {
            if (httpsClient == null) throw new ArgumentNullException("httpsClient");
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            if (string.IsNullOrEmpty(imageUrl)) throw new ArgumentNullException("imageUrl");
            
            previewApi.AddRoute(new HttpCwsRoute(string.Format("{0}.jpg", name))
            {
                RouteHandler = this
            });
            
            _client = httpsClient;
            Key = "videoPreview-" + name;
            _imageUrl = imageUrl;
            _minPollIntervalMs = 1000;
            _routePattern = string.Format("{0}.jpg", name);

            _previewPollTimer = new CTimer(PreviewPoll, Timeout.Infinite);

            Debug.Console(1, this, "VideoPreview created. ImageUrl={0}, Route={1}",
                _imageUrl);
        }

        public void EnablePreview()
        {
            _enablePreviewFeedback = true;
            _previewPollTimer.Reset(0);
        }

        public void DisablePreview()
        {
            _enablePreviewFeedback = false;
            _previewPollTimer.Reset(Timeout.Infinite);
            HandlePreviewFailure();
        }

        private void PreviewPoll(object o)
        {
            DateTime pollStart = DateTime.Now;

            try
            {
                if (!_enablePreviewFeedback)
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
                if (_enablePreviewFeedback)
                {
                    double elapsed = (DateTime.Now - pollStart).TotalMilliseconds;
                    int delay = elapsed < _minPollIntervalMs ? (int)(_minPollIntervalMs - elapsed) : 0;
                    _previewPollTimer.Reset(delay);
                }
            }
        }

        private void HandlePreviewFailure()
        {
            _latestJpeg = null;
        }

        public void Dispose()
        {
            _previewPollTimer.Reset(Timeout.Infinite);
            _previewPollTimer.Dispose();
        }
        
        public void ProcessRequest(HttpCwsContext context)
        {
            try
            {
                if (_latestJpeg == null || _latestJpeg.Length == 0)
                {
                    context.Response.StatusCode = 404;
                    context.Response.End();
                    return;
                }

                context.Response.StatusCode = 200;
                context.Response.ContentType = "image/jpeg";
                context.Response.OutputStream.Write(_latestJpeg, 0, _latestJpeg.Length);
                context.Response.End();
            }
            catch (Exception ex)
            {
                Debug.Console(0, "PreviewRequest exception: {0}", ex);

                context.Response.StatusCode = 500;
                context.Response.End();
            }
        }
    }
}