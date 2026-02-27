using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Http;
using PepperDash.Core;

namespace PepperDash.Essentials.EpiphanPearl.Utilities
{
    /// <summary>
    /// Polls an image from a URL using the provided EpiphanPearlSecureClient and serves the latest frame via HttpServer.
    /// </summary>
    public sealed class VideoPreview : IKeyed, IDisposable
    {
        private readonly CTimer _previewPollTimer;
        private readonly EpiphanPearlSecureClient _client;
        private readonly HttpServer _httpServer;
        private readonly string _imageRoutePath;
        private byte[] _latestJpeg;

        public string Key { get; private set; }
        public bool EnablePreviewFeedback { get; private set; }

        private readonly string _imageUrl;
        private readonly int _minPollIntervalMs;

        public VideoPreview(EpiphanPearlSecureClient httpsClient, string name, string imageUrl, int httpPort)
        {
            if (httpsClient == null) throw new ArgumentNullException("httpsClient");
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            if (string.IsNullOrEmpty(imageUrl)) throw new ArgumentNullException("imageUrl");
            if (httpPort <= 0) throw new ArgumentOutOfRangeException("httpPort");

            _client = httpsClient;

            Key = "videoPreview-" + name;
            _imageUrl = imageUrl;

            _minPollIntervalMs = 100;
            _imageRoutePath = "/preview/" + name + ".jpg";

            _httpServer = new HttpServer
            {
                Port = httpPort
            };
            _httpServer.OnHttpRequest += OnHttpRequest;
            _httpServer.Open();
            _previewPollTimer = new CTimer(PreviewPoll, Timeout.Infinite);

            Debug.Console(1, this, "VideoPreview created. ImageUrl={0}, HttpPort={1}, Route={2}",
                _imageUrl, httpPort, _imageRoutePath);
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
            _latestJpeg = null;
        }

        private void PreviewPoll(object o)
        {
            DateTime pollStart = DateTime.Now;

            try
            {
                if (!EnablePreviewFeedback) return;
                Debug.Console(0, this, "Polling image: {0}", _imageUrl);
                CrestronInvoke.BeginInvoke(obj =>
                {
                    byte[] img = _client.Get(_imageUrl);
                    if (img == null || img.Length == 0)
                    {
                        Debug.Console(0, this, "Image response was null/empty.");
                        return;
                    }

                    _latestJpeg = img;

                    Debug.Console(1, this, "Image cached. Size={0} bytes", img.Length);
                });
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Error in PreviewPoll: {0}", ex);
            }
            finally
            {
                if (EnablePreviewFeedback)
                {
                    double elapsed = (DateTime.Now - pollStart).TotalMilliseconds;
                    int delay = elapsed < _minPollIntervalMs ? (int)(_minPollIntervalMs - elapsed) : 0;
                    Debug.Console(1, this, "Poll complete. Elapsed={0}ms, NextDelay={1}ms", elapsed, delay);
                    _previewPollTimer.Reset(delay);
                }
            }
        }

        private void OnHttpRequest(object o, OnHttpRequestArgs args)
        {
            try
            {
                string path = args.Request.Path.IndexOf('?') >= 0
                    ? args.Request.Path.Substring(0, args.Request.Path.IndexOf('?'))
                    : args.Request.Path;
                Debug.Console(1, this, "HTTP {0} {1}", args.Request.Header.RequestType, path);

                if (!string.Equals(path, _imageRoutePath, StringComparison.OrdinalIgnoreCase))
                {
                    args.Response.SendErrorWithCustomBody(404, "Not Found", "Not Found");
                    Debug.Console(1, this, "HTTP 404 for path: {0}", path);
                    return;
                }

                byte[] jpeg = _latestJpeg;

                if (jpeg == null || jpeg.Length == 0)
                {
                    args.Response.SendErrorWithCustomBody(503, "Service Unavailable", "No frame available yet");
                    Debug.Console(1, this, "HTTP 503 - no cached frame yet.");
                    return;
                }

                // Build response
                args.Response.Code = 200;
                args.Response.ResponseText = "OK";
                args.Response.Header.ContentType = "image/jpeg";
                args.Response.Header.SetHeaderValue("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0");
                args.Response.ContentBytes = jpeg;
                args.Response.FinalizeHeader();

                Debug.Console(1, this, "HTTP 200 - served {0} bytes", jpeg.Length);
            }
            catch (Exception ex)
            {
                Debug.Console(1, this, "Error handling HTTP request: {0}", ex);
                args.Response.SendErrorWithCustomBody(500, "Internal Server Error", "Internal Server Error");
            }
        }

        public void Dispose()
        {
            _previewPollTimer.Reset(Timeout.Infinite);
            _previewPollTimer.Dispose();
            _httpServer.Close();
            _httpServer.Dispose();
        }
    }
}