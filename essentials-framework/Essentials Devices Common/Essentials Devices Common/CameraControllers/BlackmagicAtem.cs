using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;

namespace PepperDash.Essentials.Devices.Common.ImageProcessors
{
    public class BlackmagicAtem : EssentialsBridgeableDevice, IBridgeAdvanced, ICommunicationMonitor
    {
        private const int numberInputs = 32;
        private const int numberOutputs = 32;
        private string processMode = "";
        private readonly long _pollTimeMs = 10000; // 10s
        private readonly long _warningTimeoutMs = 30000; // 30s
        private readonly long _errorTimeoutMs = 60000; // 60s

        public IBasicCommunication Communication { get; private set; }
        public StatusMonitorBase CommunicationMonitor { get; private set; }
        CommunicationGather CommGather;

        public Dictionary<uint, IntFeedback> OutputFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> InputNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputRouteNameFeedbacks { get; private set; }

        private Dictionary<uint, string> inputNames;
        private Dictionary<uint, string> outputNames;
        private Dictionary<uint, uint> routeFeedback;

        public BlackmagicAtem(string key, string name, IBasicCommunication comm) :
            base(key, name)
        {
            Communication = comm;
            CommGather = new CommunicationGather(Communication, '\x0A');
            CommGather.IncludeDelimiter = false;
            CommGather.LineReceived += new EventHandler<GenericCommMethodReceiveTextArgs>(DelimitedTextReceived);

            CommunicationMonitor = new GenericCommunicationMonitor(this, comm, _pollTimeMs, _warningTimeoutMs, _errorTimeoutMs, Poll);
            CommunicationMonitor.StatusChange += new EventHandler<MonitorStatusChangeEventArgs>(CommunicationMonitor_StatusChange);

            inputNames = new Dictionary<uint, string>();
            outputNames = new Dictionary<uint, string>();
            routeFeedback = new Dictionary<uint, uint>();

            OutputFeedbacks = new Dictionary<uint, IntFeedback>();
            InputNameFeedbacks = new Dictionary<uint, StringFeedback>();
            OutputNameFeedbacks = new Dictionary<uint, StringFeedback>();
            OutputRouteNameFeedbacks = new Dictionary<uint, StringFeedback>();

            for (uint x = 0; x <= numberInputs; x++)
            {
                var tempX = x;
                inputNames[x] = "";
                InputNameFeedbacks[tempX] = new StringFeedback(() =>
                {
                    return inputNames[tempX];
                });
            }

            for (uint x = 0; x <= numberOutputs; x++)
            {
                var tempX = x;
                outputNames[x] = "";
                routeFeedback[x] = 0;
                OutputFeedbacks[tempX] = new IntFeedback(() =>
                {
                    return (int)routeFeedback[tempX];
                });

                OutputNameFeedbacks[tempX] = new StringFeedback(() =>
                {
                    return outputNames[tempX];
                });

                OutputRouteNameFeedbacks[tempX] = new StringFeedback(() =>
                {
                    if (inputNames[routeFeedback[tempX]] != null)
                    {
                        return inputNames[routeFeedback[tempX]];
                    }
                    return "None";
                });
            }
        }

        public override bool CustomActivate()
        {
            Communication.Connect();
            CommunicationMonitor.Start();
            return true;
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            BlackmagicAtemJoinMap joinMap = new BlackmagicAtemJoinMap(joinStart);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }
            else
            {
                Debug.Console(0, this, "Please update config to use 'eiscapiadvanced' to get all join map features for this device.");
            }
            
            //Events from SIMPL
            for (uint x = 0; x < numberOutputs; x++)
            {
                uint output = x+1;
                trilist.SetUShortSigAction(joinMap.OutputSource.JoinNumber + x, o => ExecuteNumericSwitch(o, output));
            }

            //Feedback to SIMPL
            trilist.StringInput[joinMap.DeviceName.JoinNumber].StringValue = Name;
            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

            for (uint x = 0; x < numberInputs; x++)
            {
                uint input = x + 1;
                InputNameFeedbacks[input].LinkInputSig(trilist.StringInput[joinMap.InputNames.JoinNumber + x]);
            }

            for (uint x = 0; x < numberOutputs; x++)
            {
                uint output = x + 1;
                OutputFeedbacks[output].LinkInputSig(trilist.UShortInput[joinMap.OutputSource.JoinNumber + x]);
                OutputNameFeedbacks[output].LinkInputSig(trilist.StringInput[joinMap.OutputNames.JoinNumber + x]);
                OutputRouteNameFeedbacks[x+1].LinkInputSig(trilist.StringInput[joinMap.OutputCurrentInputNames.JoinNumber + x]);
            }     
        }
        
        public void ExecuteNumericSwitch(uint input, uint output)
        {
            Debug.Console(1, this, "Executing switch input:{0} output:{1}", input, output);
            if (input >= 0 && input <= numberInputs && output > 0 && output <= numberOutputs)
            {
                //Shift output indexing from 1 to 0 for ATEM
                Communication.SendText(string.Format("VIDEO OUTPUT ROUTING:\x0A{0} {1}\x0A\x0A", output - 1, input));
            }
        }

