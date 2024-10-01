using Crestron.SimplSharpPro;

namespace PepperDash.Essentials.Core.SmartObjects
{
    public class SmartObjectDPad : SmartObjectHelperBase
    {
        public BoolOutputSig SigUp
        {
            get { return GetBoolOutputNamed("Up"); }
        }

        public BoolOutputSig SigDown
        {
            get { return GetBoolOutputNamed("Down"); }
        }

        public BoolOutputSig SigLeft
        {
            get { return GetBoolOutputNamed("Left"); }
        }

        public BoolOutputSig SigRight
        {
            get { return GetBoolOutputNamed("Right"); }
        }

        public BoolOutputSig SigCenter
        {
            get { return GetBoolOutputNamed("Center"); }
        }

        public SmartObjectDPad(SmartObject so, bool useUserObjectHandler)
            : base(so, useUserObjectHandler)
        {
        }
    }
}