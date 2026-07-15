using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.Fusion;
using DynFusion.Assets;
using PepperDash.Core;
using UmdEssentials.Devices.Common.Microphones;

namespace UmdEssentials.Devices.Common.DynFusion.StaticAssets
{
    public class WirelessMicStaticAsset : StaticAsset
    {
        public WirelessMicStaticAsset(string name, WirelessMic wirelessMic, uint assetNumber, FusionRoom symbol) :
            base(name, name + "-Asset", assetNumber, "Wireless Mic", symbol)
        {
            _asset.AssetUsage.AddSigToRVIFile = false;
            _asset.PowerOn.AddSigToRVIFile = false;
            _asset.PowerOff.AddSigToRVIFile = false;
            _asset.AssetError.AddSigToRVIFile = true;
            _asset.Connected.AddSigToRVIFile = true;

            _asset.Connected.InputSig.BoolValue = true;
            wirelessMic.ErrorStringFeedback.LinkInputSig(_asset.AssetError.InputSig);

            _asset.ParamMake.Value = wirelessMic.Model;
            _asset.ParamModel.Value = "Mic";

            //Battery On Dock
            _asset.AddSig(eSigType.Bool, 1, "Mic - On Dock", eSigIoMask.InputSigOnly);
            wirelessMic.OnDockFeedback.LinkInputSig(_asset.FusionGenericAssetDigitalsAsset1.BooleanInput[50]);

            //Mic in use
            _asset.AddSig(eSigType.Bool, 2, "Mic - In Use", eSigIoMask.InputSigOnly);
            wirelessMic.MicrophoneInUseFeedback.LinkInputSig(_asset.FusionGenericAssetDigitalsAsset1.BooleanInput[51]);

            //Battery Error Int
            _asset.AddSig(eSigType.UShort, 1, "Mic - Error", eSigIoMask.InputSigOnly);
            wirelessMic.BatteryErrorAnalogFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[50]);

            //Battery % Health
            _asset.AddSig(eSigType.UShort, 2, "Mic - % Health", eSigIoMask.InputSigOnly);
            wirelessMic.PercentHealthFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[51]);

            //Battery Temp
            _asset.AddSig(eSigType.UShort, 3, "Mic - Temp F", eSigIoMask.InputSigOnly);
            wirelessMic.TemperatureFFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[52]);

            //Battery % Charge
            _asset.AddSig(eSigType.UShort, 4, "Mic - % Charge", eSigIoMask.InputSigOnly);
            wirelessMic.PercentChargeFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[53]);

            //Battery State
            _asset.AddSig(eSigType.String, 1, "Mic - State", eSigIoMask.InputSigOnly);
            wirelessMic.StateFeedback.LinkInputSig(_asset.FusionGenericAssetSerialsAsset3.StringInput[50]);

            //Battery Error String
            _asset.AddSig(eSigType.String, 2, "Mic - Error Text", eSigIoMask.InputSigOnly);
            wirelessMic.ErrorStringFeedback.LinkInputSig(_asset.FusionGenericAssetSerialsAsset3.StringInput[51]);
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