using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Devices;

namespace ExtronMlsDspPlugin
{
    public class ExtronMlsDsp : EssentialsBridgeableDevice
	{
        private readonly IBasicCommunication _comms;
        private CommunicationGather _gather;
        private GenericCommunicationMonitor _commsMonitor;
        private bool muteFb = false;
        private int volumeFb = 0;
        private bool volumeUpPress = false;
        private bool volumeDownPress = false;

        /// <summary>
        /// Online feedback
        /// </summary>
        public BoolFeedback OnlineFeedback { get; private set; }

        /// <summary>
        /// Monitor status feedback
        /// </summary>
        public IntFeedback MonitorStatusFeedback { get; private set; }

        /// <summary>
        /// Mute feedback
        /// </summary>
        public BoolFeedback MuteFeedback { get; private set; }

        /// <summary>
        /// Volume feedback
        /// </summary>
        public IntFeedback VolumeFeedback { get; private set; }
		
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="key">String</param>
		/// <param name="name">String</param>
		/// <param name="comm">IBasicCommunication</param>
		public ExtronMlsDsp(string key, string name, IBasicCommunication comm)
			: base(key, name)
		{
            _comms = comm;
			_gather = new CommunicationGather(_comms, "\x0d\x0a");
			_gather.LineReceived += this.Handle_BytesRecieved;
            _comms.Connect();

			// Comm monitoring, will poll every 30s.
			_commsMonitor = new GenericCommunicationMonitor(this, _comms, 30000, 120000, 300000, Poll);
            _commsMonitor.Start();

            //Link feedback
            OnlineFeedback = new BoolFeedback(() => _comms.IsConnected);
            MonitorStatusFeedback = new IntFeedback(() => (int)_commsMonitor.Status);
            MuteFeedback = new BoolFeedback(() => muteFb);
            VolumeFeedback = new IntFeedback(() => volumeFb);

            _commsMonitor.IsOnlineFeedback.OutputChange += new EventHandler<FeedbackEventArgs>(IsOnlineFeedback_OutputChange);
		}

        /// <summary>
        /// Link to API
        /// </summary>
        /// <param name="trilist">BasicTriList</param>
        /// <param name="joinStart">uint</param>
        /// <param name="joinMapKey">string</param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
           var joinMap = new ExtronMlsDspDeviceJoinMap(joinStart);

			Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

			//From Plugin to Simpl
			OnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = this.Name;
            trilist.StringInput[joinMap.Presets.JoinNumber + 1].StringValue = "Default Volume";

            //From Simpl to Plugin
            trilist.SetSigTrueAction(joinMap.Presets.JoinNumber + 1, () => DefaultVolume());
            
            //Link main volume to channel 1 feedback
			trilist.StringInput[joinMap.ChannelName.JoinNumber + 1].StringValue = "Main Volume";
            trilist.UShortInput[joinMap.ChannelType.JoinNumber + 1].UShortValue = 0;
            trilist.BooleanInput[joinMap.ChannelVisible.JoinNumber + 1].BoolValue = true;

            MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.ChannelMuteToggle.JoinNumber + 1]);
            VolumeFeedback.LinkInputSig(trilist.UShortInput[joinMap.ChannelVolume.JoinNumber + 1]);

            //Link channel 1 actions to main volume            
            trilist.SetSigTrueAction(joinMap.ChannelMuteToggle.JoinNumber + 1, () => MuteToggle());
            trilist.SetSigTrueAction(joinMap.ChannelMuteOn.JoinNumber + 1, () => MuteOn());
            trilist.SetSigTrueAction(joinMap.ChannelMuteOff.JoinNumber + 1, () => MuteOff());

            trilist.SetBoolSigAction(joinMap.ChannelVolumeUp.JoinNumber + 1, b => VolumeUp(b));
            trilist.SetBoolSigAction(joinMap.ChannelVolumeDown.JoinNumber + 1, b => VolumeDown(b));

            trilist.SetUShortSigAction(joinMap.ChannelVolume.JoinNumber + 1, u =>
            {
                if(trilist.BooleanOutput[joinMap.EnableLevelSend.JoinNumber].BoolValue == true)
                { 
                    SetVolume(u);
                }
            });
        }

		/// <summary>
		/// Polls the device, should be called by comm monitor only.
		/// </summary>
		void Poll()
		{
            //Query the main volume level and mute
			SendText("V");
            SendText("Z");
		}

		/// <summary>
		/// Handles a response message from the DSP
		/// </summary>
		/// <param name="dev"></param>
		/// <param name="args"></param>
		void Handle_BytesRecieved(object dev, GenericCommMethodReceiveTextArgs args)
		{
			Debug.Console(2, this, "RX: '{0}'", args.Text);
			try
			{
				if (args.Text.Contains("Vol"))
				{
                    //Get number after Vol, for example 013 if return is Vol013
                    int start = args.Text.IndexOf("Vol") + 3;
                    int vol = int.Parse(args.Text.Substring(start, args.Text.Length - start));
                    volumeFb = vol;
                    VolumeFeedback.FireUpdate();
				}

                else if (args.Text.Contains("Amt"))
                {
                    //Found mute feedback text
                    if(args.Text.Contains("Amt0"))
                    {
                        muteFb = false;
                        MuteFeedback.FireUpdate();
                    }
                    else if (args.Text.Contains("Amt1"))
                    {
                        muteFb = true;
                        MuteFeedback.FireUpdate();
                    }
                }
			}
			catch (Exception e)
			{
				if (Debug.Level == 2)
					Debug.Console(2, this, "Error parsing response: '{0}'\n{1}", args.Text, e);
			}

		}

        void IsOnlineFeedback_OutputChange(object dev, FeedbackEventArgs args)
        {
            if (args.BoolValue == true)
            {
                //Device is now online
                //Lock front panel adjustments
                SendText("1X");
                //Set Audio Input 1
                SendText("1$");
                //Poll Volume
                SendText("V");
                //Poll Mute
                SendText("Z");
            }
        }

		/// <summary>
		/// Sends a command to the DSP
		/// </summary>
		/// <param name="s">Command to send</param>
		public void SendText(string s)
		{
			Debug.Console(1, this, "TX: '{0}'", s);
			_comms.SendText(s);
		}

        public void DefaultVolume()
        {
            SendText("35V");
        }

        public void SetVolume(ushort vol)
        {
            int scaledVol = vol * 100 / ushort.MaxValue;
            SendText(string.Format("{0}V", scaledVol));
        }

        public void VolumeUp(bool press)
        {
            volumeUpPress = press;
            ushort count = 0;
            while (volumeUpPress && count < 100)
            {
                SendText("+V");
                count++;
                CrestronEnvironment.Sleep(50);
            }
        }

        public void VolumeDown(bool press)
        {
            volumeDownPress = press;
            ushort count = 0;
            while (volumeDownPress && count < 100)
            {
                SendText("-V");
                count++;
                CrestronEnvironment.Sleep(50);
            }
        }

        public void MuteToggle()
        {
            if (muteFb == true)
            {
                MuteOff();
            }
            else
            {
                MuteOn();
            }
        }

        public void MuteOn()
        {
            SendText("1Z");
        }

        public void MuteOff()
        {
            SendText("0Z");
        }
	}
}