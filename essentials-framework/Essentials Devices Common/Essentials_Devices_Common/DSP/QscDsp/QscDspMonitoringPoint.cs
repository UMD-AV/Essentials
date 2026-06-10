using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Devices.Common.DSP.QscDsp
{
    public class QscDspMonitoringPoint
    {
        public string InstanceTag { get; private set; }
        public string Name { get; private set; }
        
        private bool _isOnline;
        public bool IsSubscribed { get; private set; }
        
        public BoolFeedback IsOnline { get; private set; }
        private QscDsp Parent { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="instanceTag">level named control/instance tag</param>
        /// <param name="name"></param>
        /// <param name="parent">parent DSP instance</param>
        public QscDspMonitoringPoint(string instanceTag, string name, QscDsp parent)
        {
            InstanceTag = instanceTag;
            Name = name;
            Parent = parent;
            IsOnline = new BoolFeedback(() => _isOnline);
            
            parent.CommunicationMonitor.IsOnlineFeedback.OutputChange += (sender, args) =>
            {
                if (!args.BoolValue)
                    return;

                CrestronInvoke.BeginInvoke(o =>
                {
                    if (!string.IsNullOrEmpty(InstanceTag))
                        parent.SendLine(string.Format("cg \"{0}\"", InstanceTag));
                });
            };
        }

        /// <summary>
        /// Parses the response from the DspBase
        /// </summary>
        /// <param name="customName"></param>
        /// <param name="value"></param>
        public void ParseSubscriptionMessage(string customName, string value)
        {
            // Check for valid subscription response
            Debug.Console(1, "Monitoring Point {0} Response: '{1}'", customName, value);

                switch (value)
                {
                    case "OK":
                        _isOnline = true;
                        break;
                    default :
                        _isOnline = false;
                        break;
                }

                IsSubscribed = true;
        }

        /// <summary>
        /// Sends the subscription command of the instance tag for the provided change group
        /// </summary>
        public void Subscribe()
        {
            Parent.SendLine(string.Format("cga 1 \"{0}\"", InstanceTag));
        }
    }
}