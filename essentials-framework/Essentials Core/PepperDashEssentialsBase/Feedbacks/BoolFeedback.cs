using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;

namespace PepperDash.Essentials.Core
{
    /// <summary>
    /// A Feedback whose output is derived from the return value of a provided Func.
    /// </summary>
    public class BoolFeedback : Feedback
    {
        /// <summary>
        /// Returns the current value of the feedback, derived from the ValueFunc. The ValueFunc is 
        /// evaluated whenever FireUpdate() is called
        /// </summary>
        public override bool BoolValue
        {
            get { return _BoolValue; }
        }

        private bool _BoolValue;

        /// <summary>
        /// Fake value to be used in test mode
        /// </summary>
        public bool TestValue { get; private set; }

        /// <summary>
        /// Func that evaluates on FireUpdate
        /// </summary>
        public Func<bool> ValueFunc { get; private set; }

        private List<BoolInputSig> LinkedInputSigs = new List<BoolInputSig>();
        private List<BoolInputSig> LinkedComplementInputSigs = new List<BoolInputSig>();
        private CMutex LinkedInputSigsMutex = new CMutex();
        private CMutex LinkedComplementInputSigsMutex = new CMutex();

        private List<Crestron.SimplSharpPro.DeviceSupport.Feedback> LinkedCrestronFeedbacks =
            new List<Crestron.SimplSharpPro.DeviceSupport.Feedback>();

        /// <summary>
        /// Creates the feedback with the Func as described.
        /// </summary>
        /// <remarks>
        /// While the linked sig value will be updated with the current value stored when it is linked to a EISC Bridge,
        /// it will NOT reflect an actual value from a device until <seealso cref="FireUpdate"/> has been called
        /// </remarks>
        /// <param name="valueFunc">Delegate to invoke when this feedback needs to be updated</param>
        public BoolFeedback(Func<bool> valueFunc)
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
        public BoolFeedback(string key, Func<bool> valueFunc)
            : base(key)
        {
            ValueFunc = valueFunc;
        }

        public void SetValueFunc(Func<bool> newFunc)
        {
            ValueFunc = newFunc;
        }

        public override void FireUpdate()
        {
            bool newValue = InTestMode ? TestValue : ValueFunc.Invoke();
            if (newValue != _BoolValue)
            {
                _BoolValue = newValue;
                BoolInputSig[] inputSigs;
                BoolInputSig[] complementInputSigs;

                LinkedInputSigsMutex.WaitForMutex();
                try
                {
                    inputSigs = LinkedInputSigs.ToArray();
                }
                finally
                {
                    LinkedInputSigsMutex.ReleaseMutex();
                }

                LinkedComplementInputSigsMutex.WaitForMutex();
                try
                {
                    complementInputSigs = LinkedComplementInputSigs.ToArray();
                }
                finally
                {
                    LinkedComplementInputSigsMutex.ReleaseMutex();
                }

                for (int i = 0; i < inputSigs.Length; i++)
                    UpdateSig(inputSigs[i]);

                for (int i = 0; i < complementInputSigs.Length; i++)
                    UpdateComplementSig(complementInputSigs[i]);

                OnOutputChange(newValue);
            }
        }

        /// <summary>
        /// Links an input sig
        /// </summary>
        /// <param name="sig"></param>
        public void LinkInputSig(BoolInputSig sig)
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

        /// <summary>
        /// Unlinks an inputs sig
        /// </summary>
        /// <param name="sig"></param>
        public void UnlinkInputSig(BoolInputSig sig)
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

        /// <summary>
        /// Links an input sig to the complement value
        /// </summary>
        /// <param name="sig"></param>
        public void LinkComplementInputSig(BoolInputSig sig)
        {
            LinkedComplementInputSigsMutex.WaitForMutex();
            try
            {
                LinkedComplementInputSigs.Add(sig);
            }
            finally
            {
                LinkedComplementInputSigsMutex.ReleaseMutex();
            }

            UpdateComplementSig(sig);
        }

        /// <summary>
        /// Unlinks an input sig to the complement value
        /// </summary>
        /// <param name="sig"></param>
        public void UnlinkComplementInputSig(BoolInputSig sig)
        {
            LinkedComplementInputSigsMutex.WaitForMutex();
            try
            {
                LinkedComplementInputSigs.Remove(sig);
            }
            finally
            {
                LinkedComplementInputSigsMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Links a Crestron Feedback object
        /// </summary>
        /// <param name="feedback"></param>
        public void LinkCrestronFeedback(Crestron.SimplSharpPro.DeviceSupport.Feedback feedback)
        {
            LinkedCrestronFeedbacks.Add(feedback);
            UpdateCrestronFeedback(feedback);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="feedback"></param>
        public void UnlinkCrestronFeedback(Crestron.SimplSharpPro.DeviceSupport.Feedback feedback)
        {
            LinkedCrestronFeedbacks.Remove(feedback);
        }

        public override string ToString()
        {
            return (InTestMode ? "TEST -- " : "") + BoolValue;
        }

        /// <summary>
        /// Puts this in test mode, sets the test value and fires an update.
        /// </summary>
        /// <param name="value"></param>
        public void SetTestValue(bool value)
        {
            TestValue = value;
            InTestMode = true;
            FireUpdate();
        }

        private void UpdateSig(BoolInputSig sig)
        {
            sig.BoolValue = _BoolValue;
        }

        private void UpdateComplementSig(BoolInputSig sig)
        {
            sig.BoolValue = !_BoolValue;
        }

        private void UpdateCrestronFeedback(Crestron.SimplSharpPro.DeviceSupport.Feedback feedback)
        {
            feedback.State = _BoolValue;
        }
    }
}
