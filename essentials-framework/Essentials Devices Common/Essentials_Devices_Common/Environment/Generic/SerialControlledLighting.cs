using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Lighting;
using LightingBase = PepperDash.Essentials.Core.Lighting.LightingBase;
using Newtonsoft.Json;

namespace PepperDash.Essentials.Devices.Common.Environment.Generic
{
    public class SerialControlledLighting : LightingBase, ICommunicationMonitor
    {
        public IBasicCommunication Communication { get; private set; }
        public StatusMonitorBase CommunicationMonitor { get; private set; }
        private CrestronQueue<string> _commandQueue;
        private CMutex _commandMutex;
        private CTimer _commandTimer;
        private bool _queueWaiting = false;
        private bool _commandReady = true;
        string pollString;

        public SerialControlledLighting(string key, string name, IBasicCommunication comm,
            SerialControlledLightingPropertiesConfig props)
            : base(key, name)
        {
            Communication = comm;
            Communication.TextReceived += Communication_TextReceived;
            if (props.Scenes != null)
            {
                LightingScenes = props.Scenes;
            }

            if (props.PollString != null)
            {
                pollString = props.PollString;
                CommunicationMonitor =
                    new GenericCommunicationMonitor(this, Communication, 60000, 120000, 300000, Poll);
                CommunicationMonitor.StatusChange +=
                    new EventHandler<MonitorStatusChangeEventArgs>(CommunicationMonitor_StatusChange);
            }

            _commandQueue = new CrestronQueue<string>(20);
            _commandMutex = new CMutex();
            _commandTimer = new CTimer(commandTimeout, Timeout.Infinite);
        }

        public override bool CustomActivate()
        {
            Communication.Connect();
            CommunicationMonitor.Start();
            return true;
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            GenericLightingJoinMap joinMap = new GenericLightingJoinMap(joinStart);
            LinkLightingToApi(trilist, joinStart, joinMapKey, bridge);

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
        }

        public void CommunicationMonitor_StatusChange(object o, MonitorStatusChangeEventArgs e)
        {
            if (e.Status == MonitorStatus.IsOk)
            {
            }
            else
            {
                _commandQueue.Clear();
            }
        }

        private void commandTimeout(object o)
        {
            Debug.Console(1, this, "Command timed out");
            _commandReady = true;
            ProcessQueue();
        }

        protected void readyForNextCommand()
        {
            _commandTimer.Stop(); //No need for timeout on last command
            _commandReady = true;
            ProcessQueue();
        }

        private void ProcessQueue()
        {
            CrestronInvoke.BeginInvoke((o) =>
            {
                //Thread safe queue processing below
                if (_queueWaiting == false)
                {
                    _queueWaiting = true;
                    if (_commandMutex.WaitForMutex())
                    {
                        Debug.Console(2, this, "Got command mutex");
                        _queueWaiting = false;
                        try
                        {
                            while (!_commandQueue.IsEmpty)
                            {
                                int count = 0;
                                while (!_commandReady && (count < 50))
                                {
                                    Thread.Sleep(200);
                                    count++;
                                }

                                string cmd = _commandQueue.TryToDequeue();
                                _commandReady = false;
                                _commandTimer.Reset(1000); //Wait maximum 1s for response

                                CrestronInvoke.BeginInvoke((obj) => { Communication.SendText(cmd); });
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.Console(0, this, "Exception processing command queue: {0}", ex);
                        }
                        finally
                        {
                            _commandMutex.ReleaseMutex();
                            Debug.Console(2, this, "Released command mutex");
                        }
                    }
                    else
                    {
                        _queueWaiting = false;
                    }
                }
            });
        }

        public void QueueCommand(string cmd)
        {
            if (!_commandQueue.IsFull)
            {
                Debug.Console(2, this, "Queueing command: {0}", cmd);
                _commandQueue.TryToEnqueue(cmd);
                ProcessQueue();
            }
            else
            {
                Debug.Console(0, this, "Command queue is full! Dropping command.");
                readyForNextCommand();
            }
        }

        /// <summary>
        /// Recalls the specified scene
        /// </summary>
        /// <param name="scene"></param>
        /// 
        public override void SelectScene(LightingScene scene)
        {
            if (LightingScenes != null && LightingScenes.Exists(o => o.ID == scene.ID))
            {
                SelectScene((ushort)LightingScenes.FindIndex(o => o.ID == scene.ID));
            }
        }

        /// <summary>
        /// Communication bytes recieved
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Event args</param>
        private void Communication_TextReceived(object sender, GenericCommMethodReceiveTextArgs e)
        {
            if (e.Text.Contains("\r\n"))
            {
                Thread.Sleep(500);
                readyForNextCommand();
            }
        }

        /// <summary>
        /// Recalls the specified scene
        /// </summary>
        /// <param name="scene"></param>
        /// 
        public void SelectScene(ushort scene)
        {
            if (LightingScenes != null && LightingScenes[scene] != null && LightingScenes[scene].ID != null)
            {
                if (scene >= 0 && scene <= 10)
                {
                    Debug.Console(1, this, "Selecting Scene: '{0}'", LightingScenes[scene].ID);
                    if (LightingScenes[scene].Command != null)
                    {
                        QueueCommand(LightingScenes[scene].Command);
                    }

                    if (LightingScenes[scene].Command2 != null)
                    {
                        QueueCommand(LightingScenes[scene].Command2);
                    }
                }
            }
        }

        public void Poll()
        {
            QueueCommand(pollString);
        }
    }

    public class SerialControlledLightingPropertiesConfig
    {
        public CommunicationMonitorConfig CommunicationMonitorProperties { get; set; }
        public ControlPropertiesConfig Control { get; set; }

        [JsonProperty("pollString", NullValueHandling = NullValueHandling.Ignore)]
        public string PollString { get; set; }

        public List<LightingScene> Scenes { get; set; }
    }

    public class SerialControlledLightingFactory : EssentialsDeviceFactory<SerialControlledLighting>
    {
        public SerialControlledLightingFactory()
        {
            TypeNames = new List<string>() { "seriallighting" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Serial Controlled Lighting Device");
            IBasicCommunication comm = CommFactory.CreateCommForDevice(dc);

            SerialControlledLightingPropertiesConfig props = Newtonsoft.Json.JsonConvert
                .DeserializeObject<Environment.Generic.SerialControlledLightingPropertiesConfig>(
                    dc.Properties.ToString());

            return new SerialControlledLighting(dc.Key, dc.Name, comm, props);
        }
    }
}