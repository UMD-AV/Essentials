using Crestron.SimplSharpPro.DM;
using Crestron.SimplSharpPro.DM.Endpoints;

namespace PepperDash.Essentials.DM
{
	public static class VideoAttributesBasicExtensions
	{
		public static string GetVideoResolutionString(this VideoAttributesBasic va)
		{
			ushort h = va.HorizontalResolutionFeedback.UShortValue;
			ushort v = va.VerticalResolutionFeedback.UShortValue;
            ushort r = va.FramesPerSecondFeedback.UShortValue;
			if (h == 0 || v == 0)
				return "n/a";
			else
				return string.Format("{0}x{1}@{2}Hz", h, v, r);
		}
	}

    public static class AdvEndpointHdmiOutputExtensions
    {
        public static string GetVideoResolutionString(this AdvEndpointHdmiOutput va)
        {
            ushort h = va.HorizontalResolutionFeedback.UShortValue;
            ushort v = va.VerticalResolutionFeedback.UShortValue;
            ushort r = va.FramesPerSecondFeedback.UShortValue;
            if (h == 0 || v == 0)
                return "n/a";
            else
                return string.Format("{0}x{1}@{2}Hz", h, v, r);
        }
    }
}