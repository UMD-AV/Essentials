using System;
using System.Text;
using Crestron.SimplSharp.CrestronWebSocketClient;
using PepperDash.Core;

namespace PepperDash.Essentials.Devices.Common.LightwareUcx
{
    public class LightwareUcxWebSocket : IBasicCommunication
    {
        private readonly WebSocketClient _client;
        public string Key { get; private set; }

        public LightwareUcxWebSocket(string key, ControlPropertiesConfig controlConfig)
        {
            if (string.IsNullOrEmpty(key) || controlConfig == null)
            {
                Debug.ConsoleWithLog(0,
                    "WebSocket host is null or empty - failed to instantiate websocket client");
                return;
            }

            if (string.IsNullOrEmpty(controlConfig.TcpSshProperties.Username) ||
                string.IsNullOrEmpty(controlConfig.TcpSshProperties.Password))
            {
                Debug.ConsoleWithLog(0,
                    "WebSocket has no login information - failed to instantiate websocket client");
                return;
            }

            Key = string.Format("{0}-websocket", key).ToLower();


            _client =
                new WebSocketClient
                {
                    URL = string.Format("wss://{0}:{1}@{2}/lw3", controlConfig.TcpSshProperties.Username,
                        controlConfig.TcpSshProperties.Password, controlConfig.TcpSshProperties.Address),
                    KeepAlive = true,
                    ConnectionCallBack = WebsocketConnected,
                    DisconnectCallBack = WebsocketDisconnected,
                    ReceiveCallBack = WebsocketReceiveCallback
                };
        }

        private int WebsocketConnected(WebSocketClient.WEBSOCKET_RESULT_CODES error)
        {
            Debug.Console(1, this, "Websocket Connected Result = {0}", error);
            IsConnected = true;
            return (int)error;
        }

        private int WebsocketDisconnected(WebSocketClient.WEBSOCKET_RESULT_CODES error, object item)
        {
            Debug.Console(1, this, "Websocket Disconnected Result = {0}", error);
            IsConnected = false;
            return (int)error;
        }

        private int WebsocketReceiveCallback(byte[] data, uint dataLen,
            WebSocketClient.WEBSOCKET_PACKET_TYPES opcode, WebSocketClient.WEBSOCKET_RESULT_CODES error)
        {
            if (opcode != WebSocketClient.WEBSOCKET_PACKET_TYPES.LWS_WS_OPCODE_07__TEXT_FRAME) return (int)error;
            string strData = Encoding.GetEncoding(28591).GetString(data, 0, data.Length);
            Debug.Console(0, this, "Incoming Data Packet From Websocket");
            Debug.Console(0, this, "{0}", strData);

            if (BytesReceived != null)
            {
                BytesReceived(this, new GenericCommMethodReceiveBytesArgs(data));
            }

            if (TextReceived != null)
            {
                TextReceived(this, new GenericCommMethodReceiveTextArgs(strData));
            }

            return (int)error;
        }

        public event EventHandler<GenericCommMethodReceiveBytesArgs> BytesReceived;
        public event EventHandler<GenericCommMethodReceiveTextArgs> TextReceived;
        public bool IsConnected { get; private set; }

        public void Connect()
        {
            _client.Connect();
        }

        public void Disconnect()
        {
            _client.Disconnect();
        }

        public void SendText(string text)
        {
            byte[] textBytes = Encoding.GetEncoding(28591).GetBytes(text);
            _client.Send(textBytes, (uint)textBytes.Length,
                WebSocketClient.WEBSOCKET_PACKET_TYPES.LWS_WS_OPCODE_07__TEXT_FRAME);
        }

        public void SendBytes(byte[] bytes)
        {
            _client.Send(bytes, (uint)bytes.Length,
                WebSocketClient.WEBSOCKET_PACKET_TYPES.LWS_WS_OPCODE_07__TEXT_FRAME);
        }
    }
}