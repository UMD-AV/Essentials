using System.Collections.Generic;
using Newtonsoft.Json;

namespace PepperDash.Essentials.Core.Config
{
    public class RoomConfig
    {
        [JsonProperty("key")] public string Key { get; set; }

        [JsonProperty("roomName")] public string RoomName { get; set; }

        [JsonProperty("advancedModeDefault")] public bool AdvancedModeDefaultOn { get; set; }

        [JsonProperty("advancedModeToggleVisible")]
        public bool AdvancedModeToggleVisible { get; set; }

        [JsonProperty("defaultSystemPreset")] public int DefaultSystemPreset { get; set; }

        [JsonProperty("defaultMicPreset")] public int DefaultMicPreset { get; set; }

        [JsonProperty("helpText")] public string HelpText { get; set; }

        [JsonProperty("serviceNowEnable")] public bool ServiceNowEnable { get; set; }

        [JsonProperty("occShutdownMinutes")] public int OccShutdownMinutes { get; set; }

        [JsonProperty("occShutdownEnable")] public bool OccShutdownEnable { get; set; }

        [JsonProperty("occSensor01Key")] public string OccSensor01Key { get; set; }

        [JsonProperty("occSensor02Key")] public string OccSensor02Key { get; set; }

        [JsonProperty("avBridgeKey")] public string AvBridgeKey { get; set; }

        [JsonProperty("lights01Key")] public string Lights01Key { get; set; }

        [JsonProperty("lights02Key")] public string Lights02Key { get; set; }

        [JsonProperty("shades01Key")] public string Shades01Key { get; set; }

        [JsonProperty("shades02Key")] public string Shades02Key { get; set; }

        [JsonProperty("micDock01Key")] public string MicDock01Key { get; set; }

        [JsonProperty("micDock02Key")] public string MicDock02Key { get; set; }

        [JsonProperty("micRx01Key")] public string MicRx01Key { get; set; }

        [JsonProperty("micRx02Key")] public string MicRx02Key { get; set; }

        [JsonProperty("micRx03Key")] public string MicRx03Key { get; set; }

        [JsonProperty("micRx04Key")] public string MicRx04Key { get; set; }

        [JsonProperty("numberOfMicBatteries")] public int NumberOfMicBatteries { get; set; }

        [JsonProperty("fusionKey")] public string FusionKey { get; set; }

        [JsonProperty("scheduleKey")] public string ScheduleKey { get; set; }

        [JsonProperty("codecKey")] public string CodecKey { get; set; }

        [JsonProperty("recorderKey")] public string RecorderKey { get; set; }

        [JsonProperty("cameraControllerKey")] public string CameraControllerKey { get; set; }

        [JsonProperty("overflowKey")] public string OverflowKey { get; set; }

        [JsonProperty("wallplateCapable")] public bool WallplateCapable { get; set; }

        [JsonProperty("physicsDivisible")] public bool? PhysicsDivisible { get; set; }

        [JsonProperty("mainFaderKey")] public string MainFaderKey { get; set; }

        [JsonProperty("privacyFaderKey")] public string PrivacyFaderKey { get; set; }

        [JsonProperty("overflowInFaderKey")] public string OverflowInFaderKey { get; set; }

        [JsonProperty("overflowOutFaderKey")] public string OverflowOutFaderKey { get; set; }

        [JsonProperty("faders")] public List<Fader> Faders { get; set; }

        [JsonProperty("cameras")] public List<Camera> Cameras { get; set; }

        [JsonProperty("sources")] public List<Source> Sources { get; set; }

        [JsonProperty("dests")] public List<Dest> Dests { get; set; }

        [JsonProperty("actions")] public List<RoutingAction> Actions { get; set; }
    }

    public class Fader
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("key")] public string Key { get; set; }

        [JsonProperty("techVisible")] public bool TechVisible { get; set; }

        [JsonProperty("userVisible")] public bool UserVisible { get; set; }

        [JsonProperty("muteOnly")] public bool? MuteOnly { get; set; }
    }

    public class Camera
    {
        [JsonProperty("key")] public string Key { get; set; }
        [JsonProperty("source")] public int? Source { get; set; }
        [JsonProperty("hide")] public bool? Hide { get; set; }
    }

    public class Source
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("icon")] public string Icon { get; set; }
        [JsonProperty("index")] public ushort Index { get; set; }
        [JsonProperty("easyModeVisible")] public bool? EasyModeVisible { get; set; }
        [JsonProperty("advancedModeVisible")] public bool? AdvancedModeVisible { get; set; }
        [JsonProperty("techVisible")] public bool? techVisible { get; set; }
        [JsonProperty("visibleMode")] public string visibleMode { get; set; }
        [JsonProperty("overflow")] public bool? Overflow { get; set; }
        [JsonProperty("videoSyncKey")] public string VideoSyncKey { get; set; }
        [JsonProperty("disableDestinations")] public string disableDestinations { get; set; }
        [JsonProperty("deviceKey")] public string DeviceKey { get; set; }
        [JsonProperty("routes")] public List<Route> Routes { get; set; }
        [JsonIgnore] public bool? FeedbackState { get; set; }
        [JsonIgnore] public bool HasAudio { get; set; }
        [JsonIgnore] public bool ContentVisible { get; set; }
    }

    public class Dest
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("index")] public ushort Index { get; set; }
        [JsonProperty("routes")] public List<Route> Routes { get; set; }
        [JsonProperty("visible")] public bool? Visible { get; set; }
        [JsonProperty("techVisible")] public bool? techVisible { get; set; }
        [JsonProperty("visibleMode")] public string visibleMode { get; set; }
        [JsonProperty("overflow")] public bool? Overflow { get; set; }
        [JsonProperty("deviceKey")] public string DeviceKey { get; set; }

        [JsonIgnore] public string FeedbackName { get; set; }
        [JsonIgnore] public ushort? FeedbackIndex { get; set; }
        [JsonIgnore] public string SourceDeviceKey { get; set; }
        [JsonIgnore] public ushort? AudioMode { get; set; }

        //List of destination indexes that follow this audio feedback
        [JsonIgnore] public List<ushort> AudioFollowers = new List<ushort>();
    }

    public class RoutingAction
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("index")] public ushort Index { get; set; }
        [JsonProperty("routes")] public List<Route> Routes { get; set; }
    }

    public class Route
    {
        [JsonProperty("comment")] public string Comment { get; set; }
        [JsonProperty("key")] public string RouteKey { get; set; }
        [JsonProperty("input")] public ushort? Input { get; set; }
        [JsonProperty("output")] public ushort? Output { get; set; }
        [JsonProperty("useForFeedback")] public bool? UseForFeedback { get; set; }
        [JsonProperty("delaySeconds")] public ushort? DelaySeconds { get; set; }
        [JsonProperty("disableInOverflow")] public bool? DisableInOverflow { get; set; }
        [JsonProperty("enableInOverflow")] public bool? EnableInOverflow { get; set; }

        public Route Copy()
        {
            return new Route()
            {
                RouteKey = RouteKey,
                Input = Input,
                Output = Output,
                UseForFeedback = UseForFeedback,
                DelaySeconds = DelaySeconds,
                DisableInOverflow = DisableInOverflow,
                EnableInOverflow = EnableInOverflow
            };
        }
    }
}