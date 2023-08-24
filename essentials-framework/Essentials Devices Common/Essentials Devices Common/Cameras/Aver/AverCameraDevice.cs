using System;
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;
using PepperDash.Core;
using PepperDash.Essentials.Core.Config;
using ViscaCameraPlugin;

namespace AverCameraPlugin
{
    public class AverCameraDevice : ViscaCameraDevice
    {
        private string hostname;
        private string username;
        private string password;
        private HttpClient client;

        public AverCameraDevice(string key, string name, IBasicCommunication comms, ViscaCameraConfig config, EssentialsControlPropertiesConfig commConfig)
			: base(key, name, comms, config)
        {
            this.hostname = commConfig.TcpSshProperties.Address;
            this.username = commConfig.TcpSshProperties.Username;
            this.password = commConfig.TcpSshProperties.Password;

            BuildClient();
        }

        private void BuildClient()
        {
            client = new HttpClient()
            {
                UserAgent = "crestron",
                KeepAlive = false,
                Accept = "text/plain",
                AllowAutoRedirect = false
            };
        }

        /// <summary>
        /// For tracking feedback responses from camera
        /// </summary>
        private enum eAverCameraInquiry
        {
            AutoTrackOnCmd,
            AutoTrackOffCmd,
            AutoTrackInquiry
        }

        private void PostData(string data, eAverCameraInquiry requestName)
        {
            try
            {
                Debug.Console(1, "Aver Camera Post {0} http:{1}", requestName, data);
                var req = new HttpClientRequest();
                string url = string.Format("http://{0}/{1}", hostname, data);
                string auth = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(username + ":" + password));
                req.Header.SetHeaderValue("Authorization", "Basic " + auth);
                req.Header.ContentType = "text/plain";
                req.Header.SetHeaderValue("Content-Length", "0");
                req.Encoding = Encoding.ASCII;
                req.RequestType = Crestron.SimplSharp.Net.Http.RequestType.Post;
                req.Url.Parse(url);
                
                Debug.Console(1, "Aver Camera Post to url {0} with token {1}", url, auth);
                client.DispatchAsyncEx(req, HttpCallback, requestName);
            }
            catch (Exception ex)
            {
                Debug.Console(0, "Aver Camera Exception in PostData:{0}", ex);
            }
        }

        private void HttpCallback(HttpClientResponse response, HTTP_CALLBACK_ERROR error, object requestName)
        {
            try
            {
                if (error != HTTP_CALLBACK_ERROR.COMPLETED)
                {
                    Debug.Console(0, "Aver Camera Http client callback error: {0}", error);
                    return;
                }
                else
                {
                    Debug.Console(1, "Aver Camera Http client response code:{0}", response.Code.ToString());
                    if (response.Code < 200 || response.Code >= 300)
                    {
                        Debug.Console(0, "Aver Camera Http client callback code error: {0}", response.Code);
                        return;
                    }
                    else
                    {
                        Debug.Console(1, "Aver Camera Http client response content:{0}", response.ContentString.ToString());
                        if (response.ContentLength > 0)
                        {
                            ParseMessage((eAverCameraInquiry)requestName, response.ContentString);
                        }
                        else
                        {
                            Debug.Console(0, "Aver Camera Empty http client response");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, "Aver Camera Http client callback exception: {0}", ex.Message);
            }
        }

        private void ParseMessage(eAverCameraInquiry request, string message)
        {
            Debug.Console(1, "Aver Camera Parsing: {0}, request: {1}", message, request.ToString());
            switch (request)
            {
                case eAverCameraInquiry.AutoTrackInquiry:
                    if (message == "trk_tracking_on,3=0")
                    {
                        AutoTrackingOn = false;
                    }
                    else if (message == "trk_tracking_on,3=1")
                    {
                        AutoTrackingOn = true;
                    }
                    break;
                case eAverCameraInquiry.AutoTrackOnCmd:
                    if (message.StartsWith("method return"))
                    {
                        AutoTrackingOn = true;
                    }
                    break;
                case eAverCameraInquiry.AutoTrackOffCmd:
                    if (message.StartsWith("method return"))
                    {
                        AutoTrackingOn = false;
                    }
                    break;
            }
        }

        public override void PollAutoTrack()
        {
            PostData("cgi-bin?Get=trk_tracking_on,3&_=X", eAverCameraInquiry.AutoTrackInquiry);
        }

        /// <summary>
        /// Turn AutoTracking On
        /// </summary>
        public override void SetAutoTrackingOn()
        {
            if (AutoTrackingCapable.BoolValue)
            {
                PostData("cgi-bin?Set=trk_tracking_on,3,1", eAverCameraInquiry.AutoTrackOnCmd);
            }
        }

        /// <summary>
        /// Turn AutoTracking Off
        /// </summary>
        public override void SetAutoTrackingOff()
        {
            PostData("cgi-bin?Set=trk_tracking_on,3,0", eAverCameraInquiry.AutoTrackOffCmd);
        }
    }
}

