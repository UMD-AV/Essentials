using System;
using PepperDash.Essentials.Core;

namespace NvxEpi.Features.Routing
{
    public class UsbRouter : EssentialsDevice, IRouting
    {
        public UsbRouter(string key) : base(key)
        {
        }


        #region IRouting Members

        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
        }

        #endregion

        #region IRoutingInputs Members

        public RoutingPortCollection<RoutingInputPort> InputPorts
        {
            get { throw new NotImplementedException(); }
        }

        #endregion

        #region IRoutingOutputs Members

        public RoutingPortCollection<RoutingOutputPort> OutputPorts
        {
            get { throw new NotImplementedException(); }
        }

        #endregion
    }
}