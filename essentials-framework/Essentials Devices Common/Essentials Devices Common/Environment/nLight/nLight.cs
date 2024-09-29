using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Lighting;
using LightingBase = PepperDash.Essentials.Core.Lighting.LightingBase;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core.Logging;

namespace PepperDash.Essentials.Devices.Common.Environment.NLight
{
    public class NLight : LightingBase, ICommunicationMonitor
    {
        public IBasicCommunication Communication { get; private set; }
        public StatusMonitorBase CommunicationMonitor { get; private set; }
        readonly nLightQueue _cmdQueue;
        CMutex _CommandMutex;
        NLightPropertiesConfig _props;
        bool _readyForNextCommand;

        public NLight(string key, string name, IBasicCommunication comm, NLightPropertiesConfig props)
            : base(key, name)
        {
            Communication = comm;
            _props = props;
            _cmdQueue = new nLightQueue();
            _CommandMutex = new CMutex();
            _readyForNextCommand = true;

            if (props.Scenes != null)
            {
                LightingScenes = props.Scenes;
            }

            Communication.BytesReceived += Communication_BytesReceived;
            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 60000, 120000, 300000, Poll);
        }

        public override bool CustomActivate()
        {
            Communication.Connect();
            CommunicationMonitor.Start();
            return true;
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new GenericLightingJoinMap(joinStart);
            LinkLightingToApi(trilist, joinStart, joinMapKey, bridge);

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
        }

