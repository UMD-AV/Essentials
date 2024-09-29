using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp.CrestronXml;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.Fusion;
using PepperDash.Core;
using PepperDash.Core.Logging;

namespace DynFusion
{
    public class DynFusionHelpRequest
    {
        public event EventHandler<MessageEventArgs> HelpMessageFromFusionEvent;
        public event EventHandler<EventArgs> ClearHelpEvent;
        private List<string> helpRequestIds = new List<string>();
        private CMutex requestMutex = new CMutex();
        private StringSigDataFixedName helpSig;
        public string Organizer { get; set; }
        public DynFusionHelpRequest(StringSigDataFixedName HelpSig)
        {
            Organizer = "Touchpanel";
            helpSig = HelpSig;
        }

        public void Clear()
        {
            if (ClearHelpEvent != null)
            {
                ClearHelpEvent(this, null);
            }
        }

        public void CreateRequest(string message, string id)
        {
            if (message.Length < 1)
                return;
            requestMutex.WaitForMutex();
            try
            {
                string uniqueId = id + "-" + Guid.NewGuid().ToString();

                helpSig.InputSig.StringValue = "<HelpRequest><ID>" + uniqueId + "</ID><Message>"
                        + message + "</Message><Severity>1</Severity><Organizer>" + Organizer
                        + "</Organizer><Type>new_user</Type></HelpRequest>";
 
                helpRequestIds.Add(uniqueId);
                HelpMessageFromFusionEvent(this, new MessageEventArgs(id, "Help Request Sent", 1));
            }
            catch (Exception ex)
            {                
                ErrorLog.Error("Error in fusion help request create request: {0}", ex);
            }
            finally
            {
                requestMutex.ReleaseMutex();
            }
        }

        public void CancelRequest(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                requestMutex.WaitForMutex();
                try
                {
                    foreach (var req in helpRequestIds)
                    {
                        if (req.ToString().StartsWith(id))
                        {
                            helpSig.InputSig.StringValue = "<HelpRequest><ID>" + req.ToString() + "</ID><Message>cancel</Message><Type>cancel</Type></HelpRequest>";
                        }
                    }
                    helpRequestIds.RemoveAll(o => (o.StartsWith(id)));
                    HelpMessageFromFusionEvent(this, new MessageEventArgs(id, "", 0));
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("Error in fusion help request cancel: {0}", ex);
                }
                finally
                {
                    requestMutex.ReleaseMutex();
                }
            }
        }

        public void CancelAll()
        {
            requestMutex.WaitForMutex();
            try
            {
                if (helpRequestIds != null)
                {
                    foreach (string uniqueId in helpRequestIds)
                    {
                        helpSig.InputSig.StringValue = "<HelpRequest><ID>" + uniqueId + "</ID><Message>cancel</Message><Type>cancel</Type></HelpRequest>";
                    }
                }
                helpRequestIds.Clear();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Error in fusion help request cancel all: {0}", ex);
            }
            finally
            {
                requestMutex.ReleaseMutex();
            }
        }

        public void GetOpenItems()
        {
            requestMutex.WaitForMutex();
            try
            {
                helpSig.InputSig.StringValue = "<HelpRequest><Type>open_items</Type></HelpRequest>";
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Error in fusion help request get open items: {0}", ex);
            }
            finally
            {
                requestMutex.ReleaseMutex();
            }
        }

        public void ParseFeedback(string data)
        {
            if (data.Length < 2)
            {
                return;
            }
            requestMutex.WaitForMutex();
            try
            {
                XmlDocument XmlDoc = new XmlDocument();
                XmlDoc.LoadXml(data);
                var xmlId = XmlDoc.SelectSingleNode("HelpRequest/ID");
                if (xmlId != null)
                {
                    string message = "";
                    ushort active = 1;
                    string id = xmlId.InnerText.ToString();
                    Debug.Console(1, "Help request response id: {0}", id);
                    
                    var xmlMessage = XmlDoc.SelectSingleNode("HelpRequest/Message");
                    if (xmlMessage != null)
                    {                        
                        message = xmlMessage.InnerText.ToString();
                        Debug.Console(0, "Help request message: {0}", message);
                    }
                    var xmlType = XmlDoc.SelectSingleNode("HelpRequest/Type");
                    if (xmlType != null)
                    {
                        string type = xmlType.InnerText.ToString();
                        Debug.Console(0, "Help request type: {0}", type);
                        if (type == "cancel" || type == "close")
                        {
                            active = 0;
                            if (helpRequestIds != null)
                            {
                                helpRequestIds.RemoveAll(o => o == id);
                            }
                        }
                    }
                    if (HelpMessageFromFusionEvent != null)
                        HelpMessageFromFusionEvent(this, new MessageEventArgs(id, message, active));
                }
                else if (XmlDoc.SelectNodes("HelpRequest/OpenItems").Count > 0)
                {
                    var openIds = XmlDoc.SelectNodes("HelpRequest/OpenItems/ID");
                    if (openIds.Count > 0)
                    {
                        foreach (XmlNode x in openIds)
                        {
                            string openId = x.InnerText.ToString();
                            Debug.Console(0, "Help request open id: {0}", openId);
                            if (!helpRequestIds.Contains(openId))
                            {
                                helpRequestIds.Add(openId);
                            }
                        }
                        if (HelpMessageFromFusionEvent != null)
                            HelpMessageFromFusionEvent(this, new MessageEventArgs("", "Help Request Sent", 1));
                    }
                    else
                    {
                        if (HelpMessageFromFusionEvent != null)
                            HelpMessageFromFusionEvent(this, new MessageEventArgs("", "", 0));
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Exception("Error in help request parsing", ex);
            }
            finally
            {
                requestMutex.ReleaseMutex();
            }
        }
    }

    public class MessageEventArgs : EventArgs
    {
        public string StringVal;
        public string Id;
        public ushort Active;

        public MessageEventArgs()
        {
        }
        public MessageEventArgs(string id, string stringVal, ushort active)
        {
            this.StringVal = stringVal;
            this.Id = id;
            this.Active = active;
        }
    }
}