using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.CrestronXml;
using Crestron.SimplSharp.CrestronXmlLinq;
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json;
using PepperDash.Core;
using Formatting = Crestron.SimplSharp.CrestronXml.Formatting;

namespace PepperDash.Essentials.Core.Recording
{
    public static class PanoptoController
    {
        private static readonly CTimer _oauthTimer;
        private static string _token;
        private const string clientIdKey = "PanoptoClientId";
        private const string clientSecretKey = "PanoptoClientSecret";
        private const string clientUsernameKey = "PanoptoClientUsername";
        private const string clientPasswordKey = "PanoptoClientPassword";
        private const string Name = "Panopto Controller";

        static PanoptoController()
        {
            CrestronConsole.AddNewConsoleCommand(
                s => { SetClientId(s); }, "PANOPTOCLIENT", "Panopto API client ID",
                ConsoleAccessLevelEnum.AccessAdministrator);

            CrestronConsole.AddNewConsoleCommand(
                s => { SetClientSecret(s); }, "PANOPTOSECRET", "Panopto API client secret",
                ConsoleAccessLevelEnum.AccessAdministrator);

            CrestronConsole.AddNewConsoleCommand(
                s => { SetClientUsername(s); }, "PANOPTOUSERNAME", "Panopto API account username",
                ConsoleAccessLevelEnum.AccessAdministrator);

            CrestronConsole.AddNewConsoleCommand(
                s => { SetClientPassword(s); }, "PANOPTOPASSWORD", "Panopto API account password",
                ConsoleAccessLevelEnum.AccessAdministrator);

            _oauthTimer = new CTimer(o => UpdateToken(), Timeout.Infinite);
        }

        public static void SetClientId(string clientId)
        {
            eCrestronSecureStorageStatus storageResult = CrestronSecureStorage.Store(clientIdKey,
                false,
                Encoding.ASCII.GetBytes(clientId),
                Encoding.ASCII.GetBytes(clientIdKey));

            if (storageResult != eCrestronSecureStorageStatus.Ok)
            {
                Debug.Console(0, Name, "Failed to store clientId");
                return;
            }

            CrestronSecureStorage.Flush();
            Debug.Console(0, Name, "Succesfully stored clientId");
        }

        public static void SetClientSecret(string clientSecret)
        {
            eCrestronSecureStorageStatus storageResult = CrestronSecureStorage.Store(clientSecretKey,
                false,
                Encoding.ASCII.GetBytes(clientSecret),
                Encoding.ASCII.GetBytes(clientSecretKey));

            if (storageResult != eCrestronSecureStorageStatus.Ok)
            {
                Debug.Console(0, Name, "Failed to store clientSecret");
                return;
            }

            CrestronSecureStorage.Flush();
            Debug.Console(0, Name, "Succesfully stored clientSecret");
        }

        public static void SetClientUsername(string clientUsername)
        {
            eCrestronSecureStorageStatus storageResult = CrestronSecureStorage.Store(clientUsernameKey,
                false,
                Encoding.ASCII.GetBytes(clientUsername),
                Encoding.ASCII.GetBytes(clientUsernameKey));

            if (storageResult != eCrestronSecureStorageStatus.Ok)
            {
                Debug.Console(0, Name, "Failed to store clientUsername");
                return;
            }

            CrestronSecureStorage.Flush();
            Debug.Console(0, Name, "Succesfully stored clientUsername");
        }

        public static void SetClientPassword(string clientPassword)
        {
            eCrestronSecureStorageStatus storageResult = CrestronSecureStorage.Store(clientPasswordKey,
                false,
                Encoding.ASCII.GetBytes(clientPassword),
                Encoding.ASCII.GetBytes(clientPasswordKey));

            if (storageResult != eCrestronSecureStorageStatus.Ok)
            {
                Debug.Console(0, Name, "Failed to store clientPassword");
                return;
            }

            CrestronSecureStorage.Flush();
            Debug.Console(0, Name, "Succesfully stored clientPassword");
        }

        public static bool UpdateToken()
        {
            const string path = "/Panopto/oauth2/connect/token";
            string url = _url + path;
            try
            {
                string clientId;
                if (!TryGetValueFromSecureStorage(clientIdKey, out clientId))
                {
                    Debug.Console(1, Name, "Client Id not set");
                    return false;
                }

                string clientSecret;
                if (!TryGetValueFromSecureStorage(clientSecretKey, out clientSecret))
                {
                    Debug.Console(1, Name, "Client Secret not set");
                    return false;
                }

                string clientUsername;
                if (!TryGetValueFromSecureStorage(clientUsernameKey, out clientUsername))
                {
                    Debug.Console(1, Name, "Client Username not set");
                    return false;
                }

                string clientPassword;
                if (!TryGetValueFromSecureStorage(clientPasswordKey, out clientPassword))
                {
                    Debug.Console(1, Name, "Client Password not set");
                    return false;
                }

                Debug.Console(1, Name, "Getting token...");
                PanoptoOauthClient.TokenResponse token =
                    PanoptoOauthClient.GetToken(url, clientUsername, clientPassword, clientId, clientSecret);
                _token = token.AccessToken;

                int expireTime = token.ExpiresIn * 1000 - 500;
                _oauthTimer.Reset(expireTime);
                Debug.Console(1, Name, "Success!  Token expires at: {0}",
                    DateTime.Now.AddMilliseconds(expireTime).ToShortTimeString());
                return true;
            }
            catch (Exception ex)
            {
                _oauthTimer.Reset();
                Debug.Console(0, Name, "Caught an error getting the token: {0}{1}", ex.Message,
                    ex.StackTrace);
                return false;
            }
        }

