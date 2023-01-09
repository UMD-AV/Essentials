﻿using System;
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

        public LightingScene CurrentLightingScene { get; protected set; }
		
		public IntFeedback CurrentLightingSceneFeedback { get; protected set; }

        #endregion

        protected LightingBase(string key, string name)
            : base(key, name)
        {
            LightingScenes = new List<LightingScene>();

            CurrentLightingScene = new LightingScene();
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
        protected void OnLightingSceneChange()
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

            var sceneIndex = 0;
            foreach (var scene in this.LightingScenes)
            {
                var index = sceneIndex;

                trilist.SetSigTrueAction((uint)(joinMap.SelectButtonDirect.JoinNumber + index), () => this.SelectScene(this.LightingScenes[index]));
                scene.IsActiveFeedback.LinkInputSig(trilist.BooleanInput[(uint)(joinMap.SelectButtonDirect.JoinNumber + index)]);
                trilist.StringInput[(uint)(joinMap.SelectButtonDirect.JoinNumber + index)].StringValue = scene.Name;
                trilist.BooleanInput[(uint)(joinMap.ButtonVisibility.JoinNumber + index)].BoolValue = true;

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
                    trilist.BooleanInput[(uint)(joinMap.ButtonVisibility.JoinNumber + index)].BoolValue = true;
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
}