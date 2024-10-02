using PepperDash.Core;


namespace Tesira_DSP_EPI.Extensions
{
    public static class ScalingExtensions
    {
        public static double Scale(this double input, double inMin, double inMax, double outMin, double outMax,
            IKeyed parent)
        {
            double inputRange = inMax - inMin;

            if (inputRange <= 0)
            {
                Debug.Console(0, parent, Debug.ErrorLogLevel.Notice,
                    "Invalid Input Range '{0}' for Scaling.  Min '{1}' Max '{2}'.", inputRange, inMin, inMax);
                return input;
            }

            double outputRange = outMax - outMin;

            double output = (((input - inMin) * outputRange) / inputRange) + outMin;

            return output;
        }
    }
}