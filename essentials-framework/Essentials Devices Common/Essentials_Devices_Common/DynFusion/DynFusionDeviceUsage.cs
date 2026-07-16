using System;
using System.Collections.Generic;
using PepperDash.Core;
using UmdEssentials.Core;

namespace DynFusion
{
    public class DynFusionDeviceUsage : EssentialsDevice
    {
        public Dictionary<string, UsageInfo> UsageInfoDict = new Dictionary<string, UsageInfo>();


        public int UsageMinThreshold = 1;
        private readonly DynFusionDevice _dynFusionDevice;

        public DynFusionDeviceUsage(string key, DynFusionDevice dynFusionInstance)
            : base(key, key)
        {
            try
            {
                _dynFusionDevice = dynFusionInstance;
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "{0}", ex);
            }
        }

        public void CreateDevice(uint deviceNumber, string type, string name)
        {
            try
            {
                UsageInfo newDev = new UsageInfo();
                newDev.Name = name;
                newDev.Type = type;
                newDev.UsageType = UsageType.Device;
                newDev.JoinNumber = (ushort)deviceNumber;
                string key = string.Format("DEV:{0}", deviceNumber);
                UsageInfoDict.Add(key, newDev);

                Debug.Console(1, this, string.Format("DynFusionDeviceUsage Created Device key: {0}", key));
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "{0}", ex);
            }
        }

        public void CreateDisplay(uint deviceNumber, string name)
        {
            try
            {
                UsageInfo newDisp = new UsageInfo();
                newDisp.Name = name;
                newDisp.Type = "Display";
                newDisp.SourceNumber = 0;
                newDisp.UsageType = UsageType.Display;
                newDisp.JoinNumber = (ushort)deviceNumber;
                string key = string.Format("DISP:{0}", deviceNumber);
                UsageInfoDict.Add(key, newDisp);
                Debug.Console(1, this, string.Format("DynFusionDeviceUsage Created Display key: {0}", key));
            }
            catch (Exception x)
            {
                Debug.Console(1, this, "{0}", x);
            }
        }

        public void CreateSource(uint sourceNumber, string name, string type)
        {
            try
            {
                UsageInfo newSource = new UsageInfo();
                newSource.Name = name;
                newSource.Type = type;
                newSource.SourceNumber = sourceNumber;
                newSource.UsageType = UsageType.Source;
                string key = string.Format("SRC:{0}", sourceNumber);
                UsageInfoDict.Add(key, newSource);
                Debug.Console(1, this, "DynFusionDeviceUsage Created Source key: {0}", key);
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "{0}", ex);
            }
        }

        public void StartStopDevice(ushort device, bool action)
        {
            string key = string.Format("DEV:{0}", device);
            if (action)
                StartDevice(key);
            else
                StopDevice(key);
        }

        public void ChangeSource(ushort disp, ushort source)
        {
            try
            {
                string dispKey = string.Format("DISP:{0}", disp);
                Debug.Console(1, this, "DynFusionDeviceUsage Change Source {0}", dispKey);
                if (UsageInfoDict.ContainsKey(dispKey))
                {
                    Debug.Console(1, this,
                        "DynFusionDeviceUsage Change Source dispKey: {0}, LastSource: {1} New Source: {2}", dispKey,
                        UsageInfoDict[dispKey].SourceNumber, source);
                    // get last source
                    uint lastSourceNumber = UsageInfoDict[dispKey].SourceNumber;

                    // Start new Device
                    if (lastSourceNumber > 0 && source > 0)
                    {
                        string newSourceKey = string.Format("SRC:{0}", source);
                        StartDevice(newSourceKey);
                        UsageInfoDict[dispKey].SourceNumber = source;
                    }
                    //Start new device && display
                    else if (lastSourceNumber == 0 && source > 0)
                    {
                        string newSourceKey = string.Format("SRC:{0}", source);
                        StartDevice(dispKey);
                        StartDevice(newSourceKey);
                        UsageInfoDict[dispKey].SourceNumber = source;
                    }
                    // Stop display
                    else if (lastSourceNumber > 0 && source == 0)
                    {
                        UsageInfoDict[dispKey].SourceNumber = source;
                        StopDevice(dispKey);
                    }

                    if (lastSourceNumber > 0)
                    {
                        bool onlySource = true;
                        foreach (KeyValuePair<string, UsageInfo> entry in UsageInfoDict)
                            //Debug.Console(1,this, "DynFusionDeviceUsage Change Source dictEntry: {0}", entry.Key);
                            if (entry.Key.Contains("DISP"))
                                //Debug.Console(1,this, "DynFusionDeviceUsage Change Source dictEntry Display - Source #: {0}", entry.Value.sourceNumber);
                                if (entry.Value.SourceNumber == lastSourceNumber)
                                {
                                    onlySource = false;
                                    break;
                                }

                        if (onlySource)
                        {
                            string lastSourceKey = string.Format("SRC:{0}", lastSourceNumber);
                            StopDevice(lastSourceKey);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "{0}", ex);
            }
        }

        public void StartDevice(string key)
        {
            UsageInfo value;
            if (UsageInfoDict.TryGetValue(key, out value))
                value.StartTime = DateTime.Now;
            else
                Debug.Console(1, this, "DynFusionDeviceUsage no device number {0}", key);
        }

        public void StopDevice(string key)
        {
            if (UsageInfoDict.ContainsKey(key))
            {
                int minUsed = (int)(DateTime.Now - UsageInfoDict[key].StartTime).TotalMinutes;
                if (minUsed >= UsageMinThreshold)
                {
                    string usageString = string.Format(
                        "USAGE||{0:yyyy-MM-dd}||{1:HH:mm:ss}||TIME||{2}||{3}||-||{4}||-||{5}||{6}||", DateTime.Now,
                        DateTime.Now,
                        UsageInfoDict[key].Type,
                        UsageInfoDict[key].Name,
                        minUsed,
                        "",
                        "");
                    _dynFusionDevice.FusionSymbol.DeviceUsage.InputSig.StringValue = usageString;
                    Debug.Console(1, this, "DynFusionDeviceUsage message \n{0}", usageString);
                }
                else
                {
                    Debug.Console(1, this, "DynFusionDeviceUsage did not pass threshord {0}", key);
                }
            }
        }

        public void NameDevice(ushort deviceNumber, string name)
        {
            UsageInfo value;
            if (UsageInfoDict.TryGetValue(string.Format("DEV:{0}", deviceNumber), out value))
                value.Name = name;
            else
                Debug.Console(1, this, "DynFusionDeviceUsage no device number {0}", deviceNumber);
        }

        public class UsageInfo
        {
            public DateTime StartTime;
            public string Name;
            public string Type;
            public uint SourceNumber;
            public UsageType UsageType;
            public ushort JoinNumber;
        }

        public enum UsageType : int
        {
            Device = 0,
            Display = 1,
            Source = 2
        };
    }
}