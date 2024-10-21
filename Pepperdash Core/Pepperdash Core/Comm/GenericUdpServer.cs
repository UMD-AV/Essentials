﻿using System;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Newtonsoft.Json;

namespace PepperDash.Core
{
    public class GenericUdpServer : Device, ISocketStatusWithStreamDebugging
    {
        private const string SplusKey = "Uninitialized Udp Server";
        public CommunicationStreamDebugging StreamDebugging { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<GenericCommMethodReceiveBytesArgs> BytesReceived;

        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<GenericCommMethodReceiveTextArgs> TextReceived;

        /// <summary>
        /// This event will fire when a message is dequeued that includes the source IP and Port info if needed to determine the source of the received data.
        /// </summary>
        public event EventHandler<GenericUdpReceiveTextExtraArgs> DataRecievedExtra;

        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<GenericSocketStatusChageEventArgs> ConnectionChange;

        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<GenericUdpConnectedEventArgs> UpdateConnectionStatus;

        /// <summary>
        /// 
        /// </summary>
        public SocketStatus ClientStatus
        {
            get { return Server.ServerStatus; }
        }

        /// <summary>
        /// 
        /// </summary>
        public ushort UStatus
        {
            get { return (ushort)Server.ServerStatus; }
        }

        /// <summary>
        /// Address of server
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// IP Address of the sender of the last recieved message 
        /// </summary>
        /// <summary>
        /// Port on server
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Another damn S+ helper because S+ seems to treat large port nums as signed ints
        /// which screws up things
        /// </summary>
        public ushort UPort
        {
            get { return Convert.ToUInt16(Port); }
            set { Port = Convert.ToInt32(value); }
        }

        /// <summary>
        /// Indicates that the UDP Server is enabled
        /// </summary>
        public bool IsConnected { get; private set; }

        public ushort UIsConnected
        {
            get { return IsConnected ? (ushort)1 : (ushort)0; }
        }

        /// <summary>
        /// Defaults to 2000
        /// </summary>
        public int BufferSize { get; set; }

        public UDPServer Server { get; private set; }

        /// <summary>
        /// Constructor for S+. Make sure to set key, address, port, and buffersize using init method
        /// </summary>
        public GenericUdpServer()
            : base(SplusKey)
        {
            StreamDebugging = new CommunicationStreamDebugging(SplusKey);
            BufferSize = 5000;

            CrestronEnvironment.ProgramStatusEventHandler +=
                new ProgramStatusEventHandler(CrestronEnvironment_ProgramStatusEventHandler);
            CrestronEnvironment.EthernetEventHandler +=
                new EthernetEventHandler(CrestronEnvironment_EthernetEventHandler);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="bufferSize"></param>
        public GenericUdpServer(string key, string address, int port, int bufferSize)
            : base(key)
        {
            StreamDebugging = new CommunicationStreamDebugging(key);
            Hostname = address;
            Port = port;
            BufferSize = bufferSize;

            CrestronEnvironment.ProgramStatusEventHandler +=
                new ProgramStatusEventHandler(CrestronEnvironment_ProgramStatusEventHandler);
            CrestronEnvironment.EthernetEventHandler +=
                new EthernetEventHandler(CrestronEnvironment_EthernetEventHandler);
        }

        /// <summary>
        /// Call from S+ to initialize values
        /// </summary>
        /// <param name="key"></param>
        /// <param name="address"></param>
        /// <param name="port"></param>
        public void Initialize(string key, string address, ushort port)
        {
            Key = key;
            Hostname = address;
            UPort = port;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ethernetEventArgs"></param>
        private void CrestronEnvironment_EthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            // Re-enable the server if the link comes back up and the status should be connected
            if (ethernetEventArgs.EthernetEventType == eEthernetEventType.LinkUp
                && IsConnected)
            {
                Connect();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="programEventType"></param>
        private void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping)
                return;

            Debug.Console(1, this, "Program stopping. Disabling Server");
            Disconnect();
        }

        /// <summary>
        /// Enables the UDP Server
        /// </summary>
        public void Connect()
        {
            if (Server == null)
            {
                Server = new UDPServer();
            }

            if (string.IsNullOrEmpty(Hostname))
            {
                Debug.Console(1, Debug.ErrorLogLevel.Warning, "GenericUdpServer '{0}': No address set", Key);
                return;
            }

            if (Port < 1 || Port > 65535)
            {
                {
                    Debug.Console(1, Debug.ErrorLogLevel.Warning, "GenericUdpServer '{0}': Invalid port", Key);
                    return;
                }
            }

            SocketErrorCodes status = Server.EnableUDPServer(Hostname, Port);

            Debug.Console(2, this, "SocketErrorCode: {0}", status);
            if (status == SocketErrorCodes.SOCKET_OK)
                IsConnected = true;

            EventHandler<GenericUdpConnectedEventArgs> handler = UpdateConnectionStatus;
            if (handler != null)
                handler(this, new GenericUdpConnectedEventArgs(UIsConnected));

            // Start receiving data
            Server.ReceiveDataAsync(Receive);
        }

        /// <summary>
        /// Disabled the UDP Server
        /// </summary>
        public void Disconnect()
        {
            if (Server != null)
                Server.DisableUDPServer();

            IsConnected = false;

            EventHandler<GenericUdpConnectedEventArgs> handler = UpdateConnectionStatus;
            if (handler != null)
                handler(this, new GenericUdpConnectedEventArgs(UIsConnected));
        }


        /// <summary>
        /// Recursive method to receive data
        /// </summary>
        /// <param name="server"></param>
        /// <param name="numBytes"></param>
        private void Receive(UDPServer server, int numBytes)
        {
            Debug.Console(2, this, "Received {0} bytes", numBytes);

            try
            {
                if (numBytes <= 0)
                    return;

                string sourceIp = Server.IPAddressLastMessageReceivedFrom;
                int sourcePort = Server.IPPortLastMessageReceivedFrom;
                byte[] bytes = server.IncomingDataBuffer.Take(numBytes).ToArray();
                string str = Encoding.GetEncoding(28591).GetString(bytes, 0, bytes.Length);

                EventHandler<GenericUdpReceiveTextExtraArgs> dataRecivedExtra = DataRecievedExtra;
                if (dataRecivedExtra != null)
                    dataRecivedExtra(this, new GenericUdpReceiveTextExtraArgs(str, sourceIp, sourcePort, bytes));

                Debug.Console(2, this, "Bytes: {0}", bytes.ToString());
                EventHandler<GenericCommMethodReceiveBytesArgs> bytesHandler = BytesReceived;
                if (bytesHandler != null)
                {
                    if (StreamDebugging.RxStreamDebuggingIsEnabled)
                    {
                        Debug.Console(0, this, "Received {1} bytes: '{0}'", ComTextHelper.GetEscapedText(bytes),
                            bytes.Length);
                    }

                    bytesHandler(this, new GenericCommMethodReceiveBytesArgs(bytes));
                }

                EventHandler<GenericCommMethodReceiveTextArgs> textHandler = TextReceived;
                if (textHandler != null)
                {
                    if (StreamDebugging.RxStreamDebuggingIsEnabled)
                        Debug.Console(0, this, "Received {1} characters of text: '{0}'",
                            ComTextHelper.GetDebugText(str), str.Length);
                    textHandler(this, new GenericCommMethodReceiveTextArgs(str));
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, "GenericUdpServer Receive error: {0}{1}", ex.Message, ex.StackTrace);
            }
            finally
            {
                server.ReceiveDataAsync(Receive);
            }
        }

        /// <summary>
        /// General send method
        /// </summary>
        /// <param name="text"></param>
        public void SendText(string text)
        {
            byte[] bytes = Encoding.GetEncoding(28591).GetBytes(text);

            if (IsConnected && Server != null)
            {
                if (StreamDebugging.TxStreamDebuggingIsEnabled)
                    Debug.Console(0, this, "Sending {0} characters of text: '{1}'", text.Length,
                        ComTextHelper.GetDebugText(text));

                Server.SendData(bytes, bytes.Length);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        public void SendBytes(byte[] bytes)
        {
            if (StreamDebugging.TxStreamDebuggingIsEnabled)
                Debug.Console(0, this, "Sending {0} bytes: '{1}'", bytes.Length, ComTextHelper.GetEscapedText(bytes));

            if (IsConnected && Server != null)
                Server.SendData(bytes, bytes.Length);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class GenericUdpReceiveTextExtraArgs : EventArgs
    {
        /// <summary>
        /// 
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public string IpAddress { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public byte[] Bytes { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="ipAddress"></param>
        /// <param name="port"></param>
        /// <param name="bytes"></param>
        public GenericUdpReceiveTextExtraArgs(string text, string ipAddress, int port, byte[] bytes)
        {
            Text = text;
            IpAddress = ipAddress;
            Port = port;
            Bytes = bytes;
        }

        /// <summary>
        /// Stupid S+ Constructor
        /// </summary>
        public GenericUdpReceiveTextExtraArgs()
        {
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class UdpServerPropertiesConfig
    {
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string Address { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public int Port { get; set; }

        /// <summary>
        /// Defaults to 32768
        /// </summary>
        public int BufferSize { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public UdpServerPropertiesConfig()
        {
            BufferSize = 32768;
        }
    }
}