using System;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.Ssh;
using Crestron.SimplSharp.Ssh.Common;

namespace PepperDash.Core
{
    /// <summary>
    /// 
    /// </summary>
    public class GenericSshClient : Device, ISocketStatusWithStreamDebugging, IAutoReconnect, IDisposable
    {
        /// <summary>
        /// Enables debugging to console
        /// </summary>
        public CommunicationStreamDebugging StreamDebugging { get; private set; }

        /// <summary>
        /// Event that fires when data is received.  Delivers args with byte array
        /// </summary>
        public event EventHandler<GenericCommMethodReceiveBytesArgs> BytesReceived;

        /// <summary>
        /// Event that fires when data is received.  Delivered as text.
        /// </summary>
        public event EventHandler<GenericCommMethodReceiveTextArgs> TextReceived;

        /// <summary>
        /// Event when the connection status changes.
        /// </summary>
        public event EventHandler<GenericSocketStatusChageEventArgs> ConnectionChange;

        /// <summary>
        /// Address of server
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// Port on server
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Username for server
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// And... Password for server.  That was worth documenting!
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// True when the server is connected - when status == 2.
        /// </summary>
        public bool IsConnected
        {
            // returns false if no client or not connected
            get { return Client != null && Client.IsConnected && ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED; }
        }

        /// <summary>
        /// S+ helper for IsConnected
        /// </summary>
        public ushort UIsConnected
        {
            get { return (ushort)(IsConnected ? 1 : 0); }
        }

        /// <summary>
        /// 
        /// </summary>
        public SocketStatus ClientStatus
        {
            get { return _ClientStatus; }
            private set
            {
                if (_ClientStatus == value)
                    return;
                _ClientStatus = value;
                OnConnectionChange();
            }
        }

        private SocketStatus _ClientStatus;

        /// <summary>
        /// Contains the familiar Simpl analog status values. This drives the ConnectionChange event
        /// and IsConnected with be true when this == 2.
        /// </summary>
        public ushort UStatus
        {
            get { return (ushort)_ClientStatus; }
        }

        /// <summary>
        /// Determines whether client will attempt reconnection on failure. Default is true
        /// </summary>
        public bool AutoReconnect { get; set; }

        /// <summary>
        /// Will be set and unset by connect and disconnect only
        /// </summary>
        public bool ConnectEnabled { get; private set; }

        /// <summary>
        /// Millisecond value, determines the timeout period in between reconnect attempts.
        /// Set to 5000 by default
        /// </summary>
        public int AutoReconnectIntervalMs { get; set; }

        private SshClient Client;

        private ShellStream TheStream;

        private readonly CTimer ReconnectTimer;

        //Lock object to prevent simultaneous connect/disconnect operations
        private readonly CMutex connectLock = new CMutex();

        private bool DisconnectLogged;

        /// <summary>
        /// Typical constructor.
        /// </summary>
        public GenericSshClient(string key, string hostname, int port, string username, string password) :
            base(key)
        {
            StreamDebugging = new CommunicationStreamDebugging(key);
            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironment_ProgramStatusEventHandler;
            Key = key;
            Hostname = hostname;
            Port = port;
            Username = username;
            Password = password;
            ReconnectTimer = new CTimer(ReconnectCallback, null, Timeout.Infinite);
        }

        private void ReconnectCallback(object o)
        {
            Debug.Console(2, this, "Reconnect callback status: connect enabled: {0} isConnected: {1}", ConnectEnabled,
                IsConnected);
            if (ConnectEnabled && !IsConnected)
            {
                ConnectGo();
            }
        }

