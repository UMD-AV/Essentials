using System.Collections.Generic;

namespace UmdEssentials.Core
{
    public interface IHasDspPresets
    {
        List<IDspPreset> Presets { get; }

        void RecallPreset(IDspPreset preset);
    }

    public interface IDspPreset
    {
        string Name { get; }
    }
}