using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.Fusion;
using DynFusion.Assets;
using PepperDash.Core;
using UmdEssentials.Devices.Common.Microphones;

namespace UmdEssentials.Devices.Common.DynFusion.StaticAssets
{
    public class ShureMxaStaticAsset : StaticAsset
    {
        public ShureMxaStaticAsset(string name, ShureMxaDevice shureMic, uint assetNumber, FusionRoom symbol) :
            base(name, name + "-Asset", assetNumber, "Shure Mic", symbol)
        {
            _asset.AssetUsage.AddSigToRVIFile = false;
            _asset.PowerOn.AddSigToRVIFile = false;
            _asset.PowerOff.AddSigToRVIFile = false;
            _asset.AssetError.AddSigToRVIFile = true;
            _asset.Connected.AddSigToRVIFile = true;

            _asset.Connected.InputSig.BoolValue = true;

            _asset.ParamMake.Value = "Shure";
            _asset.ParamModel.Value = "Mic";

            //Mic in use
            _asset.AddSig(eSigType.Bool, 2, "Mic - In Use", eSigIoMask.InputSigOnly);
            shureMic.MicrophoneInUseFeedback.LinkInputSig(_asset.FusionGenericAssetDigitalsAsset1.BooleanInput[51]);
        }

        public override void FusionAssetStateChange(FusionAssetStateEventArgs args)
        {
            if (args.UserConfigurableAssetDetailIndex != _assetNumber) return;

            Debug.Console(1, this, "Mic battery static asset state change {0} received EventID {1} Index {2}", Name,
                args.EventId, args.UserConfigurableAssetDetailIndex);
            switch (args.EventId)
            {
                case FusionAssetEventId.StaticAssetAssetBoolAssetSigEventReceivedEventId:
                {
                    BooleanSigData sigDetails = args.UserConfiguredSigDetail as BooleanSigData;
                    if (sigDetails != null)
                        Debug.Console(1, this, string.Format("StaticAsset: {0} Bool Change Join:{1} Name:{2} Value:{3}",
                            _asset.ParamAssetName, sigDetails.Number, sigDetails.Name, sigDetails.OutputSig.BoolValue));

                    break;
                }
                case FusionAssetEventId.StaticAssetAssetUshortAssetSigEventReceivedEventId:
                {
                    UShortSigData sigDetails = args.UserConfiguredSigDetail as UShortSigData;
                    if (sigDetails != null)
                        Debug.Console(1, this, string.Format(
                            "StaticAsset: {0} UShort Change Join:{1} Name:{2} Value:{3}",
                            _asset.ParamAssetName, sigDetails.Number, sigDetails.Name,
                            sigDetails.OutputSig.UShortValue));

                    break;
                }
                case FusionAssetEventId.StaticAssetAssetStringAssetSigEventReceivedEventId:
                {
                    StringSigData sigDetails = args.UserConfiguredSigDetail as StringSigData;
                    if (sigDetails != null)
                        Debug.Console(1, this, string.Format(
                            "StaticAsset: {0} String Change Join:{1} Name:{2} Value:{3}",
                            _asset.ParamAssetName, sigDetails.Number, sigDetails.Name,
                            sigDetails.OutputSig.StringValue));

                    break;
                }
            }
        }
    }
}