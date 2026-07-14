using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using UmdEssentials.Core;
using UmdEssentials.Core.Bridges;
using UmdEssentials.Core.Config;

namespace UmdEssentials.Devices.Common.DSP.QscDsp
{
    /// <summary>
    /// DSP Device 
    /// </summary>
    /// <remarks>
    /// 
    /// - Example subscription feedback responses:
    /// ! "publishToken":"name" "value":-77.0
    /// ! "myLevelName" -77
    /// </remarks>
    public class QscDsp : EssentialsBridgeableDevice, IOnline, ICommunicationMonitor
    {
        /// <summary>
        /// Communication object
        /// </summary>
        private IBasicCommunication Communication { get; set; }

        /// <summary>
        /// Gather
        /// </summary>
        private CommunicationGather PortGather { get; set; }

        /// <summary>
        /// Communication monitor object
        /// </summary>
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        public Dictionary<string, QscDspLevelControl> LevelControlPoints { get; private set; }
        public List<QscDspMonitoringPoint> MonitoringControlPoints { get; private set; }
        public readonly List<QscDspPresets> PresetList = new List<QscDspPresets>();

        private readonly DeviceConfig _dc;

        private uint _heartbeatTracker;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="key">String</param>
        /// <param name="name">String</param>
        /// <param name="comm">IBasicCommunication</param>
        /// <param name="dc">DeviceConfig</param>
        public QscDsp(string key, string name, IBasicCommunication comm, DeviceConfig dc)
            : base(key, name)
        {
            _dc = dc;

            Communication = comm;
            ISocketStatus socket = comm as ISocketStatus;
            if (socket != null)
            {
                // This instance uses IP control
                socket.ConnectionChange += socket_ConnectionChange;
            }
            else
            {
                // This instance uses RS-232 control
            }

            PortGather = new CommunicationGather(Communication, "\n");
            PortGather.LineReceived += Port_LineReceived;

            // Custom monitoring, will check the heartbeat tracker count every 20s and reset. Heartbeat should be coming in every 20s if subscriptions are valid
            CommunicationMonitor =
                new GenericCommunicationMonitor(this, Communication, 20000, 120000, 300000, CheckSubscriptions);

            LevelControlPoints = new Dictionary<string, QscDspLevelControl>();
            MonitoringControlPoints = new List<QscDspMonitoringPoint>();
            CreateDspObjects();
        }

        /// <summary>
        /// CustomActivate Override
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            Communication.Connect();
            CommunicationMonitor.StatusChange +=
                (o, a) => Debug.Console(2, this, "Communication monitor state: {0}", CommunicationMonitor.Status);

            CrestronConsole.AddNewConsoleCommand(SendLine, "send" + Key, "", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s => Communication.Connect(), "con" + Key, "",
                ConsoleAccessLevelEnum.AccessOperator);
            return true;
        }

