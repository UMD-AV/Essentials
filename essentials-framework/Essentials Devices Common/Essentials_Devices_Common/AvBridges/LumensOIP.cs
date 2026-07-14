using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Http;
using PepperDash.Core;
using Newtonsoft.Json;
using Crestron.SimplSharpPro.DeviceSupport;
using UmdEssentials.Core;
using UmdEssentials.Core.Bridges;
using UmdEssentials.Core.Config;

namespace UmdEssentials.Devices.Common.LumensOip
{
    public class LumensOipDevice : EssentialsBridgeableDevice, ITxRoutingWithFeedback, IDisposable
    {
        private readonly HttpClient _client;
        private string _basePath;
        private readonly CTimer _pollTimer;
        private readonly string _username;
        private readonly string _password;
        public LumensCommunicationMonitor CommunicationMonitor { get; private set; }

        public ushort InputFb = 999;

        public readonly StringFeedback Input1NameFb;

        public readonly StringFeedback Input2NameFb;

        public readonly StringFeedback Input3NameFb;

        public LumensOipDevice(string key, string name, LumensOipPropertiesConfig config)
            : base(key, name)
        {
            _client = new HttpClient
            {
                TimeoutEnabled = true, Timeout = 5, KeepAlive = false
            };
            _pollTimer = new CTimer(Poll, Timeout.Infinite);
            _basePath = string.Format("http://{0}", config.Host);
            _username = config.Username;
            _password = config.Password;

            string input1Name = config.Input1Name ?? "";
            string input2Name = config.Input2Name ?? "";
            string input3Name = config.Input3Name ?? "";

            VideoSourceNumericFeedback = new IntFeedback(() => InputFb);
            Input1NameFb = new StringFeedback(() => input1Name);
            Input2NameFb = new StringFeedback(() => input2Name);
            Input3NameFb = new StringFeedback(() => input3Name);


            CommunicationMonitor = new LumensCommunicationMonitor(this, 130000, 190000);
            DeviceManager.AddDevice(CommunicationMonitor);
        }

        public override bool CustomActivate()
        {
            CommunicationMonitor.Start();
            Poll(null);
            return base.CustomActivate();
        }

        private void Poll(object o)
        {
            CrestronInvoke.BeginInvoke(obj => { GetSource(); });
            _pollTimer.Reset(60000);
        }

        private void processFeedback(string feedback)
        {
            Debug.Console(0, this, "Fb: {0}", feedback);
            try
            {
                string[] lines = feedback.Split('\n');

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("var SrcCurrentIdx"))
                    {
                        int start = trimmed.IndexOf('"');
                        int end = trimmed.LastIndexOf('"');

                        if (start >= 0 && end > start)
                        {
                            string value = trimmed.Substring(start + 1, end - start - 1);
                            UpdateSourceFb(ushort.Parse(value));
                            CommunicationMonitor.SetOnlineStatus(true);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Console(0, this, "Failed to process feedback {0}", e.Message);
            }
        }

        private void UpdateSourceFb(ushort src)
        {
            if (src != InputFb)
            {
                InputFb = src;
                VideoSourceNumericFeedback.FireUpdate();
                OnSwitchChange(src);
            }
        }

        public void GetSource()
        {
            try
            {
                HttpClientRequest req = new HttpClientRequest();
                string url = string.Format("{0}/{1}", _basePath, "cgi/inquiry.cgi?inqjs=source");
                Debug.Console(0, this, "Getting {0}", url);
                string auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(_username + ":" + _password));
                req.Header.SetHeaderValue("Authorization", "Basic " + auth);
                req.Header.ContentType = "text/plain";
                req.Header.SetHeaderValue("Content-Length", "0");
                req.Encoding = Encoding.ASCII;
                req.RequestType = RequestType.Get;
                req.Url.Parse(url);


                HttpClientResponse response = _client.Dispatch(req);
                if (response.Code == 200) processFeedback(response.ContentString);
            }
            catch (Exception ex)
            {
                Debug.Console(0, "Exception in Get:{0}", ex.Message);
            }
        }

        private void RouteInput(ushort input)
        {
            ushort oldInput = InputFb;
            if (input != oldInput && oldInput != 999)
            {
                UpdateSourceFb(input);
                try
                {
                    HttpClientRequest req = new HttpClientRequest();
                    string url = string.Format("{0}/{1}{2}", _basePath, "cgi/source.cgi?SrcConn=", input);
                    Debug.Console(0, this, "Posting {0}", url);
                    string auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(_username + ":" + _password));
                    req.Header.SetHeaderValue("Authorization", "Basic " + auth);
                    req.Header.ContentType = "text/plain";
                    req.Header.SetHeaderValue("Content-Length", "0");
                    req.Encoding = Encoding.ASCII;
                    req.RequestType = RequestType.Post;
                    req.Url.Parse(url);


                    HttpClientResponse response = _client.Dispatch(req);
                    if (response.Code != 200) UpdateSourceFb(oldInput);
                }
                catch (Exception ex)
                {
                    Debug.Console(0, this, "Exception in Post:{0}", ex.Message);
                }
            }
        }

