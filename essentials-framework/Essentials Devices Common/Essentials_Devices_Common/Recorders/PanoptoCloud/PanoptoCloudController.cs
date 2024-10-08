using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Net.Https;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Devices;
using RequestType = Crestron.SimplSharp.Net.Https.RequestType;

namespace PepperDash.Essentials.PanoptoCloud
{
    public class PanoptoCloudController : ReconfigurableBridgableDevice, ICommunicationMonitor
    {
        private readonly string _url;
        private readonly string _username;
        private readonly string _password;
        private readonly CTimer _oauthTimer;
        private readonly CTimer _pollTimer;
        private readonly CTimer _recordingTimer;

        private readonly PanoptoCloudStatusMonitor _monitor;

        private string _token;
        private RecoderInfo _recorder = new RecoderInfo();

        private int _defaultLength = 90;
        private Guid _currentRecordingId;
        private string _currentRecordingName;
        private DateTime _currentRecordingStartTime;
        private DateTime _currentRecordingEndTime;

        public readonly IntFeedback RecorderStatusInt;
        public readonly StringFeedback RecorderStatusString;

        public readonly BoolFeedback IsRecording;
        public readonly BoolFeedback IsPaused;
        public readonly BoolFeedback IsOnline;
        public readonly StringFeedback NameFeedback;
        public readonly StringFeedback CurrentRecordingId;
        public readonly StringFeedback CurrentRecordingName;
        public readonly StringFeedback CurrentRecordingStartTime;
        public readonly StringFeedback CurrentRecordingEndTime;
        public readonly StringFeedback CurrentRecordingLength;
        public readonly StringFeedback CurrentRecordingMinutesRemaining;
        public readonly IntFeedback DefaultLength;
        public readonly BoolFeedback NextRecordingExists;

        static PanoptoCloudController()
        {
            CrestronConsole.AddNewConsoleCommand(
                s =>
                {
                    string[] splitString = s.Split(':');
                    PanoptoCloudController device =
                        DeviceManager.AllDevices.OfType<PanoptoCloudController>().FirstOrDefault(x =>
                            x.Key.Equals(splitString[0], StringComparison.OrdinalIgnoreCase));

                    if (device == null)
                    {
                        CrestronConsole.ConsoleCommandResponse("Device not found");
                        return;
                    }

                    string clientId = splitString[1];
                    if (String.IsNullOrEmpty(clientId))
                    {
                        CrestronConsole.ConsoleCommandResponse("Client Id cannot be blank");
                        return;
                    }

                    device.SetClientId(clientId);
                }, "PANOPTOCLIENT", "Format: [Device_Key]:[Client_Id]", ConsoleAccessLevelEnum.AccessAdministrator);

            CrestronConsole.AddNewConsoleCommand(
                s =>
                {
                    string[] splitString = s.Split(':');
                    PanoptoCloudController device =
                        DeviceManager.AllDevices.OfType<PanoptoCloudController>().FirstOrDefault(x =>
                            x.Key.Equals(splitString[0], StringComparison.OrdinalIgnoreCase));

                    if (device == null)
                    {
                        CrestronConsole.ConsoleCommandResponse("Device not found");
                        return;
                    }

                    string clientSecret = splitString[1];
                    if (String.IsNullOrEmpty(clientSecret))
                    {
                        CrestronConsole.ConsoleCommandResponse("Client Secret cannot be blank");
                        return;
                    }

                    device.SetClientSecret(clientSecret);
                }, "PANOPTOSECRET", "Format: [Device_Key]:[Client_Secret]", ConsoleAccessLevelEnum.AccessAdministrator);
        }

