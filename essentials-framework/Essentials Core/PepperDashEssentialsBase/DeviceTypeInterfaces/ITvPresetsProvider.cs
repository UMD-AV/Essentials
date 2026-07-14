using UmdEssentials.Core.Presets;

namespace UmdEssentials.Core.DeviceTypeInterfaces
{
    public interface ITvPresetsProvider
    {
        DevicePresetsModel TvPresets { get; }
    }
}