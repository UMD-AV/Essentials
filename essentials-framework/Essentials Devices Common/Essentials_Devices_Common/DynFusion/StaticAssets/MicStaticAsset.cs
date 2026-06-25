using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.Fusion;
using DynFusion.Assets;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common.Microphones;

namespace PepperDash.Essentials.Devices.Common.DynFusion.StaticAssets
{
    public class MicStaticAsset : StaticAsset
    {
        public MicStaticAsset(string name, WirelessMic mic, uint assetNumber, FusionRoom symbol) :
            base(name, name + "-Asset", assetNumber, "Microphone", symbol)
        {
            _asset.AssetUsage.AddSigToRVIFile = false;
            _asset.PowerOn.AddSigToRVIFile = false;
            _asset.PowerOff.AddSigToRVIFile = false;
            _asset.AssetError.AddSigToRVIFile = false;
            _asset.Connected.AddSigToRVIFile = true;

            _asset.Connected.InputSig.BoolValue = true;

            _asset.ParamMake.Value = "Shure";

            mic.ModelFeedback.OutputChange += ModelFeedback_OutputChange;

            //Microphone Present
            _asset.AddSig(eSigType.Bool, 1, "Microphone - Present", eSigIoMask.InputSigOnly);
            mic.MicrophonePresentFeedback.LinkInputSig(_asset.FusionGenericAssetDigitalsAsset1.BooleanInput[50]);

            //Microphone Runtime Minutes
            _asset.AddSig(eSigType.UShort, 1, "Microphone - Runtime Minutes", eSigIoMask.InputSigOnly);
            mic.RuntimeFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[50]);

            //Battery % Health
            _asset.AddSig(eSigType.UShort, 2, "Microphone - Battery % Health", eSigIoMask.InputSigOnly);
            mic.PercentHealthFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[51]);

            //Battery Temp
            _asset.AddSig(eSigType.UShort, 3, "Microphone - Battery Temp F", eSigIoMask.InputSigOnly);
            mic.TemperatureFFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[52]);

            //Battery % Charge
            _asset.AddSig(eSigType.UShort, 4, "Microphone - Battery % Charge", eSigIoMask.InputSigOnly);
            mic.PercentChargeFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[53]);
        }

        public void ModelFeedback_OutputChange(object o, FeedbackEventArgs args)
        {
            _asset.ParamModel.Value = args.StringValue;
        }

        public override void FusionAssetStateChange(FusionAssetStateEventArgs args)
        {
            if (args.UserConfigurableAssetDetailIndex != _assetNumber) return;

            Debug.Console(1, this, "Microphone static asset state change {0} received EventID {1} Index {2}", Name,
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