using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using UmdEssentials.Core.Bridges;
using UmdEssentials.Core.Config;

namespace UmdEssentials.Core.Routing
{
    public class RoutingInterface : IKeyName, IBridgeAdvanced
    {
        public string Key { get; private set; }

        public string Name
        {
            get;
            private
            set;
        }


        public ushort debugLevel;

        private ushort _advancedMode;

        public ushort AdvancedMode
        {
            get
            {
                if (_techPanel) return 1;

                return _advancedMode;
            }
            set
            {
                _advancedMode = value;
                AdvancedModeFeedback.FireUpdate();
            }
        }

        private readonly List<string> _visibleModes = new List<string>();
        private ushort _overflowMode;
        private bool _techPanel;

        public string RouterKey { get; private set; }
        private Router _router;
        private Source SelectedSource;
        private ushort _selectedSource;
        private ushort[] DisabledDests;
        private readonly CMutex mutex = new CMutex();
        private readonly CMutex visibilityModeMutex = new CMutex();
        private List<Route> PreviewRoutes;
        private bool allowRoutes;
        private readonly CTimer allowRoutesTimer;
        private ushort _easyModeSourceFb;

        public IntFeedback SourceSelectFeedback { get; private set; }
        public IntFeedback AdvancedModeFeedback { get; private set; }
        public IntFeedback OverflowModeFeedback { get; private set; }
        public IntFeedback EasyModeSourceFb { get; private set; }
        public Dictionary<uint, BoolFeedback> SourceVisibleFeedbacks { get; private set; }
        public Dictionary<uint, IntFeedback> SourceVisibleModeFeedbacks { get; private set; }
        public Dictionary<uint, BoolFeedback> DestVisibleFeedbacks { get; private set; }
        public Dictionary<uint, BoolFeedback> DestEnableFeedbacks { get; private set; }
        public Dictionary<uint, BoolFeedback> SourceAudioVisibleFeedbacks { get; private set; }
        public Dictionary<uint, BoolFeedback> SourceContentVisibleFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> SourceNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> SourceIconFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> DestNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> DestRouteNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> SourceDeviceKeyFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> SourceVideoSyncFeedbacks { get; private set; }

        public RoutingInterface(UiConfig uiConfig)
        {
            Key = uiConfig.Key + "-router";
            Name = uiConfig.Name + "-router";
            allowRoutesTimer = new CTimer(allowRoutesTimerCallback, Timeout.Infinite);
            ReadConfig(uiConfig);

            SourceSelectFeedback = new IntFeedback(() => _selectedSource);
            AdvancedModeFeedback = new IntFeedback(() => _advancedMode);
            OverflowModeFeedback = new IntFeedback(() => _overflowMode);
            EasyModeSourceFb = new IntFeedback(() => _easyModeSourceFb);

            SourceVisibleFeedbacks = new Dictionary<uint, BoolFeedback>();
            SourceVisibleModeFeedbacks = new Dictionary<uint, IntFeedback>();
            DestVisibleFeedbacks = new Dictionary<uint, BoolFeedback>();
            DestEnableFeedbacks = new Dictionary<uint, BoolFeedback>();
            SourceAudioVisibleFeedbacks = new Dictionary<uint, BoolFeedback>();
            SourceContentVisibleFeedbacks = new Dictionary<uint, BoolFeedback>();
            SourceNameFeedbacks = new Dictionary<uint, StringFeedback>();
            SourceIconFeedbacks = new Dictionary<uint, StringFeedback>();
            DestNameFeedbacks = new Dictionary<uint, StringFeedback>();
            DestRouteNameFeedbacks = new Dictionary<uint, StringFeedback>();
            SourceDeviceKeyFeedbacks = new Dictionary<uint, StringFeedback>();
            SourceVideoSyncFeedbacks = new Dictionary<uint, StringFeedback>();

            for (ushort i = 0; i <= RouterMain.maxSources; i++)
            {
                ushort sourceIndex = i;
                SourceVisibleFeedbacks[i] = new BoolFeedback(() => GetSourceVisibility(sourceIndex));
                SourceVisibleModeFeedbacks[i] = new IntFeedback(() => GetSourceVisibilityMode(sourceIndex));
                SourceAudioVisibleFeedbacks[i] = new BoolFeedback(() => GetSourceAudioVisibility(sourceIndex));
                SourceContentVisibleFeedbacks[i] = new BoolFeedback(() => GetSourceContentVisibility(sourceIndex));
                SourceNameFeedbacks[i] = new StringFeedback(() => GetSourceName(sourceIndex));
                SourceIconFeedbacks[i] = new StringFeedback(() => GetSourceIcon(sourceIndex));
                SourceDeviceKeyFeedbacks[i] = new StringFeedback(() => GetSourceDeviceKey(sourceIndex));
                SourceVideoSyncFeedbacks[i] = new StringFeedback(() => GetSourceVideoSyncKey(sourceIndex));
            }

            for (ushort i = 0; i <= RouterMain.maxDests; i++)
            {
                ushort destIndex = i;
                DestVisibleFeedbacks[i] = new BoolFeedback(() => GetDestVisibility(destIndex));
                DestEnableFeedbacks[i] = new BoolFeedback(() => GetDestEnabled(destIndex));
                DestNameFeedbacks[i] = new StringFeedback(() => GetDestName(destIndex));
                DestRouteNameFeedbacks[i] = new StringFeedback(() => GetDestRouteName(destIndex));
            }
        }

