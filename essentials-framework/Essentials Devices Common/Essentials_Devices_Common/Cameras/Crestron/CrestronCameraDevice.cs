﻿using PepperDash.Core;
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
            //Camera sends status via heartbeat, no longer needed
        }

        /// <summary>
        /// Turn AutoTracking On
        /// </summary>
        public override void SetAutoTrackingOn()
        {
            if (this._autoTrackingCapable)
            {
                byte[] cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x02, 0x50, 0xFF };
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
                byte[] cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x02, 0x51, 0xFF };
                QueueCommand(eViscaCameraCommand.AutoTrackOffPresetCmd, cmd);
            }
        }

        /// <summary>
        /// Initialize the camera by sending Address Set Broadcast and IF Clear Broadcasst
        /// </summary>
        public override void InitializeCamera()
        {
            // send address set broadcast
            QueueCommand(new byte[] { 0x88, 0x30, 0x01, 0xFF });

            // send IF clear on connection
            QueueCommand(new byte[] { 0x88, 0x01, 0x00, 0x01, 0xFF });

            //Send preset 200 (C8) to set auto track heartbeat on
            QueueCommand(eViscaCameraCommand.AutoTrackInquiry,
                new byte[] { _address, 0x01, 0x04, 0x3F, 0x02, 0xC8, 0xFF });
        }

        protected override void ParseAdditionalFeedback(byte[] message)
        {
            if (this._autoTrackingCapable & message.Length >= 8)
            {
                if (message[0] == 0x30 && message[1] == 0x30 && message[2] == 0x30 && message[3] == 0x30 &&
                    message[4] == 0x01 && message[6] == 0x00)
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
            else if (message[0] == 0x30 && message[1] == 0x30 && message[2] == 0x30 && message[3] == 0x30 &&
                     message[4] == 0x01 && message[6] == 0x00)
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
}