        public PanoptoCloudController(DeviceConfig config)
            : base(config)
        {
            PanoptoCloudControllerProperties props = config.Properties.ToObject<PanoptoCloudControllerProperties>();
            _url = props.Url;
            _username = props.Username;
            _password = props.Password;

            _oauthTimer = new CTimer(o => _token = String.Empty, Timeout.Infinite);

            _pollTimer = new CTimer(o => PollRecorder(), Timeout.Infinite);

            _recordingTimer = new CTimer(o => PollCurrentRecording(), Timeout.Infinite);

            _monitor = new PanoptoCloudStatusMonitor(this, 30000, 60000);

            IsRecording = new BoolFeedback(() =>
                _recorder != null && (_recorder.State == RemoteRecorderState.Recording ||
                                      _recorder.State == RemoteRecorderState.Paused));

            IsPaused = new BoolFeedback(() => _recorder != null && (_recorder.State == RemoteRecorderState.Paused));

            RecorderStatusInt = new IntFeedback(() =>
                _recorder == null ? (int)RemoteRecorderState.Unknown : (int)_recorder.State);

            RecorderStatusString = new StringFeedback(() =>
                _recorder == null ? RemoteRecorderState.Unknown.ToString() : _recorder.State.ToString());

            CurrentRecordingStartTime = new StringFeedback(() =>
                _currentRecordingId != Guid.Empty ? _currentRecordingStartTime.ToString("G") : String.Empty);

            CurrentRecordingEndTime = new StringFeedback(() =>
                _currentRecordingId != Guid.Empty ? _currentRecordingEndTime.ToString("G") : String.Empty);

            CurrentRecordingName = new StringFeedback(() => _currentRecordingName);

            CurrentRecordingId = new StringFeedback(() =>
                _currentRecordingId != Guid.Empty ? _currentRecordingId.ToString("D") : String.Empty);

            CurrentRecordingLength = new StringFeedback(() =>
                _currentRecordingId.CurrentRecordingLength(_currentRecordingStartTime, _currentRecordingEndTime));

            CurrentRecordingMinutesRemaining = new StringFeedback(() =>
                _currentRecordingId.CurrentRecordingTimeRemaining(_currentRecordingEndTime));

            DefaultLength = new IntFeedback(() => _defaultLength);

            NextRecordingExists = new BoolFeedback(() => false); // TODO [] implement next recording logic

            NameFeedback = new StringFeedback(() => Name);

            IsOnline = new BoolFeedback(() => _monitor.IsOnline);
        }

        public override bool CustomActivate()
        {
            RecorderStatusInt.OutputChange += (sender, args) =>
            {
                IsRecording.FireUpdate();
                IsPaused.FireUpdate();
                RecorderStatusString.FireUpdate();
            };

            CurrentRecordingEndTime.OutputChange += (sender, args) =>
            {
                CurrentRecordingLength.FireUpdate();
                CurrentRecordingMinutesRemaining.FireUpdate();
            };

            RecorderStatusString.OutputChange +=
                (sender, args) => Debug.Console(1, this, "Recorder Status:{0}", args.StringValue);

            IsRecording.FireUpdate();
            IsPaused.FireUpdate();
            IsOnline.FireUpdate();
            RecorderStatusInt.FireUpdate();
            RecorderStatusString.FireUpdate();
            CurrentRecordingId.FireUpdate();
            CurrentRecordingName.FireUpdate();
            CurrentRecordingStartTime.FireUpdate();
            CurrentRecordingName.FireUpdate();
            DefaultLength.FireUpdate();
            NameFeedback.FireUpdate();

            return base.CustomActivate();
        }

        public void SetClientId(string clientId)
        {
            string key = Key + "-" + "ClientId";
            eCrestronSecureStorageStatus storageResult = CrestronSecureStorage.Store(key,
                false,
                Encoding.ASCII.GetBytes(clientId),
                Encoding.ASCII.GetBytes(key));

            if (storageResult != eCrestronSecureStorageStatus.Ok)
            {
                Debug.Console(1, this, "Failed to store clientId");
                return;
            }

            CrestronSecureStorage.Flush();
            Debug.Console(1, this, "Succesfully stored clientId");
        }

