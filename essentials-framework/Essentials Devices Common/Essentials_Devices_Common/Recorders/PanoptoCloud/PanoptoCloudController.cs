using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Net.Https;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharp.CrestronXml;
using Crestron.SimplSharp.CrestronXmlLinq;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Devices;
using Formatting = Newtonsoft.Json.Formatting;
using RequestType = Crestron.SimplSharp.Net.Https.RequestType;

namespace PepperDash.Essentials.PanoptoCloud
{
    public class PanoptoCloudController : ReconfigurableBridgableDevice, ICommunicationMonitor
    {
        private readonly string _url;
        private readonly string _username;
        private readonly string _recorderName;
        private readonly CTimer _oauthTimer;
        private readonly CTimer _pollTimer;
        private readonly CTimer _startRecordingStatusTimer;
        private readonly CTimer _resetAdhocTimer;
        private const int _userSearchSize = 8;
        private const int _defaultLength = 60;
        private readonly PanoptoCloudStatusMonitor _monitor;
        private string _token;
        private RecorderInfo _recorder = new RecorderInfo();

        //Ad-hoc recording settings
        private int _recordingLength = _defaultLength;
        private string _currentUser;
        private Guid _currentUserGuid;
        private string _currentFolderName;
        private Guid _currentFolderGuid;
        private string _recordingName;
        private string _recordingDescription;
        private string _startRecordingStatus;
        private readonly KeyValuePair<string, Guid>[] _usernames;
        public readonly StringFeedback CurrentUserFeedback;
        public readonly StringFeedback CurrentFolderFeedback;
        public readonly IntFeedback RecordingLength;
        public readonly StringFeedback[] UsernameFeedback;
        public readonly StringFeedback RecordingNameFeedback;
        public readonly StringFeedback RecordingDescriptionFeedback;

        //Recorder statuses
        public readonly IntFeedback RecorderStatusInt;
        public readonly StringFeedback RecorderStatusString;
        public readonly StringFeedback StartRecordingStatus;
        public readonly BoolFeedback IsRecording;
        public readonly BoolFeedback IsPaused;
        public readonly BoolFeedback IsOnline;

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
                    if (string.IsNullOrEmpty(clientId))
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
                    if (string.IsNullOrEmpty(clientSecret))
                    {
                        CrestronConsole.ConsoleCommandResponse("Client Secret cannot be blank");
                        return;
                    }

