using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.Fusion;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Core;

namespace DynFusion.Assets
{
	public class MicrophoneStaticAsset : StaticAsset
	{
        private DisplayBase _device;

		public MicrophoneStaticAsset(DisplayBase device, uint assetNumber, FusionRoom symbol):
            base(device.Name, device.Key + "-Asset", assetNumber, "Microphone", symbol)
		{
            _device = device;
            _asset.AssetUsage.AddSigToRVIFile = false;
            _asset.PowerOn.AddSigToRVIFile = false;
            _asset.PowerOff.AddSigToRVIFile = false;

            var commMonitor = _device as ICommunicationMonitor;
            if (commMonitor != null)
            {
                _asset.Connected.AddSigToRVIFile = true;
                commMonitor.CommunicationMonitor.IsOnlineFeedback.LinkInputSig(_asset.Connected.InputSig);
            }
            else
            {
                _asset.Connected.AddSigToRVIFile = false;
            }

            var errorDevice = _device as IHasErrorString;
            if (errorDevice != null)
            {
                _asset.AssetError.AddSigToRVIFile = true;
                errorDevice.ErrorFeedback.LinkInputSig(_asset.AssetError.InputSig);
            }
            else
            {
                _asset.AssetError.AddSigToRVIFile = false;
            }
		}

        public override void FusionAssetStateChange(FusionAssetStateEventArgs args)
        {
            if (args.UserConfigurableAssetDetailIndex != _assetNumber)
            {
                return;
            }

            Debug.Console(1, this, "Display static asset state change {0} recieved EventID {1} Index {2}", Name, args.EventId, args.UserConfigurableAssetDetailIndex);
            switch (args.EventId)
            {
                case FusionAssetEventId.StaticAssetAssetBoolAssetSigEventReceivedEventId:
                    {
                        var sigDetails = args.UserConfiguredSigDetail as BooleanSigData;
                        if (sigDetails != null)
                        {
                            Debug.Console(1, this, string.Format("StaticAsset: {0} Bool Change Join:{1} Name:{2} Value:{3}",
                                _asset.ParamAssetName, sigDetails.Number, sigDetails.Name, sigDetails.OutputSig.BoolValue));
                        }
                        break;
                    }
                case FusionAssetEventId.StaticAssetAssetUshortAssetSigEventReceivedEventId:
                    {
                        var sigDetails = args.UserConfiguredSigDetail as UShortSigData;
                        if (sigDetails != null)
                        {
                            Debug.Console(1,this,  string.Format("StaticAsset: {0} UShort Change Join:{1} Name:{2} Value:{3}",
                                _asset.ParamAssetName, sigDetails.Number, sigDetails.Name, sigDetails.OutputSig.UShortValue));
                        }
                        break;
                    }
                case FusionAssetEventId.StaticAssetAssetStringAssetSigEventReceivedEventId:
                    {
                        var sigDetails = args.UserConfiguredSigDetail as StringSigData;
                        if (sigDetails != null)
                        {
                            Debug.Console(1, this, string.Format("StaticAsset: {0} String Change Join:{1} Name:{2} Value:{3}",
                                _asset.ParamAssetName, sigDetails.Number, sigDetails.Name, sigDetails.OutputSig.StringValue));
                        }
                        break;
                    }
            }
        }
    }
}