        public void SetClientSecret(string clientSecret)
        {
            string key = Key + "-" + "ClientSecret";
            eCrestronSecureStorageStatus storageResult = CrestronSecureStorage.Store(key,
                false,
                Encoding.ASCII.GetBytes(clientSecret),
                Encoding.ASCII.GetBytes(key));

            if (storageResult != eCrestronSecureStorageStatus.Ok)
            {
                Debug.Console(1, this, "Failed to store clientSecret");
                return;
            }

            CrestronSecureStorage.Flush();
            Debug.Console(1, this, "Succesfully stored clientSecret");
        }

        public override void Initialize()
        {
            _pollTimer.Reset(500, 5000);
        }

        public bool CheckTokenAndUpdate()
        {
            return !String.IsNullOrEmpty(_token) || UpdateToken();
        }

        public bool UpdateToken()
        {
            const string path = "/Panopto/oauth2/connect/token";
            string url = _url + path;
            try
            {
                string clientId;
                if (!Utils.TryGetValueFromSecureStorage(Key + "-" + "ClientId", out clientId))
                {
                    Debug.Console(1, this, "Client Id not set");
                    return false;
                }

                string clientSecret;
                if (!Utils.TryGetValueFromSecureStorage(Key + "-" + "ClientSecret", out clientSecret))
                {
                    Debug.Console(1, this, "Client Secret not set");
                    return false;
                }

                Debug.Console(1, this, "Getting token...");
                PanoptoOauthClient.TokenResponse token =
                    PanoptoOauthClient.GetToken(url, _username, _password, clientId, clientSecret);
                _token = token.AccessToken;

                int expireTime = token.ExpiresIn * 1000 - 500;
                _oauthTimer.Reset(expireTime);
                Debug.Console(1, this, "Success!  Token expires at: {0}",
                    DateTime.Now.AddMilliseconds(expireTime).ToShortTimeString());
                return true;
            }
            catch (Exception ex)
            {
                _oauthTimer.Reset();
                Debug.Console(1, this, "Caught an error getting the token: {0}{1}", ex.Message, ex.StackTrace);
                return false;
            }
        }

        public void SetDeviceName(string name)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException("name");

            Name = name;
            Config.Name = Name;
            SetConfig(Config);
            NameFeedback.FireUpdate();
            PollRecorder();
        }

        public void IncrementDefaultLength(ushort inc)
        {
            _defaultLength += inc;
            if (_defaultLength < 15)
            {
                _defaultLength = 15;
            }

            DefaultLength.FireUpdate();
        }

        public void DecrementDefaultLength(ushort dec)
        {
            _defaultLength -= dec;
            if (_defaultLength < 15)
            {
                _defaultLength = 15;
            }

            DefaultLength.FireUpdate();
        }

        public void SetDefaultLength(ushort value)
        {
            _defaultLength = value;
            if (_defaultLength < 15)
            {
                _defaultLength = 15;
            }

            DefaultLength.FireUpdate();
        }

        public bool PollRecorder()
        {
            if (!CheckTokenAndUpdate())
            {
                Debug.Console(1, this, "Cannot poll recorder; no token");
                return false;
            }

            if (String.IsNullOrEmpty(Name))
            {
                Debug.Console(1, this, "Cannot poll recorder, recorder name is not set");
                return false;
            }

            _recorder = GetRecorder(Name, _url, _token);
            if (_recorder == null)
                throw new Exception("Something went wrong... this shouldn't have happened");

            RecorderStatusInt.FireUpdate();

            Debug.Console(1, this, "Recorder Status:\r{0}",
                JsonConvert.SerializeObject(_recorder, Formatting.Indented));
            return _recorder.Id.Equals(Guid.Empty);
        }

