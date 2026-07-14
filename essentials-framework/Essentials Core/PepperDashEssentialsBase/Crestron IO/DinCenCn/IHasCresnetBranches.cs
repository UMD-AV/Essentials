using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;

namespace UmdEssentials.Core
{
    public interface IHasCresnetBranches
    {
        CrestronCollection<CresnetBranch> CresnetBranches { get; }
    }
}