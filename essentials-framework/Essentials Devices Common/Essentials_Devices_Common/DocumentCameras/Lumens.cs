using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using Newtonsoft.Json;
using PepperDash.Essentials.Core.Config;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;

namespace PepperDash.Essentials.Devices.Common.Lumens
{
    public class LumensDocumentCameraDevice : EssentialsBridgeableDevice
    {
        public IBasicCommunication Communication { get; private set; }
        public GenericCommunicationMonitor CommunicationMonitor { get; private set; }

        public LumensDocumentCameraDevice(string key, string name, IBasicCommunication comm, DeviceConfig dc)
            : base(key, name)
        {
            PowerIsOnFeedback = new BoolFeedback(() => _PowerIsOn);
            LampIsOnFeedback = new BoolFeedback(() => _LampIsOn);
            Communication = comm;
            Communication.BytesReceived += Communication_BytesReceived;

            // Custom monitoring, will check the heartbeat tracker count every 20s and reset. Heartbeat sbould be coming in every 30s if subscriptions are valid
            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, Poll);
            DeviceManager.AddDevice(CommunicationMonitor);
        }

        public override bool CustomActivate()
        {
            Communication.Connect();
            CommunicationMonitor.Start();
            return base.CustomActivate();
        }

        /// <summary>
        /// Communication bytes recieved
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Event args</param>
        private void Communication_BytesReceived(object sender, GenericCommMethodReceiveBytesArgs e)
        {
            byte[] response = new byte[4];
            uint? stxPos = null;
            uint? etxPos = null;
            for (uint i = 0; i < e.Bytes.Length; i++)
            {
                if (stxPos == null)
                {
                    if (e.Bytes[i] == 0xA0)
                        stxPos = i;
                }
                else
                {
                    if (e.Bytes[i] == 0xAF)
                    {
                        etxPos = i;
                        break;
                    }
                    else
                    {
                        uint responsePos = i - (uint)stxPos - 1;
                        if (responsePos > 3)
                            break;
                        response[responsePos] = e.Bytes[i];
                    }
                }
            }

            if (stxPos != null && etxPos != null && (etxPos - stxPos == 5))
            {
                processResponse(response);
            }
            else
            {
                Debug.Console(0, this, "Unable to find stx and etx in response");
            }
        }

        private void processResponse(byte[] response)
        {
            if (response[3] != 0x00)
            {
                //NAK response
                if (response[3] == 0x01)
                {
                    Debug.Console(0, this, "Command not executed");
                }
                //Unknown command response
                else if (response[3] == 0x02)
                {
                    Debug.Console(0, this, "Unknown command");
                }
                else
                {
                    Debug.Console(0, this, "Unknown error in command");
                }

                return;
            }

            //Status response
            if (response[0] == 0xB7)
            {
                if (response[2] == 0x00)
                {
                    PowerIsOn = false;
                }
                else if (response[2] == 0x01)
                {
                    PowerIsOn = true;
                }
            }

            //Power response
            else if (response[0] == 0xB1)
            {
                if (response[1] == 0x00)
                {
                    PowerIsOn = false;
                }
                else if (response[1] == 0x01)
                {
                    PowerIsOn = true;
                }
            }

            //Lamp response
            else if (response[0] == 0xC1)
            {
                if (response[1] == 0x00)
                {
                    LampIsOn = false;
                }
                else if (response[1] == 0x01 || response[1] == 0x02 || response[1] == 0x03)
                {
                    LampIsOn = true;
                }
            }
        }

        #region IBridge Members

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            LumensDocumentCameraJoinMap joinMap = new LumensDocumentCameraJoinMap(joinStart);
            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