        public void StartRecording()
        {
            if (_recorder.Id.Equals(Guid.Empty))
                PollRecorder();

            if (_recorder.Id.Equals(Guid.Empty))
                return;

            const string path = "/Panopto/api/v1/scheduledRecordings?resolveConflicts=false";
            string url = String.Format("{0}{1}", _url, path);

            StartRecordingRequest body = new StartRecordingRequest
            {
                Name = Name + " " + DateTime.Now.ToString("g"),
                Description = Name + " " + DateTime.Now.ToString("g"),
                Recorders = new List<Recorder> { new Recorder { RemoteRecorderId = _recorder.Id } },
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(_defaultLength),
                FolderId = _recorder.DefaultRecordingFolder.Id
            };

            HttpsClientRequest request = GetDefaultRequestWithAuthHeaders(url, _token, RequestType.Post);

            request.ContentString = JsonConvert.SerializeObject(body);
            request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

            Debug.Console(1, this, "Attempting to start recording:{0}]\r{1}", request.Url.Url, request.ContentString);
            using (HttpsClient client = new HttpsClient().WithDefaultSettings())
            {
                try
                {
                    HttpsClientResponse result = client.Dispatch(request);
                    ProcessCurrentRecording(result);
                }
                catch (Exception ex)
                {
                    Debug.Console(1, this, "Error starting recording {0}", ex.Message);
                }
            }
        }

        public void StopRecording()
        {
            if (_recorder.Id.Equals(Guid.Empty) && !PollRecorder())
                return;

            if (_currentRecordingId == Guid.Empty)
            {
                Debug.Console(1, this, "Cannot stop recording, current recording id is not set");
                return;
            }

            const string path = "{0}/Panopto/api/v1/scheduledRecordings/{1}";
            string url = String.Format(path, _url, _currentRecordingId);

            var body = new
            {
                EndTime = DateTime.UtcNow,
            };

            HttpsClientRequest request = GetDefaultRequestWithAuthHeaders(url, _token, RequestType.Put);

            request.ContentString = JsonConvert.SerializeObject(body);
            request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

            Debug.Console(1, this, "Attempting to stop recording:{0}\r{1}", request.Url.Url, request.ContentString);
            using (HttpsClient client = new HttpsClient().WithDefaultSettings())
            {
                try
                {
                    HttpsClientResponse result = client.Dispatch(request);
                    ProcessCurrentRecording(result);
                }
                catch (Exception ex)
                {
                    Debug.Console(1, this, "Error stopping recording {0}", ex.Message);
                }
            }
        }

        public void PauseRecording()
        {
            throw new NotImplementedException();
        }

        public void ResumeRecording()
        {
            throw new NotImplementedException();
        }

        public void ExtendRecording()
        {
            const int defaultExtend = 15;
            ExtendRecording(defaultExtend);
        }

