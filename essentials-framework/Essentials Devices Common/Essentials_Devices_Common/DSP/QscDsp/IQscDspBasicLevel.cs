using UmdEssentials.Core;

namespace UmdEssentials.Devices.Common.DSP.QscDsp
{
    /// <summary>
    /// DSP Basic Level Interface
    /// QSC: NamedControls
    /// Biamp: InstanceTags
    /// Polycom: 
    /// </summary>
    public interface IQscDspBasicLevel : IBasicVolumeWithFeedback
    {
        string LevelInstanceTag { get; set; }
        string MuteInstanceTag { get; set; }
        bool HasMute { get; }
        bool HasLevel { get; }
        bool AutomaticUnmuteOnVolumeUp { get; }
    }
}