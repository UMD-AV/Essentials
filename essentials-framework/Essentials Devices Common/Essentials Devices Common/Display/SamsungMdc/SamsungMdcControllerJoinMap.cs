using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace PepperDash.Plugin.Display.SamsungMdc
{
	public class SamsungDisplayControllerJoinMap : DisplayControllerJoinMap
	{
        [JoinName("Warming")]
        public JoinDataComplete Warming = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 53,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Label = "Warming"
            });

        [JoinName("Cooling")]
        public JoinDataComplete Cooling = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 54,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Label = "Cooling"
            });

	    /// <summary>
	    /// Analog join to report LED product monitor temperature feedback
	    /// </summary>
	    [JoinName("LedTemperatureCelsius")] public JoinDataComplete LedTemperatureCelsius =
	        new JoinDataComplete(new JoinData {JoinNumber = 21, JoinSpan = 1},
	            new JoinMetadata
	            {
	                Description = "Display Temp Celsius",
	                JoinCapabilities = eJoinCapabilities.ToSIMPL,
	                JoinType = eJoinType.Analog
	            });
		
		/// <summary>
		/// Analog join to report LED product monitor temperature feedback
		/// </summary>
        /// <summary>
        /// Analog join to report LED product monitor temperature feedback
        /// </summary>
        [JoinName("LedTemperatureFahrenheit")]
        public JoinDataComplete LedTemperatureFahrenheit =
            new JoinDataComplete(new JoinData { JoinNumber = 22, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Display Temp Fahrenheit",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Analog join to report default volume setting in config
        /// </summary>
        [JoinName("DefaultVolumeFb")]
        public JoinDataComplete DefaultVolumeFb =
            new JoinDataComplete(new JoinData { JoinNumber = 6, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Default Volume Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("Status")]
        public JoinDataComplete Status =
            new JoinDataComplete(new JoinData { JoinNumber = 50, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Display Temp Celsius",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

		/// <summary>
		/// High when volume controls are enabled
		/// </summary>
		[JoinName("VolumeControlsVisibleFb")]
		public JoinDataComplete VolumeControlsVisibleFb =
			new JoinDataComplete(new JoinData { JoinNumber = 40, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "Enable Visibility of Volume Controls",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Digital
			});


		/// <summary>
		/// Display controller join map
		/// Some specific adds for Samsung Temperature and Brightness control and feedback
		/// </summary>
		public SamsungDisplayControllerJoinMap(uint joinStart) : base(joinStart, typeof(SamsungDisplayControllerJoinMap))
		{
        }
	}
}