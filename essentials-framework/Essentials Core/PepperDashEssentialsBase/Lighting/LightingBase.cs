using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core.Bridges;

namespace PepperDash.Essentials.Core.Lighting
{
    public abstract class LightingBase : EssentialsBridgeableDevice, ILightingScenes
    {
        #region ILightingScenes Members

        public event EventHandler<LightingSceneChangeEventArgs> LightingSceneChange;

        public List<LightingScene> LightingScenes { get; protected set; }

        private LightingScene _currentLightingScene;
        public LightingScene CurrentLightingScene
        {
            get
            {
                return _currentLightingScene;
            }
            protected set
            {        
                if(_currentLightingScene == value)
                    return;
                _currentLightingScene = value;
                OnLightingSceneChange();
            }
        }

        protected bool occupiedFb;
        protected bool vacantFb;

        public BoolFeedback OccupiedFeedback { get; protected set; }
        public BoolFeedback VacantFeedback { get; protected set; }

        #endregion

        protected LightingBase(string key, string name)
            : base(key, name)
        {
            LightingScenes = new List<LightingScene>();
            CurrentLightingScene = new LightingScene();

            OccupiedFeedback = new BoolFeedback(() => occupiedFb);
            VacantFeedback = new BoolFeedback(() => vacantFb);
        }

        public abstract void SelectScene(LightingScene scene);

        public void SimulateSceneSelect(string sceneName)
        {
            Debug.Console(1, this, "Simulating selection of scene '{0}'", sceneName);

            var scene = LightingScenes.FirstOrDefault(s => s.Name.Equals(sceneName));

            if (scene != null)
            {
                CurrentLightingScene = scene;
                OnLightingSceneChange();
            }
        }

        /// <summary>
        /// Sets the IsActive property on each scene and fires the LightingSceneChange event
        /// </summary>
        private void OnLightingSceneChange()
        {
            foreach (var scene in LightingScenes)
            {
                if (scene == CurrentLightingScene)
                    scene.IsActive = true;
					
                else
                    scene.IsActive = false;
            }

            var handler = LightingSceneChange;
            if (handler != null)
            {
                handler(this, new LightingSceneChangeEventArgs(CurrentLightingScene));
            }
        }

	    public void LinkLightingToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
	    {
			var joinMap = new GenericLightingJoinMap(joinStart);
            Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            //Send this device name to SIMPL
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = this.Name;

            // GenericLighitng Actions & FeedBack
            trilist.SetUShortSigAction(joinMap.SelectButton.JoinNumber, u => this.SelectScene(this.LightingScenes[u]));

            //Set occupied/vacant feedback
            OccupiedFeedback.LinkInputSig(trilist.BooleanInput[joinMap.OccupiedFb.JoinNumber]);
            VacantFeedback.LinkInputSig(trilist.BooleanInput[joinMap.VacantFb.JoinNumber]);

            var sceneIndex = 0;
            foreach (var scene in this.LightingScenes)
            {
                var index = sceneIndex;

                trilist.SetSigTrueAction((uint)(joinMap.SelectButtonDirect.JoinNumber + index), () => this.SelectScene(this.LightingScenes[index]));
                scene.IsActiveFeedback.LinkInputSig(trilist.BooleanInput[(uint)(joinMap.SelectButtonDirect.JoinNumber + index)]);
                trilist.StringInput[(uint)(joinMap.SelectButtonDirect.JoinNumber + index)].StringValue = scene.Name;
                sceneIndex++;
            }

            trilist.OnlineStatusChange += (sender, args) =>
            {
                if (!args.DeviceOnLine) return;

                sceneIndex = 0;
                foreach (var scene in this.LightingScenes)
                {
                    var index = sceneIndex;

                    trilist.StringInput[(uint)(joinMap.ButtonTextFb.JoinNumber + index)].StringValue = scene.Name;
                    scene.IsActiveFeedback.FireUpdate();
                    sceneIndex++;
                }
            };
	    }
    }

    public class LightingScene
    {
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string ID { get; set; }
        [JsonProperty("command", NullValueHandling = NullValueHandling.Ignore)]
        public string Command { get; set; }
        [JsonProperty("levels", NullValueHandling = NullValueHandling.Ignore)]
        public SceneLevel[] Levels { get; set; }
        [JsonProperty("portDeviceKey", NullValueHandling = NullValueHandling.Ignore)]
        public string PortDeviceKey { get; set; }
        [JsonProperty("portNumber", NullValueHandling = NullValueHandling.Ignore)]
        public uint PortNumber { get; set; }
        bool _IsActive;
        [JsonProperty("isActive", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsActive 
        {
            get
            {
                return _IsActive;
            }
            set
            {
                _IsActive = value;
                IsActiveFeedback.FireUpdate();
            }
        }

        [JsonIgnore]
        public BoolFeedback IsActiveFeedback { get; set; }

        public LightingScene()
        {
            IsActiveFeedback = new BoolFeedback(new Func<bool>(() => IsActive));
        }
    }

    public class SceneLevel
    {
        [JsonProperty("index", NullValueHandling = NullValueHandling.Ignore)]
        public ushort Index { get; set; }
        [JsonProperty("level", NullValueHandling = NullValueHandling.Ignore)]
        public ushort Level { get; set; }
    }
}