        public static bool CheckTokenAndUpdate()
        {
            return !string.IsNullOrEmpty(_token) || UpdateToken();
        }

        public static bool TryGetValueFromSecureStorage(string key, out string value)
        {
            value = string.Empty;

            byte[] bytes;
            eCrestronSecureStorageStatus storageResult = CrestronSecureStorage.Retrieve(key,
                false,
                Encoding.ASCII.GetBytes(key),
                out bytes);

            if (storageResult != eCrestronSecureStorageStatus.Ok)
                return false;

            value = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
            return true;
        }

        public static KeyValuePair<string, Guid> GetUserFolder(Guid user)
        {
            if (user.Equals(Guid.Empty))
            {
                return new KeyValuePair<string, Guid>("", Guid.Empty);
            }

            string url = string.Format("{0}/Panopto/PublicAPI/4.6/SessionManagement.svc", _url);

            HttpsClientRequest request = new HttpsClientRequest { RequestType = RequestType.Post };
            request.Header.AddHeader(new HttpsHeader("SOAPAction",
                "http://tempuri.org/ISessionManagement/GetPersonalFolderForUser"));
            request.Header.AddHeader(new HttpsHeader("Content-Type", "text/xml"));
            request.Url.Parse(url);

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
                    Debug.Console(1, Name, "Error getting user folder {0}", ex.Message);
                }
            }

            return new KeyValuePair<string, Guid>("", Guid.Empty);
        }

        private static string CreateSoapEnvelope(Guid userGuid)
        {
            string clientUsername;
            string clientPassword;
            if (!TryGetValueFromSecureStorage(clientUsernameKey, out clientUsername))
            {
                Debug.Console(1, Name, "Client Username not set");
                return null;
            }

            if (!TryGetValueFromSecureStorage(clientPasswordKey, out clientPassword))
            {
                Debug.Console(1, Name, "Client Password not set");
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
                            new XElement(pan + "UserKey", clientUsername)
                        ),
                        new XElement(tem + "userId", userGuid.ToString()),
                        new XElement(tem + "allowCreation", "false")
                    )
                )
            );

            XDocument soapEnvelope = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), envelope);
            return soapEnvelope.ToString();
        }

        private static KeyValuePair<string, Guid> ProcessGetPersonalFolderForUser(HttpsClientResponse response)
        {
            if (response.Code != 200)
            {
                Debug.Console(1, Name, "Error getting user folder... Code:{0}\r{1}", response.Code,
                    response.ContentString);
                return new KeyValuePair<string, Guid>("", Guid.Empty);
            }

            Guid resultGuid = Guid.Empty;
            string resultName = "";

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
                            case "Id":
                                try
                                {
                                    resultGuid = new Guid(reader.ReadElementContentAsString());
                                    Debug.Console(1, Name, "Success getting user folder guid: {0}\r",
                                        resultGuid.ToString());
                                }
                                catch (FormatException)
                                {
                                    // The input was not in a valid GUID format.
                                    Debug.Console(1, Name, "Invalid guid format: {0}\r",
                                        reader.ReadElementContentAsString());
                                }

                                break;
                            case "Name":
                                resultName = reader.ReadElementContentAsString();
                                Debug.Console(1, Name, "Success getting user folder name: {0}\r",
                                    resultName);
                                break;
                        }
                    }
                }
            }

            return new KeyValuePair<string, Guid>(resultName, resultGuid);
        }

        public static UserResults SearchUser(string searchText)
        {
            string url = string.Format("{0}/Panopto/api/v1/users/search?searchQuery={1}", _url, searchText);

            HttpsClientRequest request = GetDefaultRequestWithAuthHeaders(url, _token, RequestType.Get);

            Debug.Console(1, this, "Attempting to search user: {0}", searchText);
            using (HttpsClient client = new HttpsClient().WithDefaultSettings())
            {
                try
                {
                    HttpsClientResponse result = client.Dispatch(request);
                    if (result.Code != 200)
                    {
                        Debug.Console(1, this, "Error processing users... Code:{0}\r{1}", result.Code,
                            result.ContentString);
                        return null;
                    }

                    using (StreamReader stream = new StreamReader(result.ContentStream))
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
                        //Remove pines
                        users.Results.RemoveAll(x => x.Id.ToString() == "a8ea2b4b-b507-4513-945e-ae6d0107459a");

                        return users;
                    }
                }
                catch (Exception ex)
                {
                    Debug.ConsoleWithLog(0, this, "Error searching user {0}", ex.Message);
                }
            }

            return null;
        }
    }
}