        /// <summary>
        /// Handles all responses from device
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        void Communication_BytesReceived(object sender, GenericCommMethodReceiveBytesArgs args)
        {
            Debug.Console(2, this, "Received new bytes:{0}", ComTextHelper.GetEscapedText(args.Bytes));
            try
            {
                if (args.Bytes.Length > 2)
                {
                    if (args.Bytes[0] == 0xA5)
                    {
                        _readyForNextCommand = true;
                        if (args.Bytes[2] == 0x0D)
                        {
                            Debug.Console(1, this, "Found poll response");

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Console(1, this, "Exception processing response {0}", ex.Message);
            }
        }

        public void SetOff(uint channel)
        {
            if (channel > 0 && channel <= 10)
            {
                Debug.Console(1, this, "Setting channel {0} to off", channel);
                byte[] data = new byte[3];
                data[0] = (byte)channel;
                data[1] = 0x02;
                data[2] = 0x00;
                SendData(0x7A, data);
            }
        }

        public void SetLevel(uint channel, uint levelPercent)
        {
            if (channel > 0 && channel <= 10)
            {
                if (levelPercent > 0 && levelPercent <= 100)
                {
                    Debug.Console(1, this, "Setting channel {0} to level {1}", channel, levelPercent);
                    byte[] data = new byte[3];
                    data[0] = (byte)channel;
                    data[1] = 0x05;
                    data[2] = (byte)levelPercent;
                    SendData(0x7A, data);
                }
                else if (levelPercent == 0)
                {
                    SetOff(channel);
                }
            }
        }

        /// <summary>
        /// Recalls the specified scene
        /// </summary>
        /// <param name="scene"></param>
        /// 
        public override void SelectScene(LightingScene scene)
        {
            if (LightingScenes != null && LightingScenes.Exists(o => o.Name == scene.Name))
            {
                SelectScene((ushort)LightingScenes.FindIndex(o => o.Name == scene.Name));
            }
        }

        /// <summary>
        /// Recalls the specified scene
        /// </summary>
        /// <param name="scene"></param>
		/// 
        public void SelectScene(ushort scene)
        {
            try
            {
                if (LightingScenes != null && LightingScenes[scene] != null)
                {
                    if (LightingScenes[scene].ID != null)
                    {
                        Debug.Console(1, this, "Selecting Scene: {0}", LightingScenes[scene].ID);
                        byte[] data = new byte[1];
                        data[0] = (byte)Convert.ToUInt16(LightingScenes[scene].ID);
                        SendData(0x85, data);
                    }
                    else if(LightingScenes[scene].Levels != null && LightingScenes[scene].Levels.Length > 0)
                    {
                        Debug.Console(1, this, "Selecting Scene Levels");
                        foreach (var level in LightingScenes[scene].Levels)
                        {
                            SetLevel(level.Index, level.Level);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Debug.Console(1, this, "Error selecting lighting scene with index {0}, ex: {1}", scene, ex.Message);
            }
        }

        private void Poll()
        {
            SendData(0x0C, new byte[0]);
        }

        /// <summary>
        /// Appends the delimiter and sends the string
        /// </summary>
        /// <param name="s"></param>
        public void SendData(byte subject, byte[] data)
        {
            int length = 5 + data.Length;
            if (length <= 50)
            {
                byte[] bytesToSend = new byte[length];
                bytesToSend[0] = 0xA5;
                bytesToSend[1] = (byte)length;
                bytesToSend[2] = subject;

                int count = 0;
                int checksum1 = 0xA5 ^ subject;
                int checksum2 = length;
                while (count < data.Length)
                {
                    bytesToSend[count + 3] = data[count];   //copy data to the data array being sent in the proper location

                    /*************
                     * The 16-bit (2 bytes) checksum is calculated over all received/transmitted bytes B1...Bn in the data frame except
                     * the checksum bytes themselves by XORing odd bytes and even bytes separately and inverting the result.
                     * Formula:
                     * CK1 = INV [ B1 XOR B3 XOR ... XOR Bn–1 ]
                     * CK2 = INV [ B2 XOR B4 XOR ... XOR Bn ]
                     * For example:
                     * To send the packet: A5 08 7A 01 04 05 CK1 CK2, the sender must calculate CK1 and CK2 and attach them to the end of the packet.
                     * CK1 = INV[A5 ^ 7A ^ 04] = 24
                     * CK2 = INV[08 ^ 01 ^ 05] = F3
                     *************/

                    //Now build the checksums
                    if (count % 2 == 0)
                    {
                        checksum2 = checksum2 ^ data[count];
                    }
                    else
                    {
                        checksum1 = checksum1 ^ data[count];   
                    }
                    count++;
                }

                bytesToSend[length - 2] = (byte)~checksum1;
                bytesToSend[length - 1] = (byte)~checksum2;

                _cmdQueue.AddCommand(bytesToSend);
                ProcessQueue();
            }
            else
            {
                Debug.Console(0, this, "Data length is too long");
            }
        }

        private void ProcessQueue()
        {
            bool test = _CommandMutex.WaitForMutex(100);
            if (test)
            {
                //Pace the commands sending out
                while (_cmdQueue.Count > 0)
                {
                    try
                    {
                        byte[] data = _cmdQueue.Dequeue();
                        if (data != null)
                        {
                            _readyForNextCommand = false;
                            Debug.Console(2, this, "Sending bytes: {0}", ComTextHelper.GetEscapedText(data));
                            Communication.SendBytes(data);

                            int count = 0;
                            while (!_readyForNextCommand && count < 100)
                            {
                                Thread.Sleep(20);
                                count++;
                            }
                            if (count >= 100)
                            {
                                Debug.Console(1, this, "ProcessQueue timed out waiting for next command");
                            }
                            else
                            {
                                //Delay due to nLight not liking too fast paced commands
                                Thread.Sleep(500);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(0, this, "Caught an exception in ProcessQueue {0}\r{1}\r{2}", ex.Message, ex.InnerException, ex.StackTrace);
                    }
                }
                _CommandMutex.ReleaseMutex();
            }
        }
    }

    public class nLightQueue
    {
        public List<byte[]> Q = new List<byte[]>();
        public ushort Count { get { return (ushort)Q.Count; } }
        private CMutex mutex = new CMutex();

        /// <summary>
        /// Creates a queue for processing nLight commands
        /// </summary>
        public nLightQueue()
        {
        }

        public void AddCommand(byte[] command)
        {
            mutex.WaitForMutex();
            try
            {
                Q.Add(command);
            }
            catch (Exception ex)
            {
                Debug.Console(1, "Exception in nLight command queue add: {0}", ex);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public void ClearQueue()
        {
            mutex.WaitForMutex();
            try
            {
                Q.Clear();
            }
            catch (Exception ex)
            {
                Debug.Console(1, "Exception in nLight command queue clear: {0}", ex);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public byte[] Dequeue()
        {
            byte[] cmd;
            mutex.WaitForMutex();
            try
            {
                if (Q.Count > 0)
                {
                    cmd = Q[0];
                    Q.RemoveAt(0);
                    return cmd;
                }
            }
            catch (Exception ex)
            {
                Debug.Console(1, "Exception in nLight command queue dequeue: {0}", ex);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
            return null;
        }
    }

    public class NLightPropertiesConfig
    {
        public ControlPropertiesConfig Control { get; set; }

        public List<LightingScene> Scenes { get; set; }
    }

    public class NLightFactory : EssentialsDeviceFactory<NLight>
    {
        public NLightFactory()
        {
            TypeNames = new List<string>() { "nlight" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new nLight Device");
            var comm = CommFactory.CreateCommForDevice(dc);

            var props = Newtonsoft.Json.JsonConvert.DeserializeObject<Environment.NLight.NLightPropertiesConfig>(dc.Properties.ToString());

            return new NLight(dc.Key, dc.Name, comm, props);
        }
    }

}