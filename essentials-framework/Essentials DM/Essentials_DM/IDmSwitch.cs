using System.Collections.Generic;
using Crestron.SimplSharpPro.DM;
//using PepperDash.Essentials.DM.Cards;

namespace PepperDash.Essentials.DM {
    public interface IDmSwitch {
        Switch Chassis { get; }

        Dictionary<uint, string> TxDictionary { get; }
        Dictionary<uint, string> RxDictionary { get; }
    }
}