using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.Fusion;
using DynFusion.Assets;
using PepperDash.Core;
using PepperDash.Essentials.Devices.Common.Microphones;

namespace PepperDash.Essentials.Devices.Common.DynFusion.StaticAssets
{
    public class MicBatteryStaticAsset : StaticAsset
    {
        public MicBatteryStaticAsset(string name, WirelessMic battery, uint assetNumber, FusionRoom symbol) :
            base(name, name + "-Asset", assetNumber, "Mic Battery", symbol)
        {
            _asset.AssetUsage.AddSigToRVIFile = false;
            _asset.PowerOn.AddSigToRVIFile = false;
            _asset.PowerOff.AddSigToRVIFile = false;
            _asset.AssetError.AddSigToRVIFile = true;
            _asset.Connected.AddSigToRVIFile = true;

            _asset.Connected.InputSig.BoolValue = true;
            battery.ErrorStringFeedback.LinkInputSig(_asset.AssetError.InputSig);

            _asset.ParamMake.Value = "Shure";
            _asset.ParamModel.Value = "Battery";

            //Battery Present
            _asset.AddSig(eSigType.Bool, 1, "Mic Battery - Present", eSigIoMask.InputSigOnly);
            battery.OnDockFeedback.LinkInputSig(_asset.FusionGenericAssetDigitalsAsset1.BooleanInput[50]);

            //Battery Error Int
            _asset.AddSig(eSigType.UShort, 1, "Mic Battery - Error", eSigIoMask.InputSigOnly);
            battery.BatteryErrorAnalogFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[50]);

            //Battery % Health
            _asset.AddSig(eSigType.UShort, 2, "Mic Battery - % Health", eSigIoMask.InputSigOnly);
            battery.PercentHealthFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[51]);

            //Battery Temp
            _asset.AddSig(eSigType.UShort, 3, "Mic Battery - Temp F", eSigIoMask.InputSigOnly);
            battery.TemperatureFFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[52]);

            //Battery % Charge
            _asset.AddSig(eSigType.UShort, 4, "Mic Battery - % Charge", eSigIoMask.InputSigOnly);
            battery.PercentChargeFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[53]);

            //Battery State
            _asset.AddSig(eSigType.String, 1, "Mic Battery - State", eSigIoMask.InputSigOnly);
            battery.StateFeedback.LinkInputSig(_asset.FusionGenericAssetSerialsAsset3.StringInput[50]);

            //Battery Error String
            _asset.AddSig(eSigType.String, 2, "Mic Battery - Error Text", eSigIoMask.InputSigOnly);
            battery.ErrorStringFeedback.LinkInputSig(_asset.FusionGenericAssetSerialsAsset3.StringInput[51]);
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