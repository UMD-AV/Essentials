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
using PepperDash.Core;

namespace DynFusion.Assets
{
	public class DisplayStaticAsset : StaticAsset
	{
        private DisplayBase _device;

		public DisplayStaticAsset(DisplayBase device, uint assetNumber, FusionRoom symbol):
            base(device.Name, device.Key + "-Asset", assetNumber, "Display", symbol)
		{
            _device = device;

            _asset.AssetUsage.AddSigToRVIFile = false;
            _asset.PowerOn.AddSigToRVIFile = true;
            _asset.PowerOff.AddSigToRVIFile = true;

            var epson = _device as PepperDash.Essentials.Devices.Displays.EpsonProjector;
            if (epson != null)
            {
                _asset.ParamMake.Value = "Epson";
                _asset.ParamModel.Value = "Projector";
            }

            _asset.Connected.AddSigToRVIFile = true;
            _asset.Connected.InputSig.BoolValue = true;

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

            var displayBase = _device as TwoWayDisplayBase;
            if (displayBase != null)
            {
                displayBase.PowerIsOnFeedback.LinkInputSig(_asset.PowerOn.InputSig);
            }

            var lampHours = _device as IHasLampHours;
            if (lampHours != null)
            {
                uint joinNumber = 1;
                _asset.AddSig(eSigType.UShort, joinNumber, "Display - Lamp Hours", eSigIoMask.InputSigOnly);
                lampHours.LampHoursFeedback.LinkInputSig(_asset.FusionGenericAssetAnalogsAsset2.UShortInput[50]);
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
                case FusionAssetEventId.StaticAssetPowerOffReceivedEventId:
                    {
                        _device.PowerOff();
                        break;
                    }
                case FusionAssetEventId.StaticAssetPowerOnReceivedEventId:
                    {
                        _device.PowerOn();
                        break;
                    }
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

