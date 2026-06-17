using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;

namespace PepperDash.Essentials.Core
{
    public class IntFeedback : Feedback
    {
        public override int IntValue
        {
            get { return _IntValue; }
        } // ValueFunc.Invoke(); } }

        private int _IntValue;

        public ushort UShortValue
        {
            get { return (ushort)_IntValue; }
        }

        //public override eCueType Type { get { return eCueType.Int; } }

        public int TestValue { get; private set; }

        /// <summary>
        /// Func evaluated on FireUpdate
        /// </summary>
        private Func<int> ValueFunc;

        private List<UShortInputSig> LinkedInputSigs = new List<UShortInputSig>();
        private CMutex LinkedInputSigsMutex = new CMutex();

        /// <summary>
        /// Creates the feedback with the Func as described.
        /// </summary>
        /// <remarks>
        /// While the linked sig value will be updated with the current value stored when it is linked to a EISC Bridge,
        /// it will NOT reflect an actual value from a device until <seealso cref="FireUpdate"/> has been called
        /// </remarks>
        /// <param name="valueFunc">Delegate to invoke when this feedback needs to be updated</param>
        public IntFeedback(Func<int> valueFunc)
            : this(null, valueFunc)
        {
        }

        /// <summary>
        /// Creates the feedback with the Func as described.
        /// </summary>
        /// <remarks>
        /// While the linked sig value will be updated with the current value stored when it is linked to a EISC Bridge,
        /// it will NOT reflect an actual value from a device until <seealso cref="FireUpdate"/> has been called
        /// </remarks>
        /// <param name="key">Key to find this Feedback</param>
        /// <param name="valueFunc">Delegate to invoke when this feedback needs to be updated</param>
        public IntFeedback(string key, Func<int> valueFunc)
            : base(key)
        {
            ValueFunc = valueFunc;
        }

        public void SetValueFunc(Func<int> newFunc)
        {
            ValueFunc = newFunc;
        }


        public override void FireUpdate()
        {
            int newValue = InTestMode ? TestValue : ValueFunc.Invoke();
            if (newValue != _IntValue)
            {
                _IntValue = newValue;
                UShortInputSig[] inputSigs;

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
                    UpdateSig(inputSigs[i]);

                OnOutputChange(newValue);
            }
        }

        public void LinkInputSig(UShortInputSig sig)
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

        public void UnlinkInputSig(UShortInputSig sig)
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
            return (InTestMode ? "TEST -- " : "") + IntValue;
        }

        /// <summary>
        /// Puts this in test mode, sets the test value and fires an update.
        /// </summary>
        /// <param name="value"></param>
        public void SetTestValue(int value)
        {
            TestValue = value;
            InTestMode = true;
            FireUpdate();
        }

        private void UpdateSig(UShortInputSig sig)
        {
            sig.UShortValue = UShortValue;
        }
    }
}
