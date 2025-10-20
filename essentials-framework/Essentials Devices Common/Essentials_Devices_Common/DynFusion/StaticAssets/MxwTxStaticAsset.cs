using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.Fusion;
using PepperDash.Core;
using PepperDash.Essentials.Devices.Common.ShureMxwapxd2;

namespace DynFusion.Assets
{
    public class MxwTxStaticAsset : StaticAsset
    {
        public MxwTxStaticAsset(string name, ShureMxwTx tx, uint assetNumber, FusionRoom symbol) :
            base(name, name + "-Asset", assetNumber, "Mxw Tx", symbol)
        {
            _asset.AssetUsage.AddSigToRVIFile = false;
            _asset.PowerOn.AddSigToRVIFile = false;
            _asset.PowerOff.AddSigToRVIFile = false;
            _asset.AssetError.AddSigToRVIFile = false;
            _asset.Connected.AddSigToRVIFile = true;

            _asset.Connected.InputSig.BoolValue = true;

            _asset.ParamMake.Value = "Shure";
            _asset.ParamModel.Value = "Mxw Tx";

            //Battery Present
            _asset.AddSig(eSigType.Bool, 1, "Mic Battery - Present", eSigIoMask.InputSigOnly);
            tx.TxPresentFeedback.LinkInputSig(_asset.FusionGenericAssetDigitalsAsset1.BooleanInput[50]);

            //Battery % Health
            _asset.AddSig(eSigType.UShort, 2, "Mic Battery - % Health", eSigIoMask.InputSigOnly);
            tx.PercentHealthFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[51]);

            //Battery % Charge
            _asset.AddSig(eSigType.UShort, 4, "Mic Battery - % Charge", eSigIoMask.InputSigOnly);
            tx.PercentChargeFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[53]);
        }

        public override void FusionAssetStateChange(FusionAssetStateEventArgs args)
        {
            if (args.UserConfigurableAssetDetailIndex != _assetNumber)
            {
                return;
            }

            Debug.Console(1, this, "Mxw Tx battery static asset state change {0} received EventID {1} Index {2}", Name,
                args.EventId, args.UserConfigurableAssetDetailIndex);
            switch (args.EventId)
            {
                case FusionAssetEventId.StaticAssetAssetBoolAssetSigEventReceivedEventId:
                {
                    BooleanSigData sigDetails = args.UserConfiguredSigDetail as BooleanSigData;
                    if (sigDetails != null)
                    {
                        Debug.Console(1, this, string.Format("StaticAsset: {0} Bool Change Join:{1} Name:{2} Value:{3}",
                            _asset.ParamAssetName, sigDetails.Number, sigDetails.Name, sigDetails.OutputSig.BoolValue));
                    }

                    break;
                }
                case FusionAssetEventId.StaticAssetAssetUshortAssetSigEventReceivedEventId:
                {
                    UShortSigData sigDetails = args.UserConfiguredSigDetail as UShortSigData;
                    if (sigDetails != null)
                    {
                        Debug.Console(1, this, string.Format(
                            "StaticAsset: {0} UShort Change Join:{1} Name:{2} Value:{3}",
                            _asset.ParamAssetName, sigDetails.Number, sigDetails.Name,
                            sigDetails.OutputSig.UShortValue));
                    }

                    break;
                }
                case FusionAssetEventId.StaticAssetAssetStringAssetSigEventReceivedEventId:
                {
                    StringSigData sigDetails = args.UserConfiguredSigDetail as StringSigData;
                    if (sigDetails != null)
                    {
                        Debug.Console(1, this, string.Format(
                            "StaticAsset: {0} String Change Join:{1} Name:{2} Value:{3}",
                            _asset.ParamAssetName, sigDetails.Number, sigDetails.Name,
                            sigDetails.OutputSig.StringValue));
                    }

                    break;
                }
            }
        }
    }
}