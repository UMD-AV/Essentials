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
using PepperDash.Essentials.Core.Recording;
using Formatting = Newtonsoft.Json.Formatting;
using RequestType = Crestron.SimplSharp.Net.Https.RequestType;

namespace PepperDash.Essentials.PanoptoCloud
{
    public class PanoptoCloudController : ReconfigurableBridgableDevice, ICommunicationMonitor, IRecordingController
    {
        private readonly string _url;
        private readonly string _username;
        private readonly string _recorderName;
        private readonly CTimer _oauthTimer;
        private readonly CTimer _pollTimer;
        private readonly CTimer _startRecordingStatusTimer;
        private readonly PanoptoCloudStatusMonitor _monitor;
        private string _token;
        private RecorderInfo _recorder = new RecorderInfo();
        private string _startRecordingStatus;

        //Recorder statuses
        public StringFeedback StartRecordingStatus { get; set; }
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

            StartRecordingStatus = new StringFeedback(() => _startRecordingStatus);

            IsOnline = new BoolFeedback(() => _monitor.IsOnline);
        }

        public override bool CustomActivate()
        {
            IsOnline.FireUpdate();
            StartRecordingStatus.FireUpdate();
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
                Debug.Console(0, this, "Failed to store clientId");
                return;
            }

            CrestronSecureStorage.Flush();
            Debug.Console(0, this, "Succesfully stored clientId");
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
                Debug.Console(0, this, "Failed to store clientSecret");
                return;
            }

            CrestronSecureStorage.Flush();
            Debug.Console(0, this, "Succesfully stored clientSecret");
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
                Debug.Console(0, this, "Failed to store clientPassword");
                return;
            }

            CrestronSecureStorage.Flush();
            Debug.Console(0, this, "Succesfully stored clientPassword");
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


        public UserResults SearchUser(string searchText)
        {
            if (_recorder.Id.Equals(Guid.Empty) || string.IsNullOrEmpty(searchText))
                return null;

            string url = string.Format("{0}/Panopto/api/v1/users/search?searchQuery={1}", _url, searchText);

            HttpsClientRequest request = GetDefaultRequestWithAuthHeaders(url, _token, RequestType.Get);

            Debug.Console(1, this, "Attempting to search user: {0}", searchText);
            using (HttpsClient client = new HttpsClient().WithDefaultSettings())
            {
                try
                {
                    HttpsClientResponse result = client.Dispatch(request);
                    return ProcessUsers(result);
                }
                catch (Exception ex)
                {
                    Debug.Console(1, this, "Error searching user {0}", ex.Message);
                }

                return null;
            }
        }

        public KeyValuePair<string, Guid> GetUserFolder(Guid user)
        {
            string url = string.Format("{0}/Panopto/PublicAPI/4.6/SessionManagement.svc", _url);

            HttpsClientRequest request = new HttpsClientRequest { RequestType = RequestType.Post };
            request.Header.AddHeader(new HttpsHeader("SOAPAction",
                "http://tempuri.org/ISessionManagement/GetPersonalFolderForUser"));
            request.Header.AddHeader(new HttpsHeader("Content-Type", "text/xml"));
            request.Url.Parse(url);

            string content = CreateSoapEnvelope(user);
            if (content != null)
            {
                request.ContentString = CreateSoapEnvelope(user);
                using (HttpsClient client = new HttpsClient())
                {
                    try
                    {
                        HttpsClientResponse result = client.Dispatch(request);
                        return ProcessGetPersonalFolderForUser(result);
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(1, this, "Error getting user folder {0}", ex.Message);
                    }
                }
            }

            return new KeyValuePair<string, Guid>(string.Empty, Guid.Empty);
        }

        public void CancelRecord()
        {
            SetStartRecordingStatus("", Timeout.Infinite);
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

        private KeyValuePair<string, Guid> ProcessGetPersonalFolderForUser(HttpsClientResponse response)
        {
            if (response.Code != 200)
            {
                Debug.Console(1, this, "Error getting user folder... Code:{0}\r{1}", response.Code,
                    response.ContentString);
                return new KeyValuePair<string, Guid>("", Guid.Empty);
            }

            Guid id = Guid.Empty;
            string name = "";

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
                                    id = new Guid(reader.ReadElementContentAsString());
                                    Debug.Console(1, this, "Success getting user folder guid: {0}\r",
                                        id.ToString());
                                }
                                catch (FormatException)
                                {
                                    // The input was not in a valid GUID format.
                                    Debug.Console(1, this, "Invalid guid format: {0}\r",
                                        reader.ReadElementContentAsString());
                                }

                                break;
                            // Check for the start element named "Name" in the expected namespace.
                            case "Name":
                                name = reader.ReadElementContentAsString();
                                Debug.Console(1, this, "Success getting user folder name: {0}\r",
                                    name);
                                break;
                        }
                    }
                }
            }

            return new KeyValuePair<string, Guid>(name, id);
        }

        private UserResults ProcessUsers(HttpsClientResponse response)
        {
            if (response.Code != 200)
            {
                Debug.Console(1, this, "Error processing users... Code:{0}\r{1}", response.Code,
                    response.ContentString);
                return null;
            }

            using (StreamReader stream = new StreamReader(response.ContentStream))
            {
                try
                {
                    JsonTextReader reader = new JsonTextReader(stream);
                    JsonSerializer serializer = new JsonSerializer();

                    UserResults users = serializer.Deserialize<UserResults>(reader);
                    Debug.Console(2, this, "Processing users...\r{0}",
                        JsonConvert.SerializeObject(users, Formatting.Indented));

                    if (users.Results.Count > 20)
                    {
                        users.Results = users.Results.Take(20).ToList();
                    }

                    users.Results.RemoveAll(x => x.Id == Guid.Empty);

                    return users;
                }
                catch (Exception e)
                {
                    Debug.ConsoleWithLog(0, this, "Exception processing users: {0}", e.Message);
                    return null;
                }
            }
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

            Debug.Console(1, this, "Recorder Status:\r{0}",
                JsonConvert.SerializeObject(_recorder, Formatting.Indented));
        }

        private void SetStartRecordingStatus(string value, long resetTime)
        {
            _startRecordingStatusTimer.Reset(resetTime);
            _startRecordingStatus = value;
            StartRecordingStatus.FireUpdate();
        }

        public void ResetStartRecordingStatus()
        {
            _startRecordingStatus = "";
            StartRecordingStatus.FireUpdate();
        }

        public void ProcessStartRecording(HttpsClientResponse response)
        {
            if (response.Code != 200)
            {
                SetStartRecordingStatus(string.Format("Failed to start recording: {0}", response.ContentString), 60000);
                try
                {
                    ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(response.ContentString);
                    SetStartRecordingStatus(
                        string.Format("Failed to start recording: {0}", errorResponse.Error.Message), 60000);
                }
                catch (Exception)
                {
                    SetStartRecordingStatus(string.Format("Failed to start recording: {0}", response.Code), 60000);
                }

                Debug.ConsoleWithLog(0, this,
                    "Error starting recording... Code:{0}\r{1}", response.Code,
                    response.ContentString);
            }
            else
            {
                try
                {
                    using (StreamReader stream = new StreamReader(response.ContentStream))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        JsonTextReader reader = new JsonTextReader(stream);
                        ScheduledRecording currentRecording = serializer.Deserialize<ScheduledRecording>(reader);
                        Debug.Console(2, this, "Starting recording...\r{0}",
                            JsonConvert.SerializeObject(currentRecording, Formatting.Indented));
                        Debug.Console(2, this, "Start time:{0}", currentRecording.StartTime.ToShortTimeString());
                        Debug.Console(2, this, "End time:{0}", currentRecording.EndTime.ToShortTimeString());

                        //Below status will trigger epiphan to sync schedule and start,
                        //if linked via the device key in config
                        SetStartRecordingStatus("Recording requested. Synching with recorder", 120000);
                    }
                }
                catch (Exception e)
                {
                    Debug.ConsoleWithLog(0, this, "Exception after starting recording: {0}", e);
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
            IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.RecorderOnline.JoinNumber]);
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


        public void StartRecording(string name, DateTime? endTime, Guid folderId)
        {
            if (_recorder.Id.Equals(Guid.Empty))
                return;

            SetStartRecordingStatus("Starting recording. Please wait...", 60000);
            const string path = "/Panopto/api/v1/scheduledRecordings?resolveConflicts=false";
            string url = string.Format("{0}{1}", _url, path);
            if (endTime == null || endTime < DateTime.Now || endTime > DateTime.Now.AddHours(3))
            {
                SetStartRecordingStatus("Failed to start recording due to invalid end time", 60000);
                Debug.ConsoleWithLog(0, this, "Failed to start recording due to invalid end time");
                return;
            }

            StartRecordingRequest body = new StartRecordingRequest
            {
                Name = !string.IsNullOrEmpty(name) ? name : _recorderName + " " + DateTime.Now.ToString("g"),
                Description = _recorderName + " " + DateTime.Now.ToString("g"),
                Recorders = new List<Recorder> { new Recorder { RemoteRecorderId = _recorder.Id } },
                StartTime = DateTime.Now,
                EndTime = (DateTime)endTime,
                FolderId = folderId == Guid.Empty ? _recorder.DefaultRecordingFolder.Id : folderId,
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
                    Debug.ConsoleWithLog(0, this, "Error starting recording {0}", ex.Message);
                }
            }
        }
    }
}