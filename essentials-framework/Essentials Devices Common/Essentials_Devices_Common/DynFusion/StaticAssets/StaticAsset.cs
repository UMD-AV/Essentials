using Crestron.SimplSharpPro.Fusion;
using PepperDash.Essentials.Core;
using PepperDash.Core;

namespace DynFusion.Assets
{
    public class StaticAsset : EssentialsDevice
    {
        protected uint _assetNumber;
        protected FusionStaticAsset _asset;

        public StaticAsset(string friendlyName, string key, uint assetNumber, string type, FusionRoom symbol) :
            base(key)
        {
            Debug.Console(0, this, "Creating static asset {0} at number {1} of type {2}", Name, assetNumber, type);
            _assetNumber = assetNumber;
            symbol.AddAsset(eAssetType.StaticAsset, assetNumber, friendlyName, type, FusionUuid.GenerateUuid(key));
            _asset = ((FusionStaticAsset)symbol.UserConfigurableAssetDetails[_assetNumber].Asset);
        }

        public virtual void FusionAssetStateChange(FusionAssetStateEventArgs args)
        {
        }
    }
}