        void Poll()
        {
            Communication.SendText("PING:\x0A\x0A");
        }

        void CommunicationMonitor_StatusChange(object sender, MonitorStatusChangeEventArgs args)
        {
            Debug.Console(1, this, "CommMonitor status change: {0}", args.Status);
        }

        void DelimitedTextReceived(object sender, GenericCommMethodReceiveTextArgs e)
        {
            Debug.Console(2, this, "Processing feedback:{0}", e.Text);

            if (e.Text.Length < 1)
            {
                processMode = "";
            }
            switch (processMode)
            {
                case "":
                    break;
                case "inputLabels":
                    try
                    {
                        int splitIndex = e.Text.IndexOf(" ");
                        string indexText = e.Text.Substring(0, splitIndex);
                        uint index = Convert.ToUInt16(indexText);
                        string label = e.Text.Substring(splitIndex + 1);
                        Debug.Console(2, this, "Got input label feedback index:{0} label:{1}", index, label);
                        inputNames[index] = label;
                        InputNameFeedbacks[index].FireUpdate();
                    }
                    catch
                    {
                        Debug.Console(0, this, "Error processing input label feedback:{0}", e.Text);
                    }
                    break;
                case "outputLabels":
                    try
                    {
                        int splitIndex = e.Text.IndexOf(" ");
                        string indexText = e.Text.Substring(0, splitIndex);
                        uint index = (uint)(Convert.ToUInt16(indexText) + 1);     //Shift output indexing from 0 to 1
                        string label = e.Text.Substring(splitIndex + 1);
                        Debug.Console(2, this, "Got output label feedback index:{0} label:{1}", index, label);
                        outputNames[index] = label;
                        OutputNameFeedbacks[index].FireUpdate();
                    }
                    catch
                    {
                        Debug.Console(0, this, "Error processing output label feedback:{0}", e.Text);
                    }
                    break;
                case "outputRouting":
                    string[] route = e.Text.Split(' ');
                    if (route.Length == 2)
                    {
                        try
                        {
                            uint output = (uint)(Convert.ToUInt16(route[0]) + 1);   //Shift output indexing from 0 to 1
                            uint input = Convert.ToUInt16(route[1]);
                            Debug.Console(2, this, "Got route feedback input:{0} output:{1}", input, output);
                            routeFeedback[output] = input;
                            OutputFeedbacks[output].FireUpdate();
                            OutputNameFeedbacks[output].FireUpdate();
                        }
                        catch
                        {
                            Debug.Console(0, this, "Error processing route feedback:{0}", e.Text);
                        }
                    }
                    break;
            }

            if (e.Text.StartsWith("INPUT LABELS:"))
            {
                processMode = "inputLabels";
            }
            else if (e.Text.StartsWith("OUTPUT LABELS:"))
            {
                processMode = "outputLabels";
            }
            else if (e.Text.StartsWith("VIDEO OUTPUT ROUTING:"))
            {
                processMode = "outputRouting";
            }
            else if (e.Text.Contains(":"))
            {
                processMode = "";
            }
        }
    }

    public class BlackmagicAtemFactory : EssentialsDeviceFactory<BlackmagicAtem>
    {
        public BlackmagicAtemFactory()
        {
            TypeNames = new List<string>() { "blackmagicatem" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory attempting to create new Blackmagic Atem Device");
            var comm = CommFactory.CreateCommForDevice(dc);
            return new BlackmagicAtem(dc.Key, dc.Name, comm);
        }
    }

    public class BlackmagicAtemJoinMap : JoinMapBaseAdvanced
    {
        #region Digital
        [JoinName("IsOnline")]
        public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 11,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Is Online Fb",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });
        #endregion


        #region Analog
        [JoinName("OutputSource")]
        public JoinDataComplete OutputSource = new JoinDataComplete(new JoinData { JoinNumber = 101, JoinSpan = 4 },
            new JoinMetadata { Description = "Switcher Output Source Set / Get", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Analog });
        #endregion


        #region Serial

        [JoinName("DeviceName")]
        public JoinDataComplete DeviceName = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("InputNames")]
        public JoinDataComplete InputNames = new JoinDataComplete(new JoinData { JoinNumber = 101, JoinSpan = 32 },
            new JoinMetadata { Description = "Switcher Input Name", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });

        [JoinName("OutputNames")]
        public JoinDataComplete OutputNames = new JoinDataComplete(new JoinData { JoinNumber = 301, JoinSpan = 32 },
            new JoinMetadata { Description = "Switcher Output Name", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });

        [JoinName("OutputCurrentInputNames")]
        public JoinDataComplete OutputCurrentInputNames = new JoinDataComplete(new JoinData { JoinNumber = 1201, JoinSpan = 32 },
            new JoinMetadata { Description = "Switcher Output Currently Routed Input Name", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
        #endregion

        public BlackmagicAtemJoinMap(uint joinStart)
            : base(joinStart, typeof(BlackmagicAtemJoinMap))
        {
        }
    }
}