        /// <summary>
        /// Handles closing this up when the program shuts down
        /// </summary>
        private void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType == eProgramStatusEventType.Stopping)
            {
                if (Client != null)
                {
                    Debug.Console(0, this, "Program stopping. Closing connection {0}", Key);
                    AutoReconnect = false;
                    Disconnect();
                    Dispose();
                    Debug.Console(0, this, "Closing connection {0} complete", Key);
                }
            }
        }

        /// <summary>
        /// Connect to the server, using the provided properties.
        /// </summary>
        public void Connect()
        {
            ConnectEnabled = true;
            ReconnectTimer.Reset(AutoReconnectIntervalMs, AutoReconnectIntervalMs);
            ConnectGo();
        }

        private void ConnectGo()
        {
            CrestronInvoke.BeginInvoke((o) =>
            {
                // Don't go unless everything is here
                if (string.IsNullOrEmpty(Hostname) || Port < 1 || Port > 65535
                    || Username == null || Password == null)
                {
                    Debug.Console(0, this, Debug.ErrorLogLevel.Error,
                        "Connect failed.  Check hostname, port, username and password are set or not null");
                    return;
                }

                try
                {
                    connectLock.WaitForMutex();
                    if (IsConnected)
                    {
                        Debug.Console(1, this, "Connection already connected.  Exiting Connect()");
                    }
                    else
                    {
                        Debug.Console(1, this, "Attempting connect");

                        // Cleanup the old client if it already exists
                        if (Client != null)
                        {
                            Debug.Console(1, this, "Cleaning up disconnected client");
                            KillClient(SocketStatus.SOCKET_STATUS_BROKEN_LOCALLY);
                        }

                        // This handles both password and keyboard-interactive (like on OS-X, 'nixes)
                        KeyboardInteractiveAuthenticationMethod kauth =
                            new KeyboardInteractiveAuthenticationMethod(Username);
                        kauth.AuthenticationPrompt += kauth_AuthenticationPrompt;
                        PasswordAuthenticationMethod pauth = new PasswordAuthenticationMethod(Username, Password);

                        Debug.Console(1, this, "Creating new SshClient");
                        ConnectionInfo connectionInfo = new ConnectionInfo(Hostname, Port, Username, pauth, kauth);
                        if (Client != null) Client.ErrorOccurred -= Client_ErrorOccurred;
                        Client = new SshClient(connectionInfo);
                        Client.ErrorOccurred += Client_ErrorOccurred;

                        //Attempt to connect
                        ClientStatus = SocketStatus.SOCKET_STATUS_WAITING;
                        try
                        {
                            Client.Connect();
                            CreateStream();
                            Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "Connected");
                            ClientStatus = SocketStatus.SOCKET_STATUS_CONNECTED;
                            DisconnectLogged = false;
                        }
                        catch (SshConnectionException e)
                        {
                            Exception ie = e.InnerException; // The details are inside!!
                            Debug.ErrorLogLevel errorLogLevel = DisconnectLogged
                                ? Debug.ErrorLogLevel.None
                                : Debug.ErrorLogLevel.Error;

                            if (ie is SocketException)
                                Debug.Console(1, this, errorLogLevel,
                                    "'{0}' CONNECTION failure: Cannot reach host, ({1})",
                                    Key, ie.Message);
                            else if (ie is System.Net.Sockets.SocketException)
                                Debug.Console(1, this, errorLogLevel,
                                    "'{0}' Connection failure: Cannot reach host '{1}' on port {2}, ({3})",
                                    Key, Hostname, Port, ie.GetType());
                            else if (ie is SshAuthenticationException)
                            {
                                Debug.Console(1, this, errorLogLevel,
                                    "Authentication failure for username '{0}', ({1})",
                                    Username, ie.Message);
                            }
                            else
                                Debug.Console(1, this, errorLogLevel, "Error on connect:\r({0})", e.Message);

                            DisconnectLogged = true;
                            KillClient(SocketStatus.SOCKET_STATUS_CONNECT_FAILED);
                        }
                        catch (Exception e)
                        {
                            Debug.ErrorLogLevel errorLogLevel = DisconnectLogged
                                ? Debug.ErrorLogLevel.None
                                : Debug.ErrorLogLevel.Error;
                            Debug.Console(1, this, errorLogLevel, "Unhandled exception on connect:\r({0})", e.Message);
                            DisconnectLogged = true;
                            KillClient(SocketStatus.SOCKET_STATUS_CONNECT_FAILED);
                        }
                    }
                }
                finally
                {
                    connectLock.ReleaseMutex();
                }
            });
        }

        /// <summary>
        /// Disconnect the clients and put away it's resources.
        /// </summary>
        public void Disconnect()
        {
            ConnectEnabled = false;
            ReconnectTimer.Stop();
            DisconnectGo();
        }


        private void DisconnectGo()
        {
            KillClient(SocketStatus.SOCKET_STATUS_BROKEN_LOCALLY);
        }

        /// <summary>
        /// Kills the stream, cleans up the client and sets it to null
        /// </summary>
        private void KillClient(SocketStatus status)
        {
            KillStream();

            if (Client != null)
            {
                try
                {
                    Client.Disconnect();
                    Client.Dispose();
                    Client = null;
                    ClientStatus = status;
                    Debug.Console(1, this, "Disconnected client");
                }
                catch (Exception ex)
                {
                    Debug.Console(1, this, "Exception killing client: {0}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Kills the stream
        /// </summary>
        private void KillStream()
        {
            if (TheStream != null)
            {
                TheStream.DataReceived -= Stream_DataReceived;
                TheStream.ErrorOccurred -= StreamErrorOccurredHandler;
                TheStream.Close();
                TheStream.Dispose();
                TheStream = null;
                Debug.Console(1, this, "Disconnected stream");
            }
        }

        /// <summary>
        /// Creates the stream
        /// </summary>
        private void CreateStream()
        {
            if (Client != null)
            {
                TheStream = Client.CreateShellStream("PDTShell", 100, 80, 100, 200, 65534);
                TheStream.DataReceived += Stream_DataReceived;
                TheStream.ErrorOccurred += StreamErrorOccurredHandler;
            }
        }

        private void StreamErrorOccurredHandler(object sender, EventArgs e)
        {
            Debug.Console(0, this, "SSH Shellstream error: {0}", e);
            DisconnectGo();
            ConnectGo();
        }


        /// <summary>
        /// Handles the keyboard interactive authentication, should it be required.
        /// </summary>
        private void kauth_AuthenticationPrompt(object sender, AuthenticationPromptEventArgs e)
        {
            foreach (AuthenticationPrompt prompt in e.Prompts)
                if (prompt.Request.IndexOf("Password:", StringComparison.InvariantCultureIgnoreCase) != -1)
                    prompt.Response = Password;
        }

        /// <summary>
        /// Handler for data receive on ShellStream.  Passes data across to queue for line parsing.
        /// </summary>
        private void Stream_DataReceived(object sender, ShellDataEventArgs e)
        {
            byte[] bytes = e.Data;
            if (bytes.Length > 0)
            {
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
                    string str = Encoding.GetEncoding(28591).GetString(bytes, 0, bytes.Length);
                    if (StreamDebugging.RxStreamDebuggingIsEnabled)
                        Debug.Console(0, this, "Received: '{0}'", ComTextHelper.GetDebugText(str));

                    textHandler(this, new GenericCommMethodReceiveTextArgs(str));
                }
            }
        }


        /// <summary>
        /// Error event handler for client events - disconnect, etc.  Will forward those events via ConnectionChange
        /// event
        /// </summary>
        private void Client_ErrorOccurred(object sender, ExceptionEventArgs e)
        {
            CrestronInvoke.BeginInvoke(o =>
            {
                if (e.Exception is SshConnectionException || e.Exception is System.Net.Sockets.SocketException)
                    Debug.Console(1, this, Debug.ErrorLogLevel.Error, "Disconnected by remote");
                else
                    Debug.Console(1, this, Debug.ErrorLogLevel.Error, "Unhandled SSH client error: {0}", e.Exception);

                KillClient(SocketStatus.SOCKET_STATUS_BROKEN_REMOTELY);
            });
        }

        /// <summary>
        /// Helper for ConnectionChange event
        /// </summary>
        private void OnConnectionChange()
        {
            if (ConnectionChange != null)
                ConnectionChange(this, new GenericSocketStatusChageEventArgs(this));
        }

        #region IBasicCommunication Members

        /// <summary>
        /// Sends text to the server
        /// </summary>
        /// <param name="text"></param>
        public void SendText(string text)
        {
            try
            {
                if (Client != null && IsConnected)
                {
                    if (StreamDebugging.TxStreamDebuggingIsEnabled)
                        Debug.Console(0, this, "Sending {0} characters of text: '{1}'", text.Length,
                            ComTextHelper.GetDebugText(text));

                    if (TheStream != null && TheStream.CanWrite)
                    {
                        TheStream.WriteLine(text);
                        TheStream.Flush();
                    }
                    else
                    {
                        Debug.Console(0, this, "The ssh stream is null or not writable, recreating stream");
                        KillStream();
                        CreateStream();
                    }
                }
                else
                {
                    Debug.Console(1, this, "Client is null or disconnected.  Cannot Send Text");
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, "Exception: {0}", ex.Message);
                Debug.Console(0, "Stack Trace: {0}", ex.StackTrace);
                DisconnectGo();
                ConnectGo();
            }
        }

        /// <summary>
        /// Sends Bytes to the server
        /// </summary>
        /// <param name="bytes"></param>
        public void SendBytes(byte[] bytes)
        {
            try
            {
                if (Client != null && IsConnected)
                {
                    if (StreamDebugging.TxStreamDebuggingIsEnabled)
                        Debug.Console(0, this, "Sending {0} bytes: '{1}'", bytes.Length,
                            ComTextHelper.GetEscapedText(bytes));

                    if (TheStream != null && TheStream.CanWrite)
                    {
                        TheStream.Write(bytes, 0, bytes.Length);
                        TheStream.Flush();
                    }
                    else
                    {
                        Debug.Console(0, this, "The ssh stream is null or not writable, recreating stream");
                        KillStream();
                        CreateStream();
                    }
                }
                else
                {
                    Debug.Console(1, this, "Client is null or disconnected.  Cannot Send Bytes");
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, "Exception: {0}", ex.Message);
                Debug.Console(0, "Stack Trace: {0}", ex.StackTrace);
                DisconnectGo();
                ConnectGo();
            }
        }

        #endregion

        public void Dispose()
        {
            if (Client != null) Client.Dispose();
            if (TheStream != null) TheStream.Dispose();
            if (ReconnectTimer != null) ReconnectTimer.Dispose();
            if (connectLock != null) connectLock.Dispose();
        }
    }

    //*****************************************************************************************************
    //*****************************************************************************************************
    /// <summary>
    /// Fired when connection changes
    /// </summary>
    public class SshConnectionChangeEventArgs : EventArgs
    {
        /// <summary>
        /// Connection State
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Connection Status represented as a ushort
        /// </summary>
        public ushort UIsConnected
        {
            get { return (ushort)(Client.IsConnected ? 1 : 0); }
        }

        /// <summary>
        /// The client
        /// </summary>
        public GenericSshClient Client { get; private set; }

        /// <summary>
        /// Socket Status as represented by
        /// </summary>
        public ushort Status
        {
            get { return Client.UStatus; }
        }

        /// <summary>
        ///  S+ Constructor
        /// </summary>
        public SshConnectionChangeEventArgs()
        {
        }

        /// <summary>
        /// EventArgs class
        /// </summary>
        /// <param name="isConnected">Connection State</param>
        /// <param name="client">The Client</param>
        public SshConnectionChangeEventArgs(bool isConnected, GenericSshClient client)
        {
            IsConnected = isConnected;
            Client = client;
        }
    }
}