        public void ReadConfig(UiConfig uiConfig)
        {
            if (uiConfig.PreviewRoutes != null)
            {
                PreviewRoutes = uiConfig.PreviewRoutes;
                Debug.Console(0, "Preview routes found, count: {0}", PreviewRoutes.Count);
            }

            _techPanel = uiConfig.TechPanel ?? false;
        }


        private void RoomSelect(ushort roomIndex)
        {
            Register(string.Format("room0{0}-router", roomIndex));
        }

        public void Register(string newRouterKey)
        {
            mutex.WaitForMutex();
            try
            {
                if (newRouterKey == RouterKey || newRouterKey.Length < 2)
                {
                    if (debugLevel > 0) Debug.Console(0, "Ignoring new router key: {0}", newRouterKey);
                }
                else
                {
                    RouterKey = newRouterKey;
                    if (_router != null) _router.DestinationFeedbackChanged -= DestinationFeedbackChangedHandler;

                    _router = RouterMain.GetRouter(newRouterKey);
                    if (_router != null) _router.DestinationFeedbackChanged += DestinationFeedbackChangedHandler;

                    UpdateAllOutputs();
                }
            }
            catch (Exception ex)
            {
                Debug.ConsoleWithLog(0, "Routing interface register exception: {0}", ex);
                _router = null;
                UpdateAllOutputs();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        private void DestinationFeedbackChangedHandler(object sender, UshrtChangeEventArgs e)
        {
            UpdateDestinationFeedback(e.IntValue);
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            RoutingInterfaceJoinMap joinMap = new RoutingInterfaceJoinMap(joinStart);
            bridge.AddJoinMap(Key, joinMap);

            trilist.OnlineStatusChange += TrilistOnOnlineStatusChange;

            trilist.SetUShortSigAction(joinMap.SourceSelect.JoinNumber, SelectSource);
            trilist.SetUShortSigAction(joinMap.DestSelect.JoinNumber, SelectDest);
            trilist.SetUShortSigAction(joinMap.AdvancedMode.JoinNumber, SetAdvancedMode);
            trilist.SetUShortSigAction(joinMap.OverflowMode.JoinNumber, SetOverflowMode);
            trilist.SetUShortSigAction(joinMap.OverridePreview.JoinNumber, OverridePreview);
            trilist.SetUShortSigAction(joinMap.RoomSelect.JoinNumber, RoomSelect);
            trilist.SetUShortSigAction(joinMap.RoomActionGo.JoinNumber, FireAction);

            trilist.SetStringSigAction(joinMap.AddVisibilityMode.JoinNumber, AddVisibilityMode);
            trilist.SetStringSigAction(joinMap.RemoveVisibilityMode.JoinNumber, RemoveVisibilityMode);

            SourceSelectFeedback.LinkInputSig(trilist.UShortInput[joinMap.SourceSelect.JoinNumber]);
            AdvancedModeFeedback.LinkInputSig(trilist.UShortInput[joinMap.AdvancedMode.JoinNumber]);
            OverflowModeFeedback.LinkInputSig(trilist.UShortInput[joinMap.OverflowMode.JoinNumber]);
            EasyModeSourceFb.LinkInputSig(trilist.UShortInput[joinMap.EasyModeSourceFb.JoinNumber]);

            for (ushort i = 1; i <= RouterMain.maxSources; i++)
            {
                SourceVisibleFeedbacks[i]
                    .LinkInputSig(trilist.BooleanInput[joinMap.SourceVisible.JoinNumber + i - 1]);
                SourceVisibleModeFeedbacks[i]
                    .LinkInputSig(trilist.UShortInput[joinMap.SourceVisibleMode.JoinNumber + i - 1]);
                SourceAudioVisibleFeedbacks[i]
                    .LinkInputSig(trilist.BooleanInput[joinMap.SourceAudioVisible.JoinNumber + i - 1]);
                SourceContentVisibleFeedbacks[i]
                    .LinkInputSig(trilist.BooleanInput[joinMap.SourceContentVisible.JoinNumber + i - 1]);
                SourceNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.SourceName.JoinNumber + i - 1]);
                SourceIconFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.SourceIcon.JoinNumber + i - 1]);
                SourceDeviceKeyFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.SourceDeviceKey.JoinNumber + i - 1]);
                SourceVideoSyncFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.SourceVideoSyncKey.JoinNumber + i - 1]);
            }

            trilist.SetUShortSigAction(joinMap.DestRouteCmd.JoinNumber, s => MakeRoute(s, 0));

            for (ushort i = 1; i <= RouterMain.maxDests; i++)
            {
                ushort d = i;
                trilist.SetUShortSigAction(joinMap.DestRouteCmd.JoinNumber + i, s => MakeRoute(s, d));

                DestVisibleFeedbacks[i]
                    .LinkInputSig(trilist.BooleanInput[joinMap.DestVisible.JoinNumber + i - 1]);
                DestEnableFeedbacks[i]
                    .LinkInputSig(trilist.BooleanInput[joinMap.DestEnable.JoinNumber + i - 1]);
                DestNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.DestName.JoinNumber + i - 1]);
                DestRouteNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.DestRouteName.JoinNumber + i - 1]);
            }
        }

        private void TrilistOnOnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            Debug.Console(0, "Routing interface {0} trilist online status: {1}", Key, args.DeviceOnLine);

            if (!args.DeviceOnLine)
            {
                allowRoutesTimer.Stop();
                allowRoutes = false;
            }
            else
            {
                allowRoutesTimer.Reset(20000);
            }
        }

        private void allowRoutesTimerCallback(object o)
        {
            allowRoutes = true;
        }

        public void SetDebug(ushort val)
        {
            debugLevel = val;
        }

        public void UpdateAllOutputs()
        {
            try
            {
                SourceSelectFeedback.FireUpdate();
                AdvancedModeFeedback.FireUpdate();
                OverflowModeFeedback.FireUpdate();
                EasyModeSourceFb.FireUpdate();

                //Update all source feedback
                for (ushort i = 0;
                     i <= RouterMain.maxSources;
                     i++)
                    UpdateSourceFeedback(i);

                //Update all destination feedback
                for (ushort i = 0; i <= RouterMain.maxDests; i++) UpdateDestinationFeedback(i);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Routing interface update exception: {0}", ex);
            }
        }

        public void FireAction(ushort i)
        {
            if (_router != null) _router.FireAction(i);
        }

        public void SelectSource(ushort i)
        {
            if (debugLevel > 0)
                Debug.Console(0, "Routing interface {0} selecting source {1}, allow routes: {2}", Key, i, allowRoutes);

            if (!allowRoutes) return;
            if (_router != null && _router.Sources != null)
            {
                Source source = _router.Sources[i];
                if (source != null)
                {
                    SelectedSource = source;

                    if (PreviewRoutes != null) _router.MakeRoute(source.Routes, PreviewRoutes);

                    if (AdvancedMode == 0)
                    {
                        //In easy mode, make the route immediately
                        _router.RouteByIndex(i, 0);
                    }
                    else
                    {
                        //In advanced mode, set the source feedback based on the last selection
                        _selectedSource = i;
                        SourceSelectFeedback.FireUpdate();
                    }

                    if (!string.IsNullOrEmpty(source.disableDestinations))
                        //If source has disabled destinations defined, only enable some destinations
                        DisabledDests = SelectedSource.disableDestinations.Split(',').Select(x => ushort.Parse(x))
                            .ToArray();
                    else
                        DisabledDests = null;

                    for (ushort d = 0; d <= RouterMain.maxDests; d++) DestEnableFeedbacks[d].FireUpdate();
                }
            }
        }

        public void SelectDest(ushort i)
        {
            if (debugLevel > 0)
                Debug.Console(0, "Routing interface {0} selecting dest {1}, allow routes: {2}", Key, i, allowRoutes);

            if (!allowRoutes) return;

            if (AdvancedMode == 0)
                //In auto route mode, select dest does nothing
                return;

            if (_router != null && SelectedSource != null) _router.RouteByIndex(SelectedSource.Index, i);
        }

        private void MakeRoute(ushort source, ushort dest)
        {
            if (debugLevel > 0)
                Debug.Console(0, "Routing interface {0} making route from source: {1} to dest: {2}", Key, source, dest);

            if (allowRoutes && _router != null) _router.RouteByIndex(source, dest);
        }

        public void OverridePreview(ushort i)
        {
            if (!allowRoutes) return;

            if (PreviewRoutes != null && _router != null && _router.Sources != null)
            {
                Source source = _router.Sources[i];
                if (source != null)
                {
                    if (i > 0)
                        _router.MakeRoute(source.Routes, PreviewRoutes);
                    else
                        _router.MakeRoute(SelectedSource.Routes, PreviewRoutes);
                }
            }
        }

        public void UpdateSourceFeedback(ushort sourceIndex)
        {
            SourceVisibleFeedbacks[sourceIndex].FireUpdate();
            SourceVisibleModeFeedbacks[sourceIndex].FireUpdate();
            SourceAudioVisibleFeedbacks[sourceIndex].FireUpdate();
            SourceContentVisibleFeedbacks[sourceIndex].FireUpdate();
            SourceNameFeedbacks[sourceIndex].FireUpdate();
            SourceIconFeedbacks[sourceIndex].FireUpdate();
            SourceDeviceKeyFeedbacks[sourceIndex].FireUpdate();
            SourceVideoSyncFeedbacks[sourceIndex].FireUpdate();
        }


        public void UpdateDestinationFeedback(ushort destIndex)
        {
            try
            {
                if (_router != null && _router.Dests != null && _router.Dests.ContainsKey(destIndex))
                {
                    Dest dest = _router.Dests[destIndex];

                    if (destIndex == 0)
                    {
                        if (AdvancedMode == 0)
                        {
                            //Check if this is the auto route destination and easy mode is on.
                            //If so, update the source select feedback
                            _selectedSource = dest.FeedbackIndex ?? 0;
                            SourceSelectFeedback.FireUpdate();
                        }

                        _easyModeSourceFb = dest.FeedbackIndex ?? 0;
                        EasyModeSourceFb.FireUpdate();
                    }
                }

                DestVisibleFeedbacks[destIndex].FireUpdate();
                DestNameFeedbacks[destIndex].FireUpdate();
                DestRouteNameFeedbacks[destIndex].FireUpdate();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Routing interface dest: {0} feedback exception: {1}", destIndex, ex);
            }
        }

        private bool GetSourceVisibility(ushort sourceIndex)
        {
            if (_router != null && _router.Sources != null && _router.Sources.ContainsKey(sourceIndex))
            {
                Source source = _router.Sources[sourceIndex];

                //Check for tech panel mode
                if (_techPanel) return source.techVisible ?? false;

                //Check for visible mode defined but not enabled
                if (!string.IsNullOrEmpty(source.visibleMode) &&
                    !_visibleModes.Contains(source.visibleMode.ToLower()))
                    return false;

                //Check for an overflow source with overflow disabled
                if (_overflowMode == 0 && source.Overflow == true) return false;

                //Determine source visibility depending on config setting and if advanced mode is enabled
                if (source.EasyModeVisible == true && AdvancedMode == 0) return true;

                if (source.AdvancedModeVisible == true && AdvancedMode == 1) return true;
            }

            return false;
        }

        // 0)not visible, 1)easy mode only, 2)advanced mode only, 3)easy and advanced mode
        private ushort GetSourceVisibilityMode(ushort sourceIndex)
        {
            if (_router != null && _router.Sources != null && _router.Sources.ContainsKey(sourceIndex))
            {
                Source source = _router.Sources[sourceIndex];

                bool visible;

                //Check for tech panel mode
                if (_techPanel)
                    visible = source.techVisible ?? false;

                //Check for visible mode defined but not enabled
                else if (!string.IsNullOrEmpty(source.visibleMode) &&
                         !_visibleModes.Contains(source.visibleMode.ToLower()))
                    visible = false;

                //Check for an overflow source with overflow disabled
                else if (_overflowMode == 0 && source.Overflow == true)
                    visible = false;
                else
                    visible = true;

                if (visible)
                {
                    if (source.EasyModeVisible == true && source.AdvancedModeVisible == true) return 3;

                    if (source.AdvancedModeVisible == true) return 2;

                    if (source.EasyModeVisible == true) return 1;
                }
            }

            return 0;
        }

        private string GetSourceName(ushort sourceIndex)
        {
            if (_router != null && _router.Sources != null && _router.Sources.ContainsKey(sourceIndex))
                return _router.Sources[sourceIndex].Name ?? "";

            return "";
        }

        private string GetSourceIcon(ushort sourceIndex)
        {
            if (_router != null && _router.Sources != null && _router.Sources.ContainsKey(sourceIndex))
                return _router.Sources[sourceIndex].Icon ?? "";

            return "";
        }

        private string GetSourceDeviceKey(ushort sourceIndex)
        {
            if (_router != null && _router.Sources != null && _router.Sources.ContainsKey(sourceIndex))
                return _router.Sources[sourceIndex].DeviceKey != null
                    ? _router.Sources[sourceIndex].DeviceKey.ToLower()
                    : "";

            return "";
        }

        private string GetSourceVideoSyncKey(ushort sourceIndex)
        {
            if (_router != null && _router.Sources != null && _router.Sources.ContainsKey(sourceIndex))
                return _router.Sources[sourceIndex].VideoSyncKey ?? "";

            return "";
        }

        private bool GetSourceAudioVisibility(ushort sourceIndex)
        {
            if (_router != null && _router.Sources != null && _router.Sources.ContainsKey(sourceIndex))
                return _router.Sources[sourceIndex].HasAudio && AdvancedMode == 1;

            return false;
        }

        private bool GetSourceContentVisibility(ushort sourceIndex)
        {
            if (_router != null && _router.Sources != null && _router.Sources.ContainsKey(sourceIndex))
                return _router.Sources[sourceIndex].ContentVisible;

            return false;
        }

        private string GetDestName(ushort destIndex)
        {
            if (_router != null && _router.Dests != null && _router.Dests.ContainsKey(destIndex))
                return _router.Dests[destIndex].Name ?? "";

            return "";
        }

        private string GetDestRouteName(ushort destIndex)
        {
            if (_router != null && _router.Dests != null && _router.Dests.ContainsKey(destIndex))
                return _router.Dests[destIndex].FeedbackName ?? "";

            return "";
        }

        private bool GetDestVisibility(ushort destIndex)
        {
            if (_router != null && _router.Dests != null && _router.Dests.ContainsKey(destIndex))
            {
                Dest dest = _router.Dests[destIndex];

                //Check for tech panel mode
                if (_techPanel) return dest.techVisible ?? false;

                //Check for visible mode defined but not enabled
                if (!string.IsNullOrEmpty(dest.visibleMode) &&
                    !_visibleModes.Contains(dest.visibleMode.ToLower()))
                    return false;

                //Check for overflow dest with overflow disabled
                if (_overflowMode == 0 && dest.Overflow == true) return false;

                if (dest.Visible == true) return true;
            }

            return false;
        }

        private bool GetDestEnabled(ushort destIndex)
        {
            if (_router != null && _router.Dests != null && _router.Dests.ContainsKey(destIndex))
            {
                if (SelectedSource == null)
                    //If no source selected, disable all destinations
                    return false;

                if (DisabledDests != null && DisabledDests.Contains(destIndex))
                    //Found match, disable destination
                    return false;

                return true;
            }

            return false;
        }

        private void UpdateAllSourceVisibility()
        {
            foreach (BoolFeedback boolFeedback in SourceVisibleFeedbacks.Values) boolFeedback.FireUpdate();

            foreach (IntFeedback intFeedback in SourceVisibleModeFeedbacks.Values) intFeedback.FireUpdate();
        }

        private void UpdateAllDestVisibility()
        {
            foreach (BoolFeedback boolFeedback in DestVisibleFeedbacks.Values) boolFeedback.FireUpdate();
        }

        public void SetAdvancedMode(ushort mode)
        {
            AdvancedMode = mode;

            UpdateAllSourceVisibility();
            //Update all source audio visible feedback
            for (ushort i = 0;
                 i <= RouterMain.maxSources;
                 i++)
                SourceAudioVisibleFeedbacks[i].FireUpdate();

            if (_router != null && _router.Dests.ContainsKey(0))
            {
                Dest dest = _router.Dests[0];
                if (dest.FeedbackIndex != null)
                {
                    if (AdvancedMode == 0)
                    {
                        //If switching to auto route mode, set source feedback to current true feedback (likely none because nothing matches)
                        _selectedSource = (ushort)dest.FeedbackIndex;
                        SourceSelectFeedback.FireUpdate();
                    }
                    else
                    {
                        //If switching to manual mode, select the most recent true feedback
                        SelectSource((ushort)dest.FeedbackIndex);
                    }
                }
                else
                {
                    if (AdvancedMode == 0)
                    {
                        _selectedSource = 0;
                        SourceSelectFeedback.FireUpdate();
                    }
                    else
                    {
                        SelectSource(0);
                    }
                }
            }
        }

        public void AddVisibilityMode(string mode)
        {
            visibilityModeMutex.WaitForMutex();
            try
            {
                if (mode.Length > 0 && !_visibleModes.Contains(mode.ToLower()))
                {
                    _visibleModes.Add(mode.ToLower());
                    if (_router != null)
                    {
                        UpdateAllSourceVisibility();
                        UpdateAllDestVisibility();
                    }
                }
            }
            finally
            {
                visibilityModeMutex.ReleaseMutex();
            }
        }

        public void RemoveVisibilityMode(string mode)
        {
            visibilityModeMutex.WaitForMutex();
            try
            {
                if (_visibleModes.Contains(mode.ToLower()))
                {
                    _visibleModes.RemoveAll(x => x == mode.ToLower());
                    if (_router != null)
                    {
                        UpdateAllSourceVisibility();
                        UpdateAllDestVisibility();
                    }
                }
            }
            finally
            {
                visibilityModeMutex.ReleaseMutex();
            }
        }

        public void SetOverflowMode(ushort mode)
        {
            _overflowMode = mode;
            OverflowModeFeedback.FireUpdate();
            if (_router != null)
            {
                UpdateAllSourceVisibility();
                UpdateAllDestVisibility();
            }
        }
    }
}