        public void ExtendRecording(int minutes)
        {
            if (_recorder.Id.Equals(Guid.Empty) && !PollRecorder())
                return;

            if (_currentRecordingId == Guid.Empty)
            {
                Debug.Console(1, this, "Cannot extend recording, current recording id is not set");
                return;
            }

            const string path = "{0}/Panopto/api/v1/scheduledRecordings/{1}?resolveConflicts=false";
            string url = String.Format(path, _url, _currentRecordingId);

            HttpsClientRequest request = GetDefaultRequestWithAuthHeaders(url, _token, RequestType.Put);
            var body = new
            {
                EndTime = _currentRecordingEndTime.AddMinutes(minutes)
            };

            request.ContentString = JsonConvert.SerializeObject(body);
            request.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));

            Debug.Console(1, this, "Attempting to extend recording:{0} {1}", request.Url.Url, request.ContentString);
            using (HttpsClient client = new HttpsClient().WithDefaultSettings())
            {
                try
                {
                    HttpsClientResponse result = client.Dispatch(request);
                    ProcessCurrentRecording(result);
                }
                catch (Exception ex)
                {
                    Debug.Console(1, this, "Error extending recording {0}", ex.Message);
                }
            }
        }

        public void PollCurrentRecording()
        {
            if (_recorder.Id == Guid.Empty && !PollRecorder())
                return;

            if (_currentRecordingId == Guid.Empty)
            {
                Debug.Console(1, this, "Cannot get recording, current recording id is not set");
                return;
            }

            const string path = "{0}/Panopto/api/v1/scheduledRecordings/{1}";
            string url = String.Format(path, _url, _currentRecordingId);

            HttpsClientRequest request = GetDefaultRequestWithAuthHeaders(url, _token, RequestType.Get);

            Debug.Console(1, this, "Polling current recording:{0}", request.Url.Url);
            using (HttpsClient client = new HttpsClient().WithDefaultSettings())
            {
                try
                {
                    HttpsClientResponse result = client.Dispatch(request);
                    ProcessCurrentRecording(result);
                }
                catch (Exception ex)
                {
                    Debug.Console(1, this, "Error polling recording {0}", ex.Message);
                }
            }
        }

        public void ProcessCurrentRecording(HttpsClientResponse response)
        {
            if (response.Code != 200)
            {
                Debug.Console(1, this, "Error processing recording... Code:{0}\r{1}", response.Code,
                    response.ContentString);
                _recordingTimer.Reset(5000);
            }
            else
            {
                using (StreamReader stream = new StreamReader(response.ContentStream))
                {
                    JsonTextReader reader = new JsonTextReader(stream);
                    JsonSerializer serializer = new JsonSerializer();

                    ScheduledRecording currentRecording = serializer.Deserialize<ScheduledRecording>(reader);
                    Debug.Console(2, this, "Processing recording...\r{0}",
                        JsonConvert.SerializeObject(currentRecording, Formatting.Indented));
                    Debug.Console(2, this, "Start time:{0}", currentRecording.StartTime.ToShortTimeString());
                    Debug.Console(2, this, "End time:{0}", currentRecording.EndTime.ToShortTimeString());

                    if (DateTime.UtcNow >= currentRecording.EndTime.ToUniversalTime())
                    {
                        Debug.Console(1, this, "Recording is over... clearing");
                        _currentRecordingId = Guid.Empty;
                        _currentRecordingName = String.Empty;
                        _recordingTimer.Stop();
                    }
                    else
                    {
                        _currentRecordingId = currentRecording.Id;
                        _currentRecordingName = currentRecording.Name;
                        _currentRecordingStartTime = currentRecording.StartTime;
                        _currentRecordingEndTime = currentRecording.EndTime;
                        _recordingTimer.Reset(5000);
                    }
                }
            }

            CurrentRecordingStartTime.FireUpdate();
            CurrentRecordingEndTime.FireUpdate();
            CurrentRecordingName.FireUpdate();
            CurrentRecordingId.FireUpdate();
        }

        public RecoderInfo GetRecorder(string name, string url, string token)
        {
            RecoderInfo defaultRecorderInfo = new RecoderInfo();
            if (String.IsNullOrEmpty(name) || String.IsNullOrEmpty(token))
                return defaultRecorderInfo;

            const string path = "/Panopto/api/v1/remoteRecorders/search";
            string fullUrl = String.Format("{0}{1}?searchQuery={2}", url, path, name);

            Debug.Console(1, "Searching for recorder name:{0}...", name);
            using (HttpsClient client = new HttpsClient())
            {
                try
                {
                    HttpsClientRequest request = GetDefaultRequestWithAuthHeaders(fullUrl, token, RequestType.Get);
                    HttpsClientResponse response = client.Dispatch(request);

                    if (response != null)
                    {
                        int responseCode = response.Code;

                        _monitor.SetOnlineStatus(responseCode == 200);
                    }
                    else
                    {
                        _monitor.SetOnlineStatus(false);
                    }

                    IsOnline.FireUpdate();
                    return ParseRecordingInfo(name, response);
                }
                catch (Exception ex)
                {
                    Debug.Console(1, "Error searching for recorder {0}{1}", ex.Message, ex.StackTrace);
                    return defaultRecorderInfo;
                }
            }
        }

        public static RecoderInfo ParseRecordingInfo(string name, HttpsClientResponse response)
        {
            using (StreamReader stream = new StreamReader(response.ContentStream))
            {
                JsonTextReader reader = new JsonTextReader(stream);
                JsonSerializer serializer = new JsonSerializer();

                RemoteRecoderSearchResult results = serializer.Deserialize<RemoteRecoderSearchResult>(reader);
                return results.Results.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ??
                       new RecoderInfo();
            }
        }

        public static HttpsClientRequest GetDefaultRequestWithAuthHeaders(string url, string token, RequestType type)
        {
            HttpsClientRequest request = new HttpsClientRequest { RequestType = type };
            request.Header.AddHeader(new HttpsHeader("Authorization", "Bearer " + token));
            request.Url.Parse(url);
            return request;
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            PanoptoCloudControllerJoinMap joinMap = new PanoptoCloudControllerJoinMap(joinStart);
            if (bridge != null)
                bridge.AddJoinMap(Key, joinMap);

            const int defaultLengthIncrement = 5;

            trilist.SetSigTrueAction(joinMap.Start.JoinNumber, StartRecording);
            trilist.SetSigTrueAction(joinMap.Stop.JoinNumber, StopRecording);
            trilist.SetSigTrueAction(joinMap.Pause.JoinNumber, PauseRecording);
            trilist.SetSigTrueAction(joinMap.Resume.JoinNumber, ResumeRecording);
            trilist.SetSigTrueAction(joinMap.Extend.JoinNumber, ExtendRecording);
            trilist.SetSigTrueAction(joinMap.IncLength.JoinNumber,
                () => IncrementDefaultLength(defaultLengthIncrement));
            trilist.SetSigTrueAction(joinMap.DecLength.JoinNumber,
                () => DecrementDefaultLength(defaultLengthIncrement));
            trilist.SetUShortSigAction(joinMap.DefaultRecordingLength.JoinNumber, SetDefaultLength);
            trilist.SetStringSigAction(joinMap.RecorderName.JoinNumber, SetDeviceName);

            IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.RecorderOnline.JoinNumber]);
            IsRecording.LinkInputSig(trilist.BooleanInput[joinMap.IsRecording.JoinNumber]);
            IsPaused.LinkInputSig(trilist.BooleanInput[joinMap.IsPaused.JoinNumber]);
            NextRecordingExists.LinkInputSig(trilist.BooleanInput[joinMap.NextRecordingExists.JoinNumber]);
            DefaultLength.LinkInputSig(trilist.UShortInput[joinMap.DefaultRecordingLength.JoinNumber]);
            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.RecorderName.JoinNumber]);
            CurrentRecordingId.LinkInputSig(trilist.StringInput[joinMap.CurrentRecordingId.JoinNumber]);
            CurrentRecordingName.LinkInputSig(trilist.StringInput[joinMap.CurrentRecordingName.JoinNumber]);
            CurrentRecordingStartTime.LinkInputSig(trilist.StringInput[joinMap.CurrentRecordingStartTime.JoinNumber]);
            CurrentRecordingEndTime.LinkInputSig(trilist.StringInput[joinMap.CurrentRecordingEndTime.JoinNumber]);
            CurrentRecordingLength.LinkInputSig(trilist.StringInput[joinMap.CurrentRecordingLength.JoinNumber]);
            CurrentRecordingMinutesRemaining.LinkInputSig(
                trilist.StringInput[joinMap.CurrentRecordingMinutesRemaining.JoinNumber]);
        }

        public StatusMonitorBase CommunicationMonitor
        {
            get { return _monitor; }
        }

        public class PanoptoCloudControllerProperties
        {
            public string Url { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string ClientId { get; set; }
            public string ClientSecret { get; set; }
        }
    }
}