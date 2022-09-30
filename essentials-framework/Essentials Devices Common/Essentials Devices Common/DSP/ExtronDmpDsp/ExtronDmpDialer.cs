using System;
using System.Linq;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common.Codec;

namespace ExtronDmp
{
	/// <summary>
	/// QSC DSP Dialer class
	/// </summary>
	public class ExtronDmpDialer : IHasDialer
	{
		/// <summary>
		/// Parent DSP
		/// </summary>
		public ExtronDmp Parent { get; private set; }

		/// <summary>
		/// Dialer block configuration 
		/// </summary>
		public ExtronDmpDialerConfig _Config;

		/// <summary>
		/// Tracks in call state
		/// </summary>
		public bool IsInCall { get; private set; }

		/// <summary>
		/// Dial string feedback 
		/// </summary>
		public StringFeedback DialStringFeedback;
		// Dial string backer field
		private string _dialString;
		/// <summary>
		/// Dial string property
		/// </summary>
		public string DialString
		{
			get { return _dialString; }
			private set
			{
				_dialString = value;
				DialStringFeedback.FireUpdate();
			}
		}

		/// <summary>
		/// Off hook feedback
		/// </summary>
		public BoolFeedback OffHookFeedback;
		// Off hook backer field
		private bool _offHook;
		/// <summary>
		/// Off Hook property
		/// </summary>
		public bool OffHook
		{
			get { return _offHook; }
			private set
			{
				_offHook = value;
				OffHookFeedback.FireUpdate();
			}
		}

		/// <summary>
		/// Auto answer feedback
		/// </summary>
		public BoolFeedback AutoAnswerFeedback;
		// Auto answer backer field
		private bool _autoAnswerState;
		/// <summary>
		/// Auto answer property
		/// </summary>
		public bool AutoAnswerState
		{
			get { return _autoAnswerState; }
			private set
			{
				_autoAnswerState = value;
				AutoAnswerFeedback.FireUpdate();
			}
		}

		/// <summary>
		/// Do not disturb feedback
		/// </summary>
		public BoolFeedback DoNotDisturbFeedback;
		// Do not disturb backer field
		private bool _doNotDisturbState;
		/// <summary>
		/// Do not disturb property
		/// </summary>
		public bool DoNotDisturbState
		{
			get { return _doNotDisturbState; }
			private set
			{
				_doNotDisturbState = value;
				DoNotDisturbFeedback.FireUpdate();
			}
		}

		/// <summary>
		/// Caller ID number feedback
		/// </summary>
		public StringFeedback CallerIdNumberFeedback;
		// Caller ID number backer field
		private string _callerIdNumber;
		/// <summary>
		///  Caller ID Number property
		/// </summary>
		public string CallerIdNumber
		{
			get { return _callerIdNumber; }
			set
			{
				_callerIdNumber = value;
				CallerIdNumberFeedback.FireUpdate();
			}
		}