        private ushort processErrorCode(HttpClientResponse result)
        {
            try
            {
                ErrorCode err = JsonConvert.DeserializeObject<ErrorCode>(result.ContentString);
                return ushort.Parse(err.ErrCode);
            }
            catch (Exception e)
            {
                Debug.Console(0, this, "Exception processing error code: {0}", e.Message);
            }

            return 0;
        }

        private string processSourceError(ushort errorCode)
        {
            switch (errorCode)
            {
                case 1:
                    return "This index is empty in the source list";
                case 2:
                    return "Connected with this source currently";
                case 3:
                    return "Unknown stream protocol";
                case 4:
                    return "Source is a full NDI stream";
                case 5:
                    return "Can't connect to this source";
                default:
                    return "Unknown error";
            }
        }

        private void OnSwitchChange(ushort input)
        {
            RoutingNumericEventArgs e = new RoutingNumericEventArgs(1, input,
                null, null, eRoutingSignalType.Video);

            if (NumericSwitchChange != null) NumericSwitchChange(this, e);
        }


        #region IBridge Members

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            DmTxControllerJoinMap joinMap = new DmTxControllerJoinMap(joinStart);
            if (bridge != null) bridge.AddJoinMap(Key, joinMap);

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

            //Names
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;
            Input1NameFb.LinkInputSig(trilist.StringInput[joinMap.Input1Name.JoinNumber]);
            Input2NameFb.LinkInputSig(trilist.StringInput[joinMap.Input2Name.JoinNumber]);
            Input3NameFb.LinkInputSig(trilist.StringInput[joinMap.Input3Name.JoinNumber]);

            //Video Sync

            //Routing
            trilist.SetUShortSigAction(joinMap.VideoInput.JoinNumber, RouteInput);
            VideoSourceNumericFeedback.LinkInputSig(trilist.UShortInput[joinMap.VideoInput.JoinNumber]);

            Input1NameFb.FireUpdate();
            Input2NameFb.FireUpdate();
            Input3NameFb.FireUpdate();
        }

        public void SetHost(string host)
        {
            _basePath = string.Format("http://{0}", host);
        }

        public void Dispose()
        {
            if (_pollTimer != null)
            {
                _pollTimer.Stop();
                _pollTimer.Dispose();
            }

            if (_client != null) _client.Dispose();
        }

        #endregion

        public RoutingPortCollection<RoutingInputPort> InputPorts
        {
            get { return null; }
        }

        public RoutingPortCollection<RoutingOutputPort> OutputPorts
        {
            get { return null; }
        }

        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            ExecuteNumericSwitch(1, 0, eRoutingSignalType.Video);
        }

        public void ExecuteNumericSwitch(ushort input, ushort output, eRoutingSignalType type)
        {
            RouteInput(input);
        }

        public IntFeedback VideoSourceNumericFeedback { get; private set; }

        public IntFeedback AudioSourceNumericFeedback
        {
            get { return null; }
        }

        public event EventHandler<RoutingNumericEventArgs> NumericSwitchChange;
    }

    public class LumensOipPropertiesConfig
    {
        [JsonProperty("host")] public string Host { get; set; }

        [JsonProperty("username")] public string Username { get; set; }

        [JsonProperty("password")] public string Password { get; set; }

        [JsonProperty("input1Name")] public string Input1Name { get; set; }

        [JsonProperty("input2Name")] public string Input2Name { get; set; }

        [JsonProperty("input3Name")] public string Input3Name { get; set; }
    }

    public class LumensOipFactory : EssentialsDeviceFactory<LumensOipDevice>
    {
        public LumensOipFactory()
        {
            TypeNames = new List<string> { "lumensoip" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Lumens OIP device");

            LumensOipPropertiesConfig config = dc.Properties.ToObject<LumensOipPropertiesConfig>();

            return new LumensOipDevice(dc.Key, dc.Name, config);
        }
    }

    public class ErrorCode
    {
        [JsonProperty("STATUS")] public string Status { get; set; }

        [JsonProperty("ERRCODE")] public string ErrCode { get; set; }
    }

    public class LumensCommunicationMonitor : StatusMonitorBase
    {
        private bool _isStarted;

        public LumensCommunicationMonitor(IKeyed parent, long warningTime, long errorTime) : base(parent, warningTime,
            errorTime)
        {
        }

        public override void Start()
        {
            _isStarted = true;
            StartErrorTimers();
        }

        public override void Stop()
        {
            _isStarted = false;
            StopErrorTimers();
        }

        public void SetOnlineStatus(bool isOnline)
        {
            if (isOnline)
            {
                Status = MonitorStatus.IsOk;
                ResetErrorTimers();
                return;
            }

            if (!_isStarted)
                return;

            StartErrorTimers();
        }
    }
}