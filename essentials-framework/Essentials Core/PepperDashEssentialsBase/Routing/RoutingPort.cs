using System;
using PepperDash.Core;


namespace PepperDash.Essentials.Core
{
    /// <summary>
    /// Base class for RoutingInput and Output ports
    /// </summary>
    public abstract class RoutingPort : IKeyed
    {
        public string Key { get; private set; }
        public eRoutingSignalType Type { get; private set; }
        public eRoutingPortConnectionType ConnectionType { get; private set; }
        public readonly object Selector;
        public bool IsInternal { get; private set; }
        public object FeedbackMatchObject { get; set; }
        public object Port { get; set; }

        public RoutingPort(string key, eRoutingSignalType type, eRoutingPortConnectionType connType, object selector,
            bool isInternal)
        {
            Key = key;
            Type = type;
            ConnectionType = connType;
            Selector = selector;
            IsInternal = isInternal;
        }
    }

    [Flags]
    public enum eRoutingSignalType
    {
        Audio = 1,
        Video = 2,
        AudioVideo = Audio | Video,
        UsbOutput = 8,
        UsbInput = 16
    }

    public enum eRoutingPortConnectionType
    {
        None,
        BackplaneOnly,
        DisplayPort,
        Dvi,
        Hdmi,
        Rgb,
        Vga,
        LineAudio,
        DigitalAudio,
        Sdi,
        Composite,
        Component,
        DmCat,
        DmMmFiber,
        DmSmFiber,
        Speaker,
        Streaming
    }

    /// <summary>
    /// Basic RoutingInput with no statuses.
    /// </summary>
    public class RoutingInputPort : RoutingPort
    {
        /// <summary>
        /// The IRoutingInputs object this lives on
        /// </summary>
        public IRoutingInputs ParentDevice { get; private set; }

        /// <summary>
        /// Constructor for a basic RoutingInputPort
        /// </summary>
        public RoutingInputPort(string key, eRoutingSignalType type, eRoutingPortConnectionType connType,
            object selector, IRoutingInputs parent)
            : this(key, type, connType, selector, parent, false)
        {
        }

        /// <summary>
        /// Constructor for a virtual routing input port that lives inside a device. For example
        /// the ports that link a DM card to a DM matrix bus
        /// </summary>
        public RoutingInputPort(string key, eRoutingSignalType type, eRoutingPortConnectionType connType,
            object selector, IRoutingInputs parent, bool isInternal)
            : base(key, type, connType, selector, isInternal)
        {
            if (parent == null)
                throw new ArgumentNullException("parent");
            ParentDevice = parent;
        }
    }

    /// <summary>
    /// A RoutingInputPort for devices like DM-TX and DM input cards. 
    /// Will provide video statistics on connected signals
    /// </summary>
    public class RoutingInputPortWithVideoStatuses : RoutingInputPort
    {
        /// <summary>
        /// Video statuses attached to this port
        /// </summary>
        public VideoStatusOutputs VideoStatus { get; private set; }

        /// <summary>
        /// Constructor 
        /// </summary>
        public RoutingInputPortWithVideoStatuses(string key,
            eRoutingSignalType type, eRoutingPortConnectionType connType, object selector,
            IRoutingInputs parent, VideoStatusFuncsWrapper funcs) :
            base(key, type, connType, selector, parent)
        {
            VideoStatus = new VideoStatusOutputs(funcs);
        }
    }

    public class RoutingOutputPort : RoutingPort
    {
        /// <summary>
        /// The IRoutingOutputs object this port lives on
        /// </summary>
        public IRoutingOutputs ParentDevice { get; private set; }

        public InUseTracking InUseTracker { get; private set; }


        /// <summary>
        /// </summary>
        public RoutingOutputPort(string key, eRoutingSignalType type, eRoutingPortConnectionType connType,
            object selector, IRoutingOutputs parent)
            : this(key, type, connType, selector, parent, false)
        {
        }

        public RoutingOutputPort(string key, eRoutingSignalType type, eRoutingPortConnectionType connType,
            object selector, IRoutingOutputs parent, bool isInternal)
            : base(key, type, connType, selector, isInternal)
        {
            if (parent == null)
                throw new ArgumentNullException("parent");
            ParentDevice = parent;
            InUseTracker = new InUseTracking();
        }

        public override string ToString()
        {
            return ParentDevice.Key + ":" + Key;
        }
    }
}