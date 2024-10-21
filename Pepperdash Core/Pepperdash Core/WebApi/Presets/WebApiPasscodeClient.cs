﻿using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core.JsonToSimpl;

// For Basic SIMPL# Classes


namespace PepperDash.Core.WebApi.Presets
{
    public class WebApiPasscodeClient : IKeyed
    {
        public event EventHandler<UserReceivedEventArgs> UserReceived;

        public event EventHandler<PresetReceivedEventArgs> PresetReceived;

        public string Key { get; private set; }

        //string JsonMasterKey;

        /// <summary>
        /// An embedded JsonToSimpl master object.
        /// </summary>
        private JsonToSimplGenericMaster J2SMaster;

        private string UrlBase;

        private string DefaultPresetJsonFilePath;

        private User CurrentUser;

        private Preset CurrentPreset;


        /// <summary>
        /// SIMPL+ can only execute the default constructor. If you have variables that require initialization, please
        /// use an Initialize method
        /// </summary>
        public WebApiPasscodeClient()
        {
        }

        public void Initialize(string key, string jsonMasterKey, string urlBase, string defaultPresetJsonFilePath)
        {
            Key = key;
            //JsonMasterKey = jsonMasterKey;
            UrlBase = urlBase;
            DefaultPresetJsonFilePath = defaultPresetJsonFilePath;

            J2SMaster = new JsonToSimplGenericMaster();
            J2SMaster.SaveCallback = this.SaveCallback;
            J2SMaster.Initialize(jsonMasterKey);
        }

        public void GetUserForPasscode(string passcode)
        {
            // Bullshit duplicate code here... These two cases should be the same 
            // except for https/http and the certificate ignores 
            if (!UrlBase.StartsWith("https"))
                return;
            HttpsClientRequest req = new HttpsClientRequest();
            req.Url = new UrlParser(UrlBase + "/api/users/dopin");
            req.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
            req.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));
            req.Header.AddHeader(new HttpsHeader("Accept", "application/json"));
            JObject jo = new JObject();
            jo.Add("pin", passcode);
            req.ContentString = jo.ToString();

            HttpsClient client = new HttpsClient();
            client.HostVerification = false;
            client.PeerVerification = false;
            HttpsClientResponse resp = client.Dispatch(req);
            EventHandler<UserReceivedEventArgs> handler = UserReceived;
            if (resp.Code == 200)
            {
                //CrestronConsole.PrintLine("Received: {0}", resp.ContentString);
                User user = JsonConvert.DeserializeObject<User>(resp.ContentString);
                CurrentUser = user;
                if (handler != null)
                    UserReceived(this, new UserReceivedEventArgs(user, true));
            }
            else if (handler != null)
                UserReceived(this, new UserReceivedEventArgs(null, false));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roomTypeId"></param>
        /// <param name="presetNumber"></param>
        public void GetPresetForThisUser(int roomTypeId, int presetNumber)
        {
            if (CurrentUser == null)
            {
                CrestronConsole.PrintLine("GetPresetForThisUser no user loaded");
                return;
            }

            UserAndRoomMessage msg = new UserAndRoomMessage
            {
                UserId = CurrentUser.Id,
                RoomTypeId = roomTypeId,
                PresetNumber = presetNumber
            };

            EventHandler<PresetReceivedEventArgs> handler = PresetReceived;
            try
            {
                if (!UrlBase.StartsWith("https"))
                    return;
                HttpsClientRequest req = new HttpsClientRequest();
                req.Url = new UrlParser(UrlBase + "/api/presets/userandroom");
                req.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                req.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));
                req.Header.AddHeader(new HttpsHeader("Accept", "application/json"));
                req.ContentString = JsonConvert.SerializeObject(msg);

                HttpsClient client = new HttpsClient();
                client.HostVerification = false;
                client.PeerVerification = false;

