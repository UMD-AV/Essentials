using System;
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;
using PepperDash.Core;
using PepperDash.Essentials.Core.Config;
using ViscaCameraPlugin;

namespace CrestronCameraPlugin
{
    public class CrestronCameraDevice : ViscaCameraDevice
    {
        public CrestronCameraDevice(string key, string name, IBasicCommunication comms, ViscaCameraConfig config)
			: base(key, name, comms, config)
        {
        }

        public override void PollAutoTrack()
        {
            if (this._autoTrackingCapable)
            {
                //Send preset 200 (C8) to set auto track heartbeat on
                var cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x02, 0xC8, 0xFF };
                QueueCommand(eViscaCameraCommand.AutoTrackInquiry, cmd);
            }
        }

        /// <summary>
        /// Turn AutoTracking On
        /// </summary>
        public override void SetAutoTrackingOn()
        {
            if (this._autoTrackingCapable)
            {
                var cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x02, 0x50, 0xFF };
                QueueCommand(eViscaCameraCommand.AutoTrackOnPresetCmd, cmd);
            }
        }

        /// <summary>
        /// Turn AutoTracking Off
        /// </summary>
        public override void SetAutoTrackingOff()
        {
            if (this._autoTrackingCapable)
            {
                var cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x02, 0x51, 0xFF };
                QueueCommand(eViscaCameraCommand.AutoTrackOffPresetCmd, cmd);
            }

        }

        protected override void ParseAdditionalFeedback(byte[] message)
        {
            if (this._autoTrackingCapable & message.Length >= 8)
            {
                if (message[0] == 0x30 && message[1] == 0x30 && message[2] == 0x30 && message[3] == 0x30 && message[4] == 0x01 && message[6] == 0x00)
                {
                    if (message[5] == 0x01)
                    {
                        AutoTrackingOn = true;
                    }
                    else if (message[5] == 0x00)
                    {
                        AutoTrackingOn = false;
                    }
                }
            }
        }

        protected override void ParseAutoTrackFeedback(byte[] message)
        {
            if (message[message.Length - 3] == 0x50)
            {
                if (message[message.Length - 2] == 0x01)
                {
                    AutoTrackingOn = true;
                }
                else if (message[message.Length - 2] == 0x00)
                {
                    AutoTrackingOn = false;
                }
            }
        }
    }
}

