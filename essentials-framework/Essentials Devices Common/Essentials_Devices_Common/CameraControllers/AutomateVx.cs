using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;

namespace PepperDash.Essentials.Devices.Common.ImageProcessors
{
    public class AutomateVx : EssentialsDevice, IBridgeAdvanced
    {
        public BoolFeedback OnlineFeedback;
        public BoolFeedback AutoSwitchFeedback;
        public StringFeedback LayoutFeedback;
        public event EventHandler LayoutNamesUpdated;

        public List<AutomateVxLayout> LayoutNames { get; private set; }

        private string hostname;
        private int port;
        private string username;
        private string password;
        private string token;
        private bool onlineStatus;
        private bool autoSwitchOn;
        private string currentLayout;
        private HttpClient client;
        private HttpsClient secureClient;
        private CTimer pollTimer;
        private CTimer offlineTimer;
        private int pollTime = 30000;
        private int offlineTime = 200000;

        public AutomateVx(string key, string name, AutomateVxPropertiesConfig props) :
            base(key, name)
        {
            this.hostname = props.hostname;
            this.port = props.port;
            this.username = props.username;
            this.password = props.password;
            onlineStatus = false;
            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironmentOnProgramStatusEventHandler;
        }

        public override bool CustomActivate()
        {
            BuildClient();
            OnlineFeedback = new BoolFeedback(() => { return onlineStatus; });
            AutoSwitchFeedback = new BoolFeedback(() => { return autoSwitchOn; });
            LayoutFeedback = new StringFeedback(() => { return currentLayout; });

            pollTimer = new CTimer(pollCallback, 0);
            offlineTimer = new CTimer(offlineCallback, offlineTime);
            return true;
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            AutomateVxJoinMap joinMap = new AutomateVxJoinMap(joinStart);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }
            else
            {
                Debug.Console(0, this,
                    "Please update config to use 'eiscapiadvanced' to get all join map features for this device.");
            }

            //Events from SIMPL
            trilist.SetSigTrueAction(joinMap.AutoSwitchOn.JoinNumber, StartAutoSwitch);
            trilist.SetSigTrueAction(joinMap.AutoSwitchOff.JoinNumber, StopAutoSwitch);
            trilist.SetSigTrueAction(joinMap.Sleep.JoinNumber, Sleep);
            trilist.SetSigTrueAction(joinMap.Wake.JoinNumber, Wake);
            trilist.SetSigTrueAction(joinMap.CloseWirecast.JoinNumber, CloseWirecast);
            trilist.SetSigTrueAction(joinMap.GoHome.JoinNumber, GoHome);
            trilist.SetStringSigAction(joinMap.LayoutRecall.JoinNumber, ChangeLayout);