		/// <summary>
		/// Incoming call feedback
		/// </summary>
		public BoolFeedback IncomingCallFeedback;
		// Incoming call backer field
		private bool _incomingCall;
		/// <summary>
		/// Incoming call property
		/// </summary>
		public bool IncomingCall
		{
			get { return _incomingCall; }
			set
			{
				_incomingCall = value;
				IncomingCallFeedback.FireUpdate();
			}
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="config">configuration object</param>
		/// <param name="parent">parent dsp instance</param>
		public ExtronDmpDialer(ExtronDmpDialerConfig config, ExtronDmp parent)
		{
			_Config = config;
			Parent = parent;

			IncomingCallFeedback = new BoolFeedback(() => { return IncomingCall; });
			DialStringFeedback = new StringFeedback(() => { return DialString; });
			OffHookFeedback = new BoolFeedback(() => { return OffHook; });
			AutoAnswerFeedback = new BoolFeedback(() => { return AutoAnswerState; });
			DoNotDisturbFeedback = new BoolFeedback(() => { return DoNotDisturbState; });
			CallerIdNumberFeedback = new StringFeedback(() => { return CallerIdNumber; });
		}

		/// <summary>
		/// Call status change event
		/// Interface requires this
		/// </summary>
		public event EventHandler<CodecCallStatusItemChangeEventArgs> CallStatusChange;

		/// <summary>
		/// Call status event handler
		/// </summary>
		/// <param name="args"></param>
		public void OnCallStatusChange(CodecCallStatusItemChangeEventArgs args)
		{
			var handler = CallStatusChange;
			if (handler == null) return;
			CallStatusChange(this, args);
		}

		/*
		/// <summary>
		/// Subscription method
		/// </summary>
		public void Subscribe()
		{
			try
			{
				// Do subscriptions and blah blah
				// This would be better using reflection JTA 2018-08-28
				//PropertyInfo[] properties = Tags.GetType().GetCType().GetProperties();
				var properties = _Config.GetType().GetCType().GetProperties();
				//GetPropertyValues(Tags);

				Debug.Console(2, "QscDspDialer Subscribe");
				foreach (var prop in properties)
				{
					if (prop.Name.Contains("Tag") && !prop.Name.ToLower().Contains("keypad"))
					{
						var propValue = prop.GetValue(_Config, null) as string;
						Debug.Console(2, "Property {0}, {1}, {2}\n", prop.GetType().Name, prop.Name, propValue);
						SendSubscriptionCommand(propValue);
					}
				}
			}
			catch (Exception e)
			{
				Debug.Console(2, "QscDspDialer Subscription Error: '{0}'\n", e);
			}

			// SendSubscriptionCommand(, "1");
			// SendSubscriptionCommand(config. , "mute", 500);
		}
		*/

		/// <summary>
		/// Parses subscription messages
		/// </summary>
		/// <param name="customName"></param>
		/// <param name="value"></param>
		public void ParseSubscriptionMessage(string customName, string value)
		{

			/*
			// Check for valid subscription response
			Debug.Console(0, "ParseMessage customName: {0} value: '{1}'", customName, value);
			if (customName == _Config.DialStringTag)
			{
				Debug.Console(0, "ParseMessage customName: {0} == Tags.DialStringTag: {1} | value: {2}", customName, _Config.DialStringTag, value);
				DialString = value;
				DialStringFeedback.FireUpdate();
			}
			else if (customName == _Config.DoNotDisturbTag)
			{
				switch (value)
				{
					case "on":
						DoNotDisturbState = true;
						break;
					case "off":
						DoNotDisturbState = false;
						break;
				}
			}
			else if (customName == _Config.CallStatusTag)
			{
				// TODO [ ] Add incoming call/ringing to parse subscription message
				if (value == "Incoming")
				{
					this.IncomingCall = true;
				}
				else if (value.Contains("Ringing"))
				{
					this.IncomingCall = false;
					this.OffHook = true;
					var splitString = value.Split(' ');
					if (splitString.Count() >= 2)
					{
						CallerIdNumber = splitString[1];
					}
				}
				else if (value.Contains("Dialing") || value.Contains("Connected"))
				{
					OffHook = true;
					var splitString = value.Split(' ');
					if (splitString.Count() >= 2)
					{
						CallerIdNumber = splitString[1];
					}
				}
				else if (value == "Disconnected")
				{
					OffHook = false;
					IncomingCall = false;
					CallerIdNumber = "";
					if (_Config.ClearOnHangup)
					{
						SendKeypad(EKeypadKeys.Clear);
					}
				}
				else if (value == "Idle")
				{
					IncomingCall = false;
					OffHook = false;
					CallerIdNumber = "";
					if (_Config.ClearOnHangup)
					{
						SendKeypad(EKeypadKeys.Clear);
					}
				}
			}
			else if (customName == _Config.AutoAnswerTag)
			{
				switch (value)
				{
					case "on":
						AutoAnswerState = true;
						break;
					case "off":
						AutoAnswerState = false;
						break;
				}
			}
			else if (customName == _Config.HookStatusTag)
			{
				switch (value)
				{
					case "true":
						OffHook = true;
						break;
					case "false":
						OffHook = false;
						break;
				}
			}
			 */
		}

		public void Init()
		{
			SendDialerCommand("RS", "1");
			SendDialerCommand("LS", "1");

		}
		/// <summary>
		/// Toggles the do not disturb state
		/// </summary>
		public void DoNotDisturbToggle()
		{
			var dndState = !DoNotDisturbState ? "1" : "0";
			SendDialerCommand("DND", dndState);
		}

		/// <summary>
		/// Sets the do not disturb state on
		/// </summary>
		public void DoNotDisturbOn()
		{
			SendDialerCommand("DND", "1");
		}

		/// <summary>
		/// Sets the do not disturb state off
		/// </summary>
		public void DoNotDisturbOff()
		{
			SendDialerCommand("DND", "0");
		}

		/// <summary>
		/// Toggles the auto answer state
		/// </summary>
		public void AutoAnswerToggle()
		{
			var autoAnswerState = !AutoAnswerState ? "1" : "0";
			SendDialerCommand("AA", autoAnswerState);
		}

		/// <summary>
		/// Sets the auto answer state on
		/// </summary>
		public void AutoAnswerOn()
		{
			SendDialerCommand("AA", "1");
		}

		/// <summary>
		/// Sets the auto answer state off
		/// </summary>
		public void AutoAnswerOff()
		{
			SendDialerCommand("AA", "0");
		}


		/// <summary>
		/// Sends the pressed keypad number
		/// </summary>
		/// <param name="button">Button pressed</param>
		public void SendKeypad(EKeypadKeys button)
		{
			if (this.OffHook)
			{
				string tempDigit = "";
				switch (button)
				{
					case EKeypadKeys.Num0: tempDigit = "0"; break;
					case EKeypadKeys.Num1: tempDigit = "1"; break;
					case EKeypadKeys.Num2: tempDigit = "2"; break;
					case EKeypadKeys.Num3: tempDigit = "3"; break;
					case EKeypadKeys.Num4: tempDigit = "4"; break;
					case EKeypadKeys.Num5: tempDigit = "5"; break;
					case EKeypadKeys.Num6: tempDigit = "6"; break;
					case EKeypadKeys.Num7: tempDigit = "7"; break;
					case EKeypadKeys.Num8: tempDigit = "8"; break;
					case EKeypadKeys.Num9: tempDigit = "9"; break;
					case EKeypadKeys.Pound: tempDigit = "#"; break;
					case EKeypadKeys.Star: tempDigit = "*"; break;
					case EKeypadKeys.Backspace: break;
					case EKeypadKeys.Clear: break;
				}
				SendDialerCommand("DD", "1," + tempDigit);
			}
			else
			{
				switch (button)
				{
					case EKeypadKeys.Num0: DialString += "0"; break;
					case EKeypadKeys.Num1: DialString += "1"; break;
					case EKeypadKeys.Num2: DialString += "2"; break;
					case EKeypadKeys.Num3: DialString += "3"; break;
					case EKeypadKeys.Num4: DialString += "4"; break;
					case EKeypadKeys.Num5: DialString += "5"; break;
					case EKeypadKeys.Num6: DialString += "6"; break;
					case EKeypadKeys.Num7: DialString += "7"; break;
					case EKeypadKeys.Num8: DialString += "8"; break;
					case EKeypadKeys.Num9: DialString += "9"; break;
					case EKeypadKeys.Pound: DialString += "#"; break;
					case EKeypadKeys.Star: DialString += "*"; break;
					case EKeypadKeys.Backspace:
						{
							if (DialString.Length > 0)
							{
								DialString = DialString.Remove(DialString.Length - 1, 1);
							}
							break;
						} 
					case EKeypadKeys.Clear: DialString = ""; break;
				}
			}

		}

		public void SetLineStatus(ELineStatus status)
		{
			switch (status)
			{
				case ELineStatus.Active: this.OffHook = true; break;
				case ELineStatus.Inactive: this.OffHook = false; break;
				case ELineStatus.Incoming: this.OffHook = true; break;
				case ELineStatus.None: this.OffHook = false; break;
				case ELineStatus.OnHold: this.OffHook = true; break;
				case ELineStatus.Outgoing: this.OffHook = true; break;

				

			}

		}

		/// <summary>
		/// Toggles the hook state
		/// </summary>
		public void Dial()
		{
			if (!this.OffHook)
			{
				SendDialerCommand("DIAL", DialString);
			}
			else
			{
				SendDialerCommand("END");
			}
		}

		/// <summary>
		/// Dial overload
		/// Dials the number provided
		/// </summary>
		/// <param name="number">Number to dial</param>
		public void Dial(string number)
		{
			SendDialerCommand("DIAL", number);
		}

		/// <summary>
		/// Ends the current call with the provided Id
		/// </summary>		
		/// <param name="item">Use null as the parameter, use of CodecActiveCallItem is not implemented</param>
		public void EndCall(CodecActiveCallItem item)
		{
			SendDialerCommand("END");
		}

		/// <summary>
		/// Ends all connectted calls
		/// </summary>
		public void EndAllCalls()
		{
			SendDialerCommand("END");
		}

		/// <summary>
		/// Accepts incoming call
		/// </summary>
		public void AcceptCall()
		{
			SendDialerCommand("END");
		}

		/// <summary>
		/// Accepts the incoming call overload
		/// </summary>
		/// <param name="item">Use "", use of CodecActiveCallItem is not implemented</param>
		public void AcceptCall(CodecActiveCallItem item)
		{
			SendDialerCommand("ANS", "1");
		}

		/// <summary>
		/// Rejects the incoming call
		/// </summary>
		public void RejectCall()
		{
			SendDialerCommand("REJ", "1");
		}

		/// <summary>
		/// Rejects the incoming call overload
		/// </summary>
		/// <param name="item"></param>
		public void RejectCall(CodecActiveCallItem item)
		{
			SendDialerCommand("REJ", "1");
		}

		public void SendDialerCommand(string cmd)
		{
			string cmdToSemd = string.Format("{0}{1}{2}VOIP", '\x1B', cmd, _Config.LineNumber);

			Parent.SendLine(cmdToSemd);
		}

		public void SendDialerCommand(string cmd, string value)
		{
			string cmdToSemd = string.Format("{0}{1}{2},{3}VOIP", '\x1B', cmd, _Config.LineNumber, value);

			Parent.SendLine(cmdToSemd);
		}
		/// <summary>
		/// Sends the DTMF tone of the keypad digit pressed
		/// </summary>
		/// <param name="digit">keypad digit pressed as a string</param>
		public void SendDtmf(string digit)
		{
			var keypadTag = EKeypadKeys.Clear;
			// Debug.Console(2, "DIaler {0} SendKeypad {1}", this.ke);
			switch (digit)
			{
				case "0":
					keypadTag = EKeypadKeys.Num0;
					break;
				case "1":
					keypadTag = EKeypadKeys.Num1;
					break;
				case "2":
					keypadTag = EKeypadKeys.Num2;
					break;
				case "3":
					keypadTag = EKeypadKeys.Num3;
					break;
				case "4":
					keypadTag = EKeypadKeys.Num4;
					break;
				case "5":
					keypadTag = EKeypadKeys.Num5;
					break;
				case "6":
					keypadTag = EKeypadKeys.Num6;
					break;
				case "7":
					keypadTag = EKeypadKeys.Num7;
					break;
				case "8":
					keypadTag = EKeypadKeys.Num8;
					break;
				case "9":
					keypadTag = EKeypadKeys.Num9;
					break;
				case "#":
					keypadTag = EKeypadKeys.Pound;
					break;
				case "*":
					keypadTag = EKeypadKeys.Star;
					break;
			}

			if (keypadTag == EKeypadKeys.Clear) return;

			SendKeypad(keypadTag);
		}

		/// <summary>
		/// Keypad digits pressed enum
		/// </summary>
		public enum EKeypadKeys
		{
			Num1,
			Num2,
			Num3,
			Num4,
			Num5,
			Num6,
			Num7,
			Num8,
			Num9,
			Num0,
			Star,
			Pound,
			Clear,
			Backspace
		}
		public enum ELineStatus
		{
			None = 0, 
			Inactive = 1, 
			Active = 2, 
			OnHold = 3,
			Incoming = 4,
			Outgoing = 5

		}
	}
}