        private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            if (e.Client.IsConnected)
            {
                SubscribeToAttributes();
            }
            else
            {
                // Cleanup items from this session
            }
        }

        private string FormatTag(string prefix, string tag)
        {
            if (prefix == null)
                prefix = "";
            if (tag == null)
                return null;
            else
                return string.Format("{0}{1}", prefix, tag);
        }

        private void CreateDspObjects()
        {
            QscDspPropertiesConfig props =
                JsonConvert.DeserializeObject<QscDspPropertiesConfig>(_dc.Properties.ToString());

            LevelControlPoints.Clear();
            PresetList.Clear();
            MonitoringControlPoints.Clear();

            // Check for prefix
            string prefix = "";
            if (props.Prefix != null) prefix = props.Prefix;

            if (props.LevelControlBlocks != null)
                foreach (KeyValuePair<string, QscDspLevelControlBlockConfig> block in props.LevelControlBlocks)
                {
                    string key = string.Format("{0}{1}", prefix, block.Key);
                    QscDspLevelControlBlockConfig value = block.Value;
                    value.LevelInstanceTag = FormatTag(prefix, value.LevelInstanceTag);
                    value.MuteInstanceTag = FormatTag(prefix, value.MuteInstanceTag);

                    LevelControlPoints.Add(key, new QscDspLevelControl(key, value, this));
                    Debug.Console(2, this, "Added LevelControlPoint {0} LevelTag: {1} MuteTag: {2}", key,
                        value.LevelInstanceTag, value.MuteInstanceTag);
                }

            if (props.Presets != null)
                foreach (QscDspPresets value in props.Presets.Select(preset => preset.Value))
                {
                    value.Preset = string.Format("{0}{1}", prefix, value.Preset);
                    AddPreset(value);
                    Debug.Console(2, this, "Added Preset {0} {1}", value.Label, value.Preset);
                }

            if (props.MonitoringPoints != null)
                foreach (QscDspMonitoringPointConfig monitorConfig in props.MonitoringPoints)
                {
                    MonitoringControlPoints.Add(new QscDspMonitoringPoint(monitorConfig.InstanceTag, monitorConfig.Name,
                        this));
                    Debug.Console(0, this, "Added Monitoring Control Point {0} - {1}", monitorConfig.Name,
                        monitorConfig.InstanceTag);
                }

            SubscribeToAttributes();
        }

        /// <summary>
        /// Checks the subscription health, should be called by comm monitor only. If no heartbeat has been detected recently, will resubscribe and log error.
        /// </summary>
        private void CheckSubscriptions()
        {
            _heartbeatTracker++;
            SendLine("cgp 2");

            CrestronInvoke.BeginInvoke(o =>
            {
                CrestronEnvironment.Sleep(1000);
                if (_heartbeatTracker > 0)
                {
                    Debug.Console(1, this, "Heartbeat missed, count {0}", _heartbeatTracker);
                    if (_heartbeatTracker % 5 != 0) return;
                    Debug.Console(1, this, "Heartbeat missed 5 times, subscriptions lost? Resubscribing now");
                    if (_heartbeatTracker == 5)
                        Debug.LogError(Debug.ErrorLogLevel.Warning,
                            "Heartbeat missed 5 times - subscriptions lost? Attempting resubscribe.");
                    SubscribeToAttributes();
                }
                else
                {
                    Debug.Console(1, this, "Heartbeat okay");
                }
            });
        }

        /// <summary>
        /// Initiates the subscription process to the DSP
        /// </summary>
        private void SubscribeToAttributes()
        {
            // Change Group destroy
            SendLine("cgd 1");
            SendLine("cgd 2");

            // Change Group create
            SendLine("cgc 1");
            SendLine("cgc 2");

            // Change group subscribe to feedback with no ack (updates every 1000 ms)
            SendLine("cgsna 1 1000");

            foreach (KeyValuePair<string, QscDspLevelControl> level in LevelControlPoints) level.Value.Subscribe();

            foreach (QscDspMonitoringPoint monitoringPoint in MonitoringControlPoints) monitoringPoint.Subscribe();

            if (CommunicationMonitor != null) CommunicationMonitor.Start();
        }

        /// <summary>
        /// Handles a response message from the DSP
        /// </summary>
        /// <param name="dev"></param>
        /// <param name="args"></param>
        private void Port_LineReceived(object dev, GenericCommMethodReceiveTextArgs args)
        {
            Debug.Console(2, this, "RX: '{0}'", args.Text);
            try
            {
                if (args.Text.EndsWith("cgpa\r"))
                {
                    Debug.Console(1, this, "Found poll response");
                    _heartbeatTracker = 0;
                }

                if (args.Text.IndexOf("sr ", StringComparison.Ordinal) > -1)
                {
                }
                else if (args.Text.IndexOf("cv", StringComparison.Ordinal) > -1)
                {
                    string[] changeMessage =
                        Regex.Split(args.Text,
                            " (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"); //Splits by space unless enclosed in double quotes using look ahead method: https://stackoverflow.com/questions/18893390/splitting-on-comma-outside-quotes

                    string changedInstance = changeMessage[1].Replace("\"", "").Trim();
                    Debug.Console(2, this, "cv parse Instance: {0}", changedInstance);
                    foreach (KeyValuePair<string, QscDspLevelControl> controlPoint in LevelControlPoints)
                    {
                        if (changedInstance == controlPoint.Value.LevelInstanceTag)
                        {
                            controlPoint.Value.ParseSubscriptionMessage(changedInstance, changeMessage[4],
                                changeMessage[3]);
                            return;
                        }

                        if (changedInstance == controlPoint.Value.MuteInstanceTag)
                        {
                            controlPoint.Value.ParseSubscriptionMessage(changedInstance,
                                changeMessage[2].Replace("\"", ""), null);
                            return;
                        }
                    }

                    foreach (QscDspMonitoringPoint monitoringPoint in MonitoringControlPoints)
                    {
                        Debug.Console(2, this, "DSP Monitoring Point Status Compare: {0} == {1}", changedInstance,
                            monitoringPoint.InstanceTag);
                        if (changedInstance == monitoringPoint.InstanceTag)
                        {
                            monitoringPoint.ParseSubscriptionMessage(changedInstance,
                                changeMessage[2].Replace("\"", "").Trim());
                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (Debug.Level == 2)
                    Debug.Console(2, this, "Error parsing response: '{0}'\n{1}", args.Text, e);
            }
        }

        /// <summary>
        /// Sends a command to the DSP (with delimiter appended)
        /// </summary>
        /// <param name="s">Command to send</param>
        public void SendLine(string s)
        {
            Debug.Console(2, this, "TX: '{0}'", s);
            Communication.SendText(s + "\n");
        }

        /// <summary>
        /// Runs the preset with the number provided
        /// </summary>
        /// <param name="n">ushort</param>
        public void RunPresetNumber(ushort n)
        {
            RunPreset(PresetList[n].Preset);
        }

        /// <summary>
        /// Adds a preset
        /// </summary>
        /// <param name="s">QscDspPresets</param>
        private void AddPreset(QscDspPresets s)
        {
            PresetList.Add(s);
        }

        /// <summary>
        /// Sends a command to execute a preset
        /// </summary>
        /// <param name="name">Preset Name</param>
        public void RunPreset(string name)
        {
            SendLine(string.Format("ssl {0}", name));
            SendLine("cgp 1");
        }

        #region IBridge Members

        /// <summary>
        /// Link to API
        /// </summary>
        /// <param name="trilist">BasicTriList</param>
        /// <param name="joinStart">uint</param>
        /// <param name="joinMapKey">string</param>
        /// <param name="bridge"></param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            this.LinkToApiExt(trilist, joinStart, joinMapKey, bridge);
        }

        #endregion

        public BoolFeedback IsOnline
        {
            get { return CommunicationMonitor.IsOnlineFeedback; }
        }
    }
}