            //Feedback to SIMPL
            trilist.StringInput[joinMap.DeviceName.JoinNumber].StringValue = Name;
            OnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            AutoSwitchFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoSwitchOn.JoinNumber]);
            LayoutFeedback.LinkInputSig(trilist.StringInput[joinMap.LayoutRecall.JoinNumber]);

            LayoutNamesUpdated += (o, a) =>
            {
                if (LayoutNames != null)
                {
                    uint count = 0;
                    foreach (AutomateVxLayout entry in LayoutNames)
                    {
                        trilist.StringInput[joinMap.LayoutNames.JoinNumber + count].StringValue = entry.Name;
                        trilist.StringInput[joinMap.LayoutIds.JoinNumber + count].StringValue = entry.Id;
                        count++;
                        if (count > 9)
                            break;
                    }
                }
            };
        }

        private void BuildClient()
        {
            try
            {
                if (port == 4443)
                {
                    secureClient = new HttpsClient()
                    {
                        UserAgent = "crestron",
                        KeepAlive = false,
                        Accept = "application/json",
                        AllowAutoRedirect = false
                    };
                }
                else
                {
                    client = new HttpClient()
                    {
                        UserAgent = "crestron",
                        KeepAlive = false,
                        Accept = "application/json",
                        AllowAutoRedirect = false
                    };
                }
            }
            catch
            {
                Debug.Console(0, this, "Error building http client");
            }
        }

        private void GetToken()
        {
            try
            {
                if (port == 4443)
                {
                    Debug.Console(1, this, "Getting token for https");
                    HttpsClientRequest req = new HttpsClientRequest();
                    string auth = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(username + ":" + password));
                    string url = string.Format("https://{0}:{1}/get-token", hostname, port);
                    req.Header.ContentType = "application/json";
                    req.Header.SetHeaderValue("Authorization", auth);
                    req.Header.SetHeaderValue("Content-Length", "0");
                    req.Encoding = Encoding.UTF8;
                    req.ContentString = "";
                    req.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                    req.Url.Parse(url);
                    secureClient.DispatchAsyncEx(req, HttpsCallback, "get-token");
                }
                else
                {
                    Debug.Console(1, this, "Getting token for http");
                    HttpClientRequest req = new HttpClientRequest();
                    string auth = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(username + ":" + password));
                    string url = string.Format("http://{0}:{1}/get-token", hostname, port);
                    req.Header.ContentType = "application/json";
                    req.Header.SetHeaderValue("Authorization", auth);
                    req.Header.SetHeaderValue("Content-Length", "0");
                    req.Encoding = Encoding.UTF8;
                    req.ContentString = "";
                    req.RequestType = Crestron.SimplSharp.Net.Http.RequestType.Post;
                    req.Url.Parse(url);
                    client.DispatchAsyncEx(req, HttpCallback, "get-token");
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Exception in gettoken:{0}", ex);
            }
        }

        private void PostData(string data, string requestName)
        {
            try
            {
                if (token != null)
                {
                    if (port == 4443)
                    {
                        Debug.Console(1, this, "Posting https:{0}", requestName);
                        HttpsClientRequest req = new HttpsClientRequest();
                        string url = string.Format("https://{0}:{1}/api/{2}", hostname, port, requestName);
                        req.Header.ContentType = "application/json";
                        req.Header.SetHeaderValue("Authorization", token);
                        req.Header.SetHeaderValue("Content-Length", data.Length.ToString());
                        req.Encoding = Encoding.UTF8;
                        req.ContentString = data;
                        req.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                        req.Url.Parse(url);
                        secureClient.DispatchAsyncEx(req, HttpsCallback, requestName);
                    }
                    else
                    {
                        Debug.Console(1, this, "Posting http:{0}", requestName);
                        HttpClientRequest req = new HttpClientRequest();
                        string url = string.Format("http://{0}:{1}/api/{2}", hostname, port, requestName);
                        req.Header.ContentType = "application/json";
                        req.Header.SetHeaderValue("Authorization", token);
                        req.Header.SetHeaderValue("Content-Length", data.Length.ToString());
                        req.Encoding = Encoding.UTF8;
                        req.ContentString = data;
                        req.RequestType = Crestron.SimplSharp.Net.Http.RequestType.Post;
                        req.Url.Parse(url);
                        client.DispatchAsyncEx(req, HttpCallback, requestName);
                    }
                }
                else
                {
                    Debug.Console(0, this, "No valid token, discarding command");
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Exception in postdata:{0}", ex);
            }
        }

        public void Sleep()
        {
            PostData("", "Sleep");
            Thread.Sleep(1000);
            PostData("", "AutoSwitchStatus");
        }

        public void Wake()
        {
            PostData("", "Wake");
            Thread.Sleep(1000);
            PostData("", "AutoSwitchStatus");
        }

        public void CloseWirecast()
        {
            PostData("", "CloseWirecast");
        }

        public void GoHome()
        {
            PostData("", "GoHome");
        }

        public void StartAutoSwitch()
        {
            PostData("", "StartAutoSwitch");
        }

        public void StopAutoSwitch()
        {
            PostData("", "StopAutoSwitch");
        }

        public void GetAutoSwitchStatus()
        {
            PostData("", "AutoSwitchStatus");
        }

        public void GetLayouts()
        {
            PostData("", "GetLayouts");
        }

        public void ChangeLayout(string layout)
        {
            string json = JsonConvert.SerializeObject(new { id = layout });
            PostData(json, "ChangeLayout");
        }

        public void GetLayoutStatus()
        {
            PostData("", "LayoutStatus");
        }

        public void GetRoomConfigStatus()
        {
            PostData("", "RoomConfigStatus");
        }

        void UpdateLayoutFeedback(string layout)
        {
            if (LayoutNames != null)
            {
                AutomateVxLayout layoutFb = LayoutNames.FirstOrDefault(o => o.Id == layout);
                if (layoutFb != null)
                {
                    currentLayout = layoutFb.Id;
                    LayoutFeedback.FireUpdate();
                }
                else
                {
                    currentLayout = "";
                    LayoutFeedback.FireUpdate();
                }
            }
            else
            {
                currentLayout = "";
                LayoutFeedback.FireUpdate();
            }
        }

        private void pollCallback(object o)
        {
            if (onlineStatus == true)
            {
                GetAutoSwitchStatus();
            }
            else
            {
                GetToken();
            }

            pollTimer.Reset(pollTime);
        }

        private void deviceOnline()
        {
            offlineTimer.Reset(offlineTime);
            if (onlineStatus == false)
            {
                onlineStatus = true;
                OnlineFeedback.FireUpdate();
                GetToken();
            }
        }

        private void offlineCallback(object o)
        {
            onlineStatus = false;
            OnlineFeedback.FireUpdate();
            autoSwitchOn = false;
            AutoSwitchFeedback.FireUpdate();
        }

        private void CrestronEnvironmentOnProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping) return;

            if (pollTimer != null)
            {
                pollTimer.Stop();
                pollTimer.Dispose();
            }

            if (offlineTimer != null)
            {
                offlineTimer.Stop();
                offlineTimer.Dispose();
            }

            client.Dispose();
        }

        private void HttpsCallback(HttpsClientResponse response, HTTPS_CALLBACK_ERROR error, object requestName)
        {
            try
            {
                Debug.Console(1, this, "Https client response code:{0}", response.Code.ToString());
                if (error != HTTPS_CALLBACK_ERROR.COMPLETED)
                {
                    Debug.Console(0, this, "Https client callback error: {0}", error);
                    return;
                }
                else
                {
                    if (response.Code < 200 || response.Code >= 300)
                    {
                        Debug.Console(0, this, "Https client callback code error: {0}", response.Code);
                        return;
                    }
                    else
                    {
                        try
                        {
                            Debug.Console(1, this, "Https client response content:{0}",
                                response.ContentString.ToString());
                            JObject obj = JObject.Parse(response.ContentString);
                            JToken status = obj["status"];
                            if ((string)status == "OK")
                            {
                                deviceOnline();
                                ProcessFeedback((string)requestName, obj);
                            }
                            else if ((string)status == "Error")
                            {
                                Debug.Console(0, this, "Https client JSON returned and error: {0}",
                                    (string)obj["error"]);
                            }
                            else
                            {
                                Debug.Console(0, this, "Unknown https client response");
                            }
                        }
                        catch
                        {
                            Debug.Console(0, this, "Error parsing https client JSON {0}", response.ContentString);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Https client callback exception: {0}", ex.Message);
            }
        }

        private void HttpCallback(HttpClientResponse response, HTTP_CALLBACK_ERROR error, object requestName)
        {
            try
            {
                if (error != HTTP_CALLBACK_ERROR.COMPLETED)
                {
                    Debug.Console(0, this, "Http client callback error: {0}", error);
                    return;
                }
                else
                {
                    Debug.Console(1, this, "Http client response code:{0}", response.Code.ToString());
                    if (response.Code < 200 || response.Code >= 300)
                    {
                        Debug.Console(0, this, "Http client callback code error: {0}", response.Code);
                        return;
                    }
                    else
                    {
                        try
                        {
                            Debug.Console(1, this, "Http client response content:{0}",
                                response.ContentString.ToString());
                            JObject obj = JObject.Parse(response.ContentString);
                            JToken status = obj["status"];
                            if ((string)status == "OK")
                            {
                                deviceOnline();
                                ProcessFeedback((string)requestName, obj);
                            }
                            else if ((string)status == "Error")
                            {
                                Debug.Console(0, this, "Http client JSON returned and error: {0}",
                                    (string)obj["error"]);
                            }
                            else
                            {
                                Debug.Console(0, this, "Unknown http client response");
                            }
                        }
                        catch
                        {
                            Debug.Console(0, this, "Error parsing http client JSON {0}", response.ContentString);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Http client callback exception: {0}", ex.Message);
            }
        }

        private void ProcessFeedback(string requestName, JObject obj)
        {
            string message;

            Debug.Console(1, this, "Processing feedback:{0}", requestName);
            switch (requestName.ToLower())
            {
                case "get-token":
                    token = (string)obj["token"];
                    CrestronInvoke.BeginInvoke(o =>
                    {
                        GetAutoSwitchStatus();
                        GetLayouts();
                        GetLayoutStatus();
                    });
                    break;

                case "startautoswitch":
                    message = (string)obj["message"];
                    if (message.ToLower() == "autoswitching started successfully" ||
                        message.ToLower() == "received startautoswitch command")
                    {
                        autoSwitchOn = true;
                        AutoSwitchFeedback.FireUpdate();
                    }

                    break;

                case "stopautoswitch":
                    message = (string)obj["message"];
                    if (message == "AutoSwitching Stopped Successfully")
                    {
                        autoSwitchOn = false;
                        AutoSwitchFeedback.FireUpdate();
                    }

                    break;

                case "autoswitchstatus":
                    message = (string)obj["message"];
                    if (message == "AutoSwitching in Progress")
                    {
                        if (!autoSwitchOn)
                        {
                            autoSwitchOn = true;
                            AutoSwitchFeedback.FireUpdate();
                        }
                    }
                    else if (message == "No AutoSwitching in Progress")
                    {
                        if (autoSwitchOn)
                        {
                            autoSwitchOn = false;
                            AutoSwitchFeedback.FireUpdate();
                        }
                    }

                    break;

                case "getlayouts":
                    message = (string)obj["message"];
                    if (message == "Layouts loaded successfully")
                    {
                        string layouts = (string)obj["layouts"];
                        LayoutNames = (List<AutomateVxLayout>)JsonConvert.DeserializeObject(layouts);
                        if (LayoutNamesUpdated != null)
                        {
                            LayoutNamesUpdated(this, null);
                        }
                    }

                    break;

                case "changelayout":
                    message = (string)obj["message"];
                    if (message.StartsWith("Changed to Layout") || message.StartsWith("Already on Layout"))
                    {
                        string[] messageArray = message.Split(' ');
                        string layout = messageArray[3];
                        UpdateLayoutFeedback(layout);
                    }

                    break;

                case "layoutstatus":
                    message = (string)obj["message"];
                    if (message == "Current layout queried successfully")
                    {
                        string layout = (string)obj["layout"]["id"];
                        UpdateLayoutFeedback(layout);
                    }

                    break;
            }
        }
    }

    public class AutomateVxLayout
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
    }

    public class AutomateVxFactory : EssentialsDeviceFactory<AutomateVx>
    {
        public AutomateVxFactory()
        {
            TypeNames = new List<string>() { "automatevx" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory attempting to create new AutomateVx Device");
            AutomateVxPropertiesConfig props = Newtonsoft.Json.JsonConvert.DeserializeObject<AutomateVxPropertiesConfig>(
                dc.Properties.ToString());
            return new AutomateVx(dc.Key, dc.Name, props);
        }
    }

    public class AutomateVxPropertiesConfig
    {
        public string hostname { get; set; }
        public int port { get; set; }
        public string username { get; set; }
        public string password { get; set; }
    }

    public class AutomateVxJoinMap : JoinMapBaseAdvanced
    {
        #region Digital

        [JoinName("IsOnline")] public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Is Online Fb",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("AutoSwitchOn")] public JoinDataComplete AutoSwitchOn = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Auto Switch On Get/Set",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("AutoSwitchOff")] public JoinDataComplete AutoSwitchOff = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 3,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Auto Switch Off Set",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Sleep")] public JoinDataComplete Sleep = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 4,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Sleep Set",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Wake")] public JoinDataComplete Wake = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 5,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Wake Set",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("CloseWirecast")] public JoinDataComplete CloseWirecast = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 6,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Close Wirecast Set",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("GoHome")] public JoinDataComplete GoHome = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 7,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Go Home Set",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        #endregion


        #region Analog

        #endregion


        #region Serial

        [JoinName("DeviceName")] public JoinDataComplete DeviceName = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("LayoutRecall")] public JoinDataComplete LayoutRecall = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "LayoutRecall Get/Set",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("LayoutNames")] public JoinDataComplete LayoutNames = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 10,
                JoinSpan = 10
            },
            new JoinMetadata()
            {
                Description = "Layout Names Get",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("LayoutIds")] public JoinDataComplete LayoutIds = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 20,
                JoinSpan = 10
            },
            new JoinMetadata()
            {
                Description = "Layout Ids Get",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        #endregion

        public AutomateVxJoinMap(uint joinStart)
            : base(joinStart, typeof(AutomateVxJoinMap))
        {
        }
    }
}