using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace QscQsysDspPlugin
{
    /// <summary>
    /// QSC DSP Camera class
    /// </summary>
    public class QscDspCamera : EssentialsBridgeableDevice, IOnline
    {
        private QscDsp _Dsp;
        public QscDspCameraConfig Config { get; private set; }
        private string LastCmd;
        private bool _Online;

        public bool Online
        {
            set
            {
                this._Online = value;
                IsOnline.FireUpdate();
            }
            get { return this._Online; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dsp">QscDsp</param>
        /// <param name="key">string</param>
        /// <param name="name">string</param>
        /// <param name="dc">QscDspCameraConfig</param>
        public QscDspCamera(QscDsp dsp, string key, string name, QscDspCameraConfig dc)
            : base(key, name)
        {
            _Dsp = dsp;
            Config = dc;
            IsOnline = new BoolFeedback(() => Online);
            DeviceManager.AddDevice(this);
        }

        /// <summary>
        /// Moves a camera 
        /// </summary>
        /// <param name="button">eCameraPtzControls</param>
        public void MoveCamera(eCameraPtzControls button)
        {
            string tag = null;

            switch (button)
            {
                case eCameraPtzControls.Stop:
                {
                    string cmdToSend = string.Format("csv \"{0}\" 0", LastCmd);
                    _Dsp.SendLine(cmdToSend);
                    break;
                }
                case eCameraPtzControls.PanLeft:
                    tag = Config.PanLeftTag;
                    break;
                case eCameraPtzControls.PanRight:
                    tag = Config.PanRightTag;
                    break;
                case eCameraPtzControls.TiltUp:
                    tag = Config.TiltUpTag;
                    break;
                case eCameraPtzControls.TiltDown:
                    tag = Config.TiltDownTag;
                    break;
                case eCameraPtzControls.ZoomIn:
                    tag = Config.ZoomInTag;
                    break;
                case eCameraPtzControls.ZoomOut:
                    tag = Config.ZoomOutTag;
                    break;
            }

            if (tag != null)
            {
                string cmdToSend = string.Format("csv \"{0}\" 1", tag);
                LastCmd = tag;
                _Dsp.SendLine(cmdToSend);
            }
        }

        /// <summary>
        /// Camera privacy on
        /// </summary>
        public void PrivacyOn()
        {
            string cmdToSend = string.Format("csv \"{0}\" 1", Config.Privacy);
            _Dsp.SendLine(cmdToSend);
        }

        /// <summary>
        /// Camera privacy off
        /// </summary>
        public void PrivacyOff()
        {
            string cmdToSend = string.Format("csv \"{0}\" 0", Config.Privacy);
            _Dsp.SendLine(cmdToSend);
        }

        /// <summary>
        /// Recalls a preset with the provided number
        /// </summary>
        /// <param name="presetNumber">ushort</param>
        public void RecallPreset(ushort presetNumber)
        {
            Debug.Console(2, this, "Recall Camera Preset {0}", presetNumber);
            if (Config.Presets.ElementAt(presetNumber).Value != null)
            {
                QscDspPresets preset = Config.Presets.ElementAt(presetNumber).Value;
                string cmdToSend = string.Format("ssl {0} {1} 0", preset.Bank, preset.Number);
                _Dsp.SendLine(cmdToSend);
            }
        }

        /// <summary>
        /// Saves a preset with the provided number
        /// </summary>
        /// <param name="presetNumber">ushort</param>
        public void SavePreset(ushort presetNumber)
        {
            if (Config.Presets.ElementAt(presetNumber).Value != null)
            {
                QscDspPresets preset = Config.Presets.ElementAt(presetNumber).Value;
                string cmdToSend = string.Format("sss {0} {1}", preset.Bank, preset.Number);
                _Dsp.SendLine(cmdToSend);
            }
        }

        /// <summary>
        /// Adds the command to the change group
        /// </summary>
        public void Subscribe()
        {
            try
            {
                // Do subscriptions and blah blah
                if (Config.OnlineStatus != null)
                {
                    string cmd = string.Format("cga 1 \"{0}\"", Config.OnlineStatus);
                    _Dsp.SendLine(cmd);
                }
            }
            catch (Exception e)
            {
                Debug.Console(2, "QscDspCamera Subscription Error: '{0}'\n", e);
            }
        }

        /// <summary>
        /// Parses the change group subscription message
        /// </summary>
        /// <param name="customName"></param>
        /// <param name="value"></param>
        /// <param name="absoluteValue"></param>
        public void ParseSubscriptionMessage(string customName, string value, string absoluteValue)
        {
            // Check for valid subscription response
            Debug.Console(1, this, "CameraOnline {0} Response: '{1}'", customName, value);

            if (value == "true")
            {
                Online = true;
            }
            else if (value == "false")
            {
                Online = false;
            }
        }

        #region IBridge Members

        /// <summary>
        /// Link to API
        /// </summary>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            QscDspCameraDeviceJoinMap joinMap = new QscDspCameraDeviceJoinMap(joinStart);

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            // from Plugin > to SiMPL
            this.IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.Online.JoinNumber]);

            // from SiMPL > to Plugin
            // ternary: camera.MoveCamera(bool ? [bool == true, method to execute] : [bool == false, method to execute])
            trilist.SetBoolSigAction(joinMap.Up.JoinNumber,
                (b) => this.MoveCamera(b ? eCameraPtzControls.TiltUp : eCameraPtzControls.Stop));
            trilist.SetBoolSigAction(joinMap.Down.JoinNumber,
                (b) => this.MoveCamera(b ? eCameraPtzControls.TiltDown : eCameraPtzControls.Stop));
            trilist.SetBoolSigAction(joinMap.Left.JoinNumber,
                (b) => this.MoveCamera(b ? eCameraPtzControls.PanLeft : eCameraPtzControls.Stop));
            trilist.SetBoolSigAction(joinMap.Right.JoinNumber,
                (b) => this.MoveCamera(b ? eCameraPtzControls.PanRight : eCameraPtzControls.Stop));
            trilist.SetBoolSigAction(joinMap.ZoomIn.JoinNumber,
                (b) => this.MoveCamera(b ? eCameraPtzControls.ZoomIn : eCameraPtzControls.Stop));
            trilist.SetBoolSigAction(joinMap.ZoomOut.JoinNumber,
                (b) => this.MoveCamera(b ? eCameraPtzControls.ZoomOut : eCameraPtzControls.Stop));

            ushort x = 0;
            foreach (KeyValuePair<string, QscDspPresets> preset in this.Config.Presets)
            {
                ushort temp = x;
                // from SiMPL > to Plugin
                trilist.SetSigTrueAction(joinMap.PresetRecallStart.JoinNumber + temp + 1,
                    () => this.RecallPreset(temp));
                trilist.SetSigTrueAction(joinMap.PresetStoreStart.JoinNumber + temp + 1, () => this.SavePreset(temp));

                // from Plugin > to SiMPL
                preset.Value.LabelFeedback.LinkInputSig(
                    trilist.StringInput[joinMap.PresetNamesStart.JoinNumber + temp]);

                x++;
            }

            // from SiMPL > to Plugin
            trilist.SetSigTrueAction(joinMap.PrivacyOn.JoinNumber, () => this.PrivacyOn());
            trilist.SetSigTrueAction(joinMap.PrivacyOff.JoinNumber, () => this.PrivacyOff());
        }

        #endregion

        public BoolFeedback IsOnline { get; private set; }
    }

    /// <summary>
    /// Camera PTZ controls enum
    /// </summary>
    public enum eCameraPtzControls
    {
        Stop,
        PanLeft,
        PanRight,
        TiltUp,
        TiltDown,
        ZoomIn,
        ZoomOut
    }
}