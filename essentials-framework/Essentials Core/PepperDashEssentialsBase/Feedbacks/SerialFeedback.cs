using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;

namespace PepperDash.Essentials.Core
{
    /// <summary>
    /// To be used for serial data feedback where the event chain / asynchronicity must be maintained
    /// and calculating the value based on a Func when it is needed will not suffice.
    /// </summary>
    public class SerialFeedback : Feedback
    {
        public override string SerialValue
        {
            get { return _SerialValue; }
        }

        private string _SerialValue;

        //public override eCueType Type { get { return eCueType.Serial; } }

        /// <summary>
        /// Used in testing.  Set/Clear functions
        /// </summary>
        public string TestValue { get; private set; }

        private List<StringInputSig> LinkedInputSigs = new List<StringInputSig>();
        private CMutex LinkedInputSigsMutex = new CMutex();

        public SerialFeedback()
        {
        }

        public SerialFeedback(string key)
            : base(key)
        {
        }

        public override void FireUpdate()
        {
            throw new NotImplementedException("This feedback type does not use Funcs");
        }

        public void FireUpdate(string newValue)
        {
            _SerialValue = newValue;
            StringInputSig[] inputSigs;

            LinkedInputSigsMutex.WaitForMutex();
            try
            {
                inputSigs = LinkedInputSigs.ToArray();
            }
            finally
            {
                LinkedInputSigsMutex.ReleaseMutex();
            }

            for (int i = 0; i < inputSigs.Length; i++)
                UpdateSig(inputSigs[i], newValue);

            OnOutputChange(newValue);
        }

        public void LinkInputSig(StringInputSig sig)
        {
            LinkedInputSigsMutex.WaitForMutex();
            try
            {
                LinkedInputSigs.Add(sig);
            }
            finally
            {
                LinkedInputSigsMutex.ReleaseMutex();
            }

            UpdateSig(sig);
        }

        public void UnlinkInputSig(StringInputSig sig)
        {
            LinkedInputSigsMutex.WaitForMutex();
            try
            {
                LinkedInputSigs.Remove(sig);
            }
            finally
            {
                LinkedInputSigsMutex.ReleaseMutex();
            }
        }

        public override string ToString()
        {
            return (InTestMode ? "TEST -- " : "") + SerialValue;
        }

        /// <summary>
        /// Puts this in test mode, sets the test value and fires an update.
        /// </summary>
        /// <param name="value"></param>
        public void SetTestValue(string value)
        {
            TestValue = value;
            InTestMode = true;
            FireUpdate(TestValue);
        }

        private void UpdateSig(StringInputSig sig)
        {
            sig.StringValue = _SerialValue;
        }

        private void UpdateSig(StringInputSig sig, string value)
        {
            sig.StringValue = value;
        }
    }
}
