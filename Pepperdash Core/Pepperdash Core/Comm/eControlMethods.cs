namespace PepperDash.Core
{
    /// <summary>
    /// Crestron Control Methods for a comm object
    /// </summary>
    public enum eControlMethod
    {
        None = 0,
        Com,
        IpId,
        IpidTcp,
        IpidModern,
        IR,
        Ssh,
        Tcpip,
        Telnet,
        Cresnet,
        Cec,
        Udp,
        Http,
        Https,
        Ws,
        Wss,
        SecureTcpIp,
        UdpShared
    }
}