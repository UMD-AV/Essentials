using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;

namespace PepperDash.Essentials.Core
{
    public interface IHasCresnetBranches
    {
        CrestronCollection<CresnetBranch> CresnetBranches { get; }
    }
}