            //Power
            trilist.SetSigTrueAction(joinMap.PowerOn.JoinNumber, () => PowerOn());
            trilist.SetSigTrueAction(joinMap.PowerOff.JoinNumber, () => PowerOff());
            PowerIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerOn.JoinNumber]);
            PowerIsOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PowerOff.JoinNumber]);

            //Lamp
            trilist.SetSigTrueAction(joinMap.LampOn.JoinNumber, () => LampOn());
            trilist.SetSigTrueAction(joinMap.LampOff.JoinNumber, () => LampOff());
            LampIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.LampOn.JoinNumber]);
            LampIsOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.LampOff.JoinNumber]);
        }

        #endregion


        #region IPower Members

        public void PowerOn()
        {
            byte[] cmd = new byte[]
            {
                0xA0, 0xB1, 0x01, 0x00, 0x00, 0xAF
            };
            Communication.SendBytes(cmd);
        }

        public void PowerOff()
        {
            byte[] cmd = new byte[]
            {
                0xA0, 0xB1, 0x00, 0x00, 0x00, 0xAF
            };
            Communication.SendBytes(cmd);
        }

        public void PowerToggle()
        {
            if (_PowerIsOn)
            {
                PowerOn();
            }
            else
            {
                PowerOff();
            }
        }

        public BoolFeedback PowerIsOnFeedback { get; set; }
        bool _PowerIsOn;

        public bool PowerIsOn
        {
            get { return _PowerIsOn; }
            set
            {
                _PowerIsOn = value;
                PowerIsOnFeedback.FireUpdate();
            }
        }

        #endregion

        public void LampOn()
        {
            byte[] cmd = new byte[]
            {
                0xA0, 0xC1, 0x01, 0x00, 0x00, 0xAF
            };
            Communication.SendBytes(cmd);
        }

        public void LampOff()
        {
            byte[] cmd = new byte[]
            {
                0xA0, 0xC1, 0x00, 0x00, 0x00, 0xAF
            };
            Communication.SendBytes(cmd);
        }

        public void LampToggle()
        {
            if (_LampIsOn)
            {
                LampOn();
            }
            else
            {
                LampOff();
            }
        }

        public BoolFeedback LampIsOnFeedback { get; set; }
        bool _LampIsOn;

        public bool LampIsOn
        {
            get { return _LampIsOn; }
            set
            {
                _LampIsOn = value;
                LampIsOnFeedback.FireUpdate();
            }
        }

        #region Poll

        public void Poll()
        {
            byte[] cmd = new byte[]
            {
                0xA0, 0xB7, 0x00, 0x00, 0x00, 0xAF
            };
            Communication.SendBytes(cmd);

            CrestronEnvironment.Sleep(1000);
            cmd = new byte[]
            {
                0xA0, 0x50, 0x00, 0x00, 0x00, 0xAF
            };
            Communication.SendBytes(cmd);
        }

        #endregion
    }

    public class LumensDocumentCameraJoinMap : JoinMapBaseAdvanced
    {
        [JoinName("IsOnline")] public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 49,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "IsOnline",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PowerOn")] public JoinDataComplete PowerOn = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Power On", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Digital
            });

        [JoinName("PowerOff")] public JoinDataComplete PowerOff = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Power Off", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital
            });

        [JoinName("LampOn")] public JoinDataComplete LampOn = new JoinDataComplete(
            new JoinData { JoinNumber = 5, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Lamp On", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Digital
            });

        [JoinName("LampOff")] public JoinDataComplete LampOff = new JoinDataComplete(
            new JoinData { JoinNumber = 6, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Lamp Off", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital
            });


        public LumensDocumentCameraJoinMap(uint joinStart)
            : base(joinStart, typeof(LumensDocumentCameraJoinMap))
        {
        }
    }

    public class LumensDocumentCameraFactory : EssentialsDeviceFactory<LumensDocumentCameraDevice>
    {
        public LumensDocumentCameraFactory()
        {
            TypeNames = new List<string> { "lumensdoccam" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Lumens Document Camera device");

            IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);

            return new LumensDocumentCameraDevice(dc.Key, dc.Name, comms, dc);
        }
    }
}