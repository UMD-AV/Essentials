namespace PepperDash.Essentials.Core.Routing
{
    public class DestFeedback
    {
        public readonly string RouteKey;
        public readonly ushort? Input;
        public readonly ushort Output;
        public ushort? FeedbackInput { get; set; }
        public readonly bool? DisableInOverflow;
        public readonly bool? EnableInOverflow;

        public DestFeedback(string routeKey, ushort output, ushort? input, bool? disableInOverflow,
            bool? enableInOverflow)
        {
            RouteKey = routeKey;
            Output = output;
            Input = input;
            DisableInOverflow = disableInOverflow;
            EnableInOverflow = enableInOverflow;
        }
    }

    public class SourceFeedback
    {
        public readonly string RouteKey;
        public readonly ushort Input;
        public readonly ushort? Output;
        public bool? FeedbackState { get; set; }

        public SourceFeedback(string routeKey, ushort output, ushort input)
        {
            RouteKey = routeKey;
            Output = output;
            Input = input;
        }
    }
}