                    device.SetClientSecret(clientSecret);
                }, "PANOPTOSECRET", "Format: [Device_Key]:[Client_Secret]", ConsoleAccessLevelEnum.AccessAdministrator);

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

                    string clientPassword = splitString[1];
                    if (string.IsNullOrEmpty(clientPassword))
                    {
                        CrestronConsole.ConsoleCommandResponse("Client password cannot be blank");
                        return;
                    }

                    device.SetClientPassword(clientPassword);
                }, "PANOPTOPASSWORD", "Format: [Device_Key]:[Client_Password]",
                ConsoleAccessLevelEnum.AccessAdministrator);
        }

        public PanoptoCloudController(DeviceConfig config)
            : base(config)
        {
            PanoptoCloudControllerProperties props = config.Properties.ToObject<PanoptoCloudControllerProperties>();
            _url = props.Url;
            _username = props.Username;
            _recorderName = props.RecorderName;

            _oauthTimer = new CTimer(o => UpdateToken(), Timeout.Infinite);

            _pollTimer = new CTimer(o => PollRecorder(), Timeout.Infinite);

            _startRecordingStatusTimer = new CTimer(o => ResetStartRecordingStatus(), Timeout.Infinite);

            _monitor = new PanoptoCloudStatusMonitor(this, 120000, 240000);

            _resetAdhocTimer = new CTimer(o => ResetAdHoc(), Timeout.Infinite);

            IsRecording = new BoolFeedback(() =>
                _recorder != null && (_recorder.State == RemoteRecorderState.Recording ||
                                      _recorder.State == RemoteRecorderState.Paused));

            IsPaused = new BoolFeedback(() => _recorder != null && (_recorder.State == RemoteRecorderState.Paused));

            RecorderStatusInt = new IntFeedback(() =>
                _recorder == null ? (int)RemoteRecorderState.Unknown : (int)_recorder.State);

            RecorderStatusString = new StringFeedback(() =>
                _recorder == null ? RemoteRecorderState.Unknown.ToString() : _recorder.State.ToString());

            StartRecordingStatus = new StringFeedback(() => _startRecordingStatus);

            RecordingLength = new IntFeedback(() => _recordingLength);

            CurrentUserFeedback = new StringFeedback(() => _currentUser);
            CurrentFolderFeedback = new StringFeedback(() => _currentFolderName);
            RecordingNameFeedback = new StringFeedback(() => _recordingName);
            RecordingDescriptionFeedback = new StringFeedback(() => _recordingDescription);

            _usernames = new KeyValuePair<string, Guid>[_userSearchSize];
            UsernameFeedback = new StringFeedback[_userSearchSize];
            for (int i = 0; i < _userSearchSize; i++)
            {
                _usernames[i] = new KeyValuePair<string, Guid>("", Guid.Empty);
                int index = i;
                UsernameFeedback[i] = new StringFeedback(() => _usernames[index].Key);
            }

            IsOnline = new BoolFeedback(() => _monitor.IsOnline);
        }

        public override bool CustomActivate()
        {
            RecorderStatusInt.OutputChange += (sender, args) =>
            {
                IsRecording.FireUpdate();
                IsPaused.FireUpdate();
                RecorderStatusString.FireUpdate();

                if (_recorder != null && _recorder.State == RemoteRecorderState.Recording)
                {
                    SetStartRecordingStatus("Recording Started", 10000);
                }
            };

            RecorderStatusString.OutputChange +=
                (sender, args) => Debug.Console(1, this, "Recorder Status:{0}", args.StringValue);

            IsRecording.FireUpdate();
            IsPaused.FireUpdate();
            IsOnline.FireUpdate();
            RecorderStatusInt.FireUpdate();
            RecorderStatusString.FireUpdate();
            StartRecordingStatus.FireUpdate();
            RecordingLength.FireUpdate();
            CurrentUserFeedback.FireUpdate();
            CurrentFolderFeedback.FireUpdate();
            RecordingNameFeedback.FireUpdate();
            RecordingDescriptionFeedback.FireUpdate();
            for (int i = 0; i < _userSearchSize; i++)
            {
                UsernameFeedback[i].FireUpdate();
            }

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

        public void SetClientPassword(string clientPassword)
        {
            string key = Key + "-" + "ClientPassword";
            eCrestronSecureStorageStatus storageResult = CrestronSecureStorage.Store(key,
                false,
                Encoding.ASCII.GetBytes(clientPassword),
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
            _pollTimer.Reset(500, 60000);
        }

        public bool CheckTokenAndUpdate()
        {
            return !string.IsNullOrEmpty(_token) || UpdateToken();
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

                string clientPassword;
                if (!Utils.TryGetValueFromSecureStorage(Key + "-" + "ClientPassword", out clientPassword))
                {
                    Debug.Console(1, this, "Client Password not set");
                    return false;
                }

                Debug.Console(1, this, "Getting token...");
                PanoptoOauthClient.TokenResponse token =
                    PanoptoOauthClient.GetToken(url, _username, clientPassword, clientId, clientSecret);
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

        public void SetCurrentUser(string name)
        {
            if (_recorder.Id.Equals(Guid.Empty) || string.IsNullOrEmpty(name))
                return;

            _resetAdhocTimer.Reset(300000);
            string url = string.Format("{0}/Panopto/api/v1/users/search?searchQuery={1}", _url, name);

            HttpsClientRequest request = GetDefaultRequestWithAuthHeaders(url, _token, RequestType.Get);

            Debug.Console(1, this, "Attempting to search user: {0}", name);
            using (HttpsClient client = new HttpsClient().WithDefaultSettings())
            {
                bool success = false;
                try
                {
                    HttpsClientResponse result = client.Dispatch(request);
                    success = ProcessUsers(result);
                }
                catch (Exception ex)
                {
                    Debug.Console(1, this, "Error searching user {0}", ex.Message);
                }

                if (!success)
                {
                    _usernames[0] = new KeyValuePair<string, Guid>("No users found", Guid.Empty);
                    UsernameFeedback[0].FireUpdate();
                    CurrentUserFeedback.FireUpdate();

                    for (int i = 1; i < _userSearchSize; i++)
                    {
                        _usernames[i] = new KeyValuePair<string, Guid>(string.Empty, Guid.Empty);
                        UsernameFeedback[i].FireUpdate();
                    }
                }
            }
        }

        private void ResetUsernameSearchList()
        {
            for (int i = 0; i < _userSearchSize; i++)
            {
                _usernames[i] = new KeyValuePair<string, Guid>(string.Empty, Guid.Empty);
                UsernameFeedback[i].FireUpdate();
            }
        }

        public void ResetAdHoc()
        {
            _currentUser = string.Empty;
            _currentUserGuid = Guid.Empty;
            CurrentUserFeedback.FireUpdate();
            _recordingLength = _defaultLength;
            RecordingLength.FireUpdate();
            _recordingName = "";
            RecordingNameFeedback.FireUpdate();
            _recordingDescription = "";
            RecordingDescriptionFeedback.FireUpdate();
            ResetCurrentFolder();
            ResetUsernameSearchList();
        }

        public void SelectCurrentUser(int user)
        {
            _resetAdhocTimer.Reset(300000);
            if (user >= _usernames.Length || user < 0)
            {
                return;
            }

            _currentUser = _usernames[user].Key;
            _currentUserGuid = _usernames[user].Value;
            UpdateUserFolder();
            CurrentUserFeedback.FireUpdate();

            ResetUsernameSearchList();
        }

        private void ResetCurrentFolder()
        {
            if (_recorder.DefaultRecordingFolder != null)
            {
                _currentFolderGuid = _recorder.DefaultRecordingFolder.Id;
                _currentFolderName = _recorder.DefaultRecordingFolder.Name;
            }
            else
            {
                _currentFolderGuid = Guid.Empty;
                _currentFolderName = string.Empty;
            }

            CurrentFolderFeedback.FireUpdate();
        }

        private void UpdateUserFolder()
        {
            if (_currentUserGuid.Equals(Guid.Empty))
            {
                ResetCurrentFolder();
            }
            else
            {
                string url = string.Format("{0}/Panopto/PublicAPI/4.6/SessionManagement.svc", _url);

                HttpsClientRequest request = new HttpsClientRequest { RequestType = RequestType.Post };
                request.Header.AddHeader(new HttpsHeader("SOAPAction",
                    "http://tempuri.org/ISessionManagement/GetPersonalFolderForUser"));
                request.Header.AddHeader(new HttpsHeader("Content-Type", "text/xml"));
                request.Url.Parse(url);

                string content = CreateSoapEnvelope(_currentUserGuid);
                bool success = false;
                if (content != null)
                {
                    request.ContentString = CreateSoapEnvelope(_currentUserGuid);
                    using (HttpsClient client = new HttpsClient())
                    {
                        try
                        {
                            HttpsClientResponse result = client.Dispatch(request);
                            success = ProcessGetPersonalFolderForUser(result);
                        }
                        catch (Exception ex)
                        {
                            Debug.Console(1, this, "Error getting user folder {0}", ex.Message);
                        }
                    }
                }

                if (!success)
                {
                    ResetCurrentFolder();
                }

                CurrentFolderFeedback.FireUpdate();
            }
        }

        private string CreateSoapEnvelope(Guid userGuid)
        {
            string clientPassword;
            if (!Utils.TryGetValueFromSecureStorage(Key + "-" + "ClientPassword", out clientPassword))
            {
                Debug.Console(1, this, "Client Password not set");
                return null;
            }

            // Define XML namespaces
            XNamespace soapenv = "http://schemas.xmlsoap.org/soap/envelope/";
            XNamespace tem = "http://tempuri.org/";
            XNamespace pan = "http://schemas.datacontract.org/2004/07/Panopto.Server.Services.PublicAPI.V40";
            XNamespace pan2 = "http://schemas.datacontract.org/2004/07/Panopto.Server.Services.PublicAPI.V46.Soap";

            // Build the XML structure
            XElement envelope = new XElement(soapenv + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", soapenv),
                new XAttribute(XNamespace.Xmlns + "tem", tem),
                new XAttribute(XNamespace.Xmlns + "pan", pan),
                new XAttribute(XNamespace.Xmlns + "pan2", pan2),
                new XElement(soapenv + "Header"),
                new XElement(soapenv + "Body",
                    new XElement(tem + "GetPersonalFolderForUser",
                        new XElement(tem + "auth",
                            new XElement(pan + "Password", clientPassword),
                            new XElement(pan + "UserKey", _username)
                        ),
                        new XElement(tem + "userId", userGuid.ToString()),
                        new XElement(tem + "allowCreation", "false")
                    )
                )
            );

            XDocument soapEnvelope = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), envelope);
            return soapEnvelope.ToString();
        }

        private bool ProcessGetPersonalFolderForUser(HttpsClientResponse response)
        {
            if (response.Code != 200)
            {
                Debug.Console(1, this, "Error getting user folder... Code:{0}\r{1}", response.Code,
                    response.ContentString);
                return false;
            }

            bool foundId = false;
            bool foundName = false;

            using (StringReader stringReader = new StringReader(response.ContentString))
            using (XmlReader reader =
                   XmlReader.Create(stringReader))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI ==
                        "http://schemas.datacontract.org/2004/07/Panopto.Server.Services.PublicAPI.V46.Soap")
                    {
                        switch (reader.LocalName)
                        {
                            // Check for the start element named "Id" in the expected namespace.
                            case "Id":
                                try
                                {
                                    _currentFolderGuid = new Guid(reader.ReadElementContentAsString());
                                    Debug.Console(1, this, "Success getting user folder guid: {0}\r",
                                        _currentFolderGuid.ToString());
                                    foundId = true;
                                }
                                catch (FormatException)
                                {
                                    // The input was not in a valid GUID format.
                                    Debug.Console(1, this, "Invalid guid format: {0}\r",
                                        reader.ReadElementContentAsString());
                                    return false;
                                }

                                break;
                            // Check for the start element named "Name" in the expected namespace.
                            case "Name":
                                _currentFolderName = reader.ReadElementContentAsString();
                                Debug.Console(1, this, "Success getting user folder name: {0}\r",
                                    _currentFolderName);
                                foundName = true;
                                break;
                        }
                    }
                }
            }

            return foundId && foundName;
        }

        private bool ProcessUsers(HttpsClientResponse response)
        {
            if (response.Code != 200)
            {
                Debug.Console(1, this, "Error processing users... Code:{0}\r{1}", response.Code,
                    response.ContentString);
                return false;
            }

            using (StreamReader stream = new StreamReader(response.ContentStream))
            {
                JsonTextReader reader = new JsonTextReader(stream);
                JsonSerializer serializer = new JsonSerializer();

                UserResults users = serializer.Deserialize<UserResults>(reader);
                Debug.Console(2, this, "Processing users...\r{0}",
                    JsonConvert.SerializeObject(users, Formatting.Indented));

                if (users.Results.Count > 50)
                {
                    users.Results = users.Results.Take(50).ToList();
                }

                users.Results.RemoveAll(x => x.Id == Guid.Empty);

                if (users.Results.Count > 0)
                {
                    for (int i = 0; i < _userSearchSize; i++)
                    {
                        if (i < users.Results.Count)
                        {
                            _usernames[i] =
                                new KeyValuePair<string, Guid>(users.Results[i].Username, users.Results[i].Id);
                        }
                        else
                        {
                            _usernames[i] = new KeyValuePair<string, Guid>(string.Empty, Guid.Empty);
                        }

                        UsernameFeedback[i].FireUpdate();
                    }

                    return true;
                }

                return false;
            }
        }

        public void SetRecordingLength(ushort value)
        {
            if (value == 0)
            {
                return;
            }

            _resetAdhocTimer.Reset(300000);
            _recordingLength = Math.Min((ushort)120, Math.Max((ushort)15, value));
            RecordingLength.FireUpdate();
        }

        public void SetRecordingName(string value)
        {
            _resetAdhocTimer.Reset(300000);
            _recordingName = value;
            RecordingNameFeedback.FireUpdate();
        }

        public void SetRecordingDescription(string value)
        {
            _resetAdhocTimer.Reset(300000);
            _recordingDescription = value;
            RecordingDescriptionFeedback.FireUpdate();
        }

        public void PollRecorder()
        {
            if (!CheckTokenAndUpdate())
            {
                Debug.Console(1, this, "Cannot poll recorder; no token");
                return;
            }

            if (string.IsNullOrEmpty(Name))
            {
                Debug.Console(1, this, "Cannot poll recorder, recorder name is not set");
                return;
            }

            _recorder = GetRecorder(_recorderName, _url, _token);
            if (_recorder.DefaultRecordingFolder != null && _currentFolderGuid != _recorder.DefaultRecordingFolder.Id)
            {
                ResetCurrentFolder();
            }

            RecorderStatusInt.FireUpdate();

            Debug.Console(1, this, "Recorder Status:\r{0}",
                JsonConvert.SerializeObject(_recorder, Formatting.Indented));
        }

        private void SetStartRecordingStatus(string value, long resetTime)
        {
            _startRecordingStatusTimer.Reset(resetTime);
            _startRecordingStatus = value;
            StartRecordingStatus.FireUpdate();
        }

        private void ResetStartRecordingStatus()
        {
            _startRecordingStatus = "";
            StartRecordingStatus.FireUpdate();
        }

        public void StartRecording()
        {
            if (_recorder.Id.Equals(Guid.Empty))
                return;

            SetStartRecordingStatus("Starting recording. Please wait...", 180000);
            const string path = "/Panopto/api/v1/scheduledRecordings?resolveConflicts=true";
            string url = string.Format("{0}{1}", _url, path);

            StartRecordingRequest body = new StartRecordingRequest
            {
                Name = string.IsNullOrEmpty(_recordingName) == false
                    ? _recordingName
                    : Name + " " + DateTime.Now.ToString("g"),
                Description = string.IsNullOrEmpty(_recordingDescription) == false
                    ? _recordingDescription
                    : Name + " " + DateTime.Now.ToString("g"),
                Recorders = new List<Recorder> { new Recorder { RemoteRecorderId = _recorder.Id } },
                //Subtract 5 minutes to allow for a possible clock drift on devices
                StartTime = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5)),
                EndTime = DateTime.UtcNow.AddMinutes(_recordingLength),
                FolderId = _currentFolderGuid
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
                    ProcessStartRecording(result);
                }
                catch (Exception ex)
                {
                    SetStartRecordingStatus(string.Format("Failed to start recording: {0}", ex.Message), 60000);
                    Debug.Console(1, this, "Error starting recording {0}", ex.Message);
                }
            }
        }

        public void ProcessStartRecording(HttpsClientResponse response)
        {
            if (response.Code != 200)
            {
                SetStartRecordingStatus(string.Format("Failed to start recording: {0}", response.Code), 60000);
                Debug.Console(1, this, "Error starting recording... Code:{0}\r{1}", response.Code,
                    response.ContentString);
            }
            else
            {
                using (StreamReader stream = new StreamReader(response.ContentStream))
                {
                    JsonTextReader reader = new JsonTextReader(stream);
                    JsonSerializer serializer = new JsonSerializer();

                    ScheduledRecording currentRecording = serializer.Deserialize<ScheduledRecording>(reader);
                    Debug.Console(2, this, "Starting recording...\r{0}",
                        JsonConvert.SerializeObject(currentRecording, Formatting.Indented));
                    Debug.Console(2, this, "Start time:{0}", currentRecording.StartTime.ToShortTimeString());
                    Debug.Console(2, this, "End time:{0}", currentRecording.EndTime.ToShortTimeString());

                    SetStartRecordingStatus("Recording requested. Synching with recorder", Timeout.Infinite);
                }
            }
        }

        public RecorderInfo GetRecorder(string name, string url, string token)
        {
            RecorderInfo defaultRecorderInfo = new RecorderInfo();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(token))
                return defaultRecorderInfo;

            const string path = "/Panopto/api/v1/remoteRecorders/search";
            string fullUrl = string.Format("{0}{1}?searchQuery={2}", url, path, name);

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

        public static RecorderInfo ParseRecordingInfo(string name, HttpsClientResponse response)
        {
            using (StreamReader stream = new StreamReader(response.ContentStream))
            {
                JsonTextReader reader = new JsonTextReader(stream);
                JsonSerializer serializer = new JsonSerializer();

                RemoteRecorderSearchResult results = serializer.Deserialize<RemoteRecorderSearchResult>(reader);
                return results.Results.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ??
                       new RecorderInfo();
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

            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;

            trilist.SetSigTrueAction(joinMap.Start.JoinNumber, StartRecording);
            trilist.SetUShortSigAction(joinMap.RecordingLength.JoinNumber, SetRecordingLength);
            trilist.SetStringSigAction(joinMap.CurrentUser.JoinNumber, SetCurrentUser);
            trilist.SetSigTrueAction(joinMap.ResetAdHoc.JoinNumber, ResetAdHoc);
            trilist.SetStringSigAction(joinMap.SetRecordingName.JoinNumber, SetRecordingName);
            trilist.SetStringSigAction(joinMap.SetRecordingDescription.JoinNumber, SetRecordingDescription);

            IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.RecorderOnline.JoinNumber]);
            IsRecording.LinkInputSig(trilist.BooleanInput[joinMap.IsRecording.JoinNumber]);
            IsPaused.LinkInputSig(trilist.BooleanInput[joinMap.IsPaused.JoinNumber]);
            RecordingLength.LinkInputSig(trilist.UShortInput[joinMap.RecordingLength.JoinNumber]);
            RecorderStatusInt.LinkInputSig(trilist.UShortInput[joinMap.RecorderStateVal.JoinNumber]);
            RecorderStatusString.LinkInputSig(trilist.StringInput[joinMap.RecorderState.JoinNumber]);
            StartRecordingStatus.LinkInputSig(trilist.StringInput[joinMap.StartRecordingStatus.JoinNumber]);
            CurrentUserFeedback.LinkInputSig(trilist.StringInput[joinMap.CurrentUser.JoinNumber]);
            CurrentFolderFeedback.LinkInputSig(trilist.StringInput[joinMap.CurrentFolder.JoinNumber]);
            RecordingNameFeedback.LinkInputSig(trilist.StringInput[joinMap.SetRecordingName.JoinNumber]);
            RecordingDescriptionFeedback.LinkInputSig(trilist.StringInput[joinMap.SetRecordingDescription.JoinNumber]);

            for (int i = 0; i < _userSearchSize; i++)
            {
                int index = i;
                UsernameFeedback[i].LinkInputSig(trilist.StringInput[joinMap.UserSearchResults.JoinNumber + (uint)i]);
                trilist.SetSigTrueAction(joinMap.SelectCurrentUser.JoinNumber + (uint)i,
                    () => SelectCurrentUser(index));
            }
        }

        public StatusMonitorBase CommunicationMonitor
        {
            get { return _monitor; }
        }

        public class PanoptoCloudControllerProperties
        {
            public string Url { get; set; }
            public string Username { get; set; }
            public string RecorderName { get; set; }
        }
    }
}