                // ask for the preset
                HttpsClientResponse resp = client.Dispatch(req);
                if (resp.Code == 200) // got it
                {
                    //Debug.Console(1, this, "Received: {0}", resp.ContentString);
                    Preset preset = JsonConvert.DeserializeObject<Preset>(resp.ContentString);
                    CurrentPreset = preset;

                    //if there's no preset data, load the template
                    if (preset.Data == null || preset.Data.Trim() == string.Empty ||
                        JObject.Parse(preset.Data).Count == 0)
                    {
                        //Debug.Console(1, this, "Loaded preset has no data. Loading default template.");
                        LoadDefaultPresetData();
                        return;
                    }

                    J2SMaster.LoadWithJson(preset.Data);
                    if (handler != null)
                        PresetReceived(this, new PresetReceivedEventArgs(preset, true));
                }
                else // no existing preset
                {
                    CurrentPreset = new Preset();
                    LoadDefaultPresetData();
                    if (handler != null)
                        PresetReceived(this, new PresetReceivedEventArgs(null, false));
                }
            }
            catch (HttpException e)
            {
                HttpClientResponse resp = e.Response;
                Debug.Console(1, this, "No preset received (code {0}). Loading default template", resp.Code);
                LoadDefaultPresetData();
                if (handler != null)
                    PresetReceived(this, new PresetReceivedEventArgs(null, false));
            }
        }

        private void LoadDefaultPresetData()
        {
            CurrentPreset = null;
            if (!File.Exists(DefaultPresetJsonFilePath))
            {
                Debug.Console(0, this, "Cannot load default preset file. Saving will not work");
                return;
            }

            using (StreamReader sr = new StreamReader(DefaultPresetJsonFilePath))
            {
                try
                {
                    string data = sr.ReadToEnd();
                    J2SMaster.SetJsonWithoutEvaluating(data);
                    CurrentPreset = new Preset() { Data = data, UserId = CurrentUser.Id };
                }
                catch (Exception e)
                {
                    Debug.Console(0, this, "Error reading default preset JSON: \r{0}", e);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roomTypeId"></param>
        /// <param name="presetNumber"></param>
        public void SavePresetForThisUser(int roomTypeId, int presetNumber)
        {
            if (CurrentPreset == null)
                LoadDefaultPresetData();
            //return;

            //// A new preset needs to have its numbers set
            //if (CurrentPreset.IsNewPreset)
            //{
            CurrentPreset.UserId = CurrentUser.Id;
            CurrentPreset.RoomTypeId = roomTypeId;
            CurrentPreset.PresetNumber = presetNumber;
            //}
            J2SMaster.Save(); // Will trigger callback when ready
        }

        /// <summary>
        /// After save operation on JSON master happens, send it to server
        /// </summary>
        /// <param name="json"></param>
        private void SaveCallback(string json)
        {
            CurrentPreset.Data = json;

            if (!UrlBase.StartsWith("https"))
                return;
            HttpsClientRequest req = new HttpsClientRequest();
            req.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
            req.Url = new UrlParser(string.Format("{0}/api/presets/addorchange", UrlBase));
            req.Header.AddHeader(new HttpsHeader("Content-Type", "application/json"));
            req.Header.AddHeader(new HttpsHeader("Accept", "application/json"));
            req.ContentString = JsonConvert.SerializeObject(CurrentPreset);

            HttpsClient client = new HttpsClient();
            client.HostVerification = false;
            client.PeerVerification = false;
            try
            {
                HttpsClientResponse resp = client.Dispatch(req);

                // 201=created
                // 204=empty content
                if (resp.Code == 201)
                    CrestronConsole.PrintLine("Preset added");
                else if (resp.Code == 204)
                    CrestronConsole.PrintLine("Preset updated");
                else if (resp.Code == 209)
                    CrestronConsole.PrintLine("Preset already exists. Cannot save as new.");
                else
                    CrestronConsole.PrintLine("Preset save failed: {0}\r", resp.Code, resp.ContentString);
            }
            catch (HttpException e)
            {
                CrestronConsole.PrintLine("Preset save exception {0}", e.Response.Code);
            }
        }
    }
}