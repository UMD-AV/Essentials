using System;
using System.Linq;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Core.Routing
{
    public class Router : IKeyName, IBridgeAdvanced
    {
        public ushort debugLevel = 1;
        public bool fakeFeedback = false;
        public string Key { get; private set; }
        public string Name { get; private set; }
        public ushort Overflow { get; set; }
        public Dictionary<ushort, Source> Sources { get; private set; }
        public Dictionary<ushort, Dest> Dests { get; private set; }
        private Dictionary<ushort, RoutingAction> Actions;

        private List<KeyValuePair<ushort, SourceFeedback>> SourceFeedbacks;
        private List<KeyValuePair<ushort, DestFeedback>> DestFeedbacks;
        private List<ushort> PreviouslyUsedAudioIndexes;
        private List<ushort> SourceFeedbackToProcess;
        private List<ushort> DestFeedbackToProcess;
        private List<ushort> AudioFeedbackToProcess;
        private readonly CMutex FeedbackMutex;
        private readonly CTimer FeedbackTimer;

        private IRoutingNumericWithFeedback switcher01;
        private IRoutingNumericWithFeedback switcher02;
        private IRoutingNumericWithFeedback switcher03;
        private readonly Dictionary<ushort, IRoutingNumericWithFeedback> txs;

        private RoomRouterJoinMap joinMap;
        private BasicTriList routerTriList;
        private bool allowRoutes;
        private readonly CTimer allowRoutesTimer;

        public Dictionary<uint, IntFeedback> CurrentRouteFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> SourceDeviceKeyFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> DestDeviceKeyFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> CurrentRouteNameFeedbacks { get; private set; }

        public Router(RoomConfig roomConfig)
        {
            Key = roomConfig.Key + "-router";
            Name = roomConfig.RoomName + "-router";
            FeedbackMutex = new CMutex();
            FeedbackTimer = new CTimer(FeedbackTimerCallback, Timeout.Infinite);
            allowRoutesTimer = new CTimer(allowRoutesTimerCallback, Timeout.Infinite);
            RouterMain.AddRouter(Key, this);

            txs = new Dictionary<ushort, IRoutingNumericWithFeedback>();

            CurrentRouteFeedbacks = new Dictionary<uint, IntFeedback>();
            SourceDeviceKeyFeedbacks = new Dictionary<uint, StringFeedback>();
            DestDeviceKeyFeedbacks = new Dictionary<uint, StringFeedback>();
            CurrentRouteNameFeedbacks = new Dictionary<uint, StringFeedback>();

            for (ushort i = 0; i <= RouterMain.maxSources; i++)
            {
                ushort index = i;
                SourceDeviceKeyFeedbacks[i] = new StringFeedback(() => GetSourceDeviceKey(index));
            }

            for (ushort i = 0; i <= RouterMain.maxDests; i++)
            {
                ushort index = i;
                CurrentRouteFeedbacks[i] = new IntFeedback(() => GetRouteFeedback(index));
                CurrentRouteNameFeedbacks[i] = new StringFeedback(() => GetRouteName(index));
                DestDeviceKeyFeedbacks[i] = new StringFeedback(() => GetDestDeviceKey(index));
            }

            ReadConfig(roomConfig);
        }

        private string GetSourceDeviceKey(ushort index)
        {
            if (Sources != null && Sources.ContainsKey(index))
            {
                return Sources[index].DeviceKey ?? "";
            }

            return "";
        }

        private ushort GetRouteFeedback(ushort index)
        {
            if (Dests != null && Dests.ContainsKey(index))
            {
                return Dests[index].FeedbackIndex ?? 0;
            }

            return 0;
        }

        private string GetRouteName(ushort index)
        {
            if (Dests != null && Dests.ContainsKey(index))
            {
                return Dests[index].FeedbackName ?? "";
            }

            return "";
        }

        private string GetDestDeviceKey(ushort index)
        {
            if (Dests != null && Dests.ContainsKey(index))
            {
                return Dests[index].DeviceKey ?? "";
            }

            return "";
        }

        public void SetOverflow(ushort val)
        {
            Overflow = val;
        }

        private IRoutingNumericWithFeedback GetSwitcher(string key)
        {
            IKeyed dev = DeviceManager.GetDeviceForKey(key);
            if (dev is IRoutingNumericWithFeedback)
            {
                Debug.Console(0, "Found IRoutingNumericWithFeedback {0}", key);
                return (IRoutingNumericWithFeedback)dev;
            }

            return null;
        }

        private IRoutingNumericWithFeedback GetTx(string key)
        {
            IKeyed dev = DeviceManager.GetDeviceForKey(key);
            if (dev is IRoutingNumericWithFeedback)
            {
                Debug.Console(0, "Found IRoutingNumericWithFeedback {0}", key);
                return (IRoutingNumericWithFeedback)dev;
            }

            return null;
        }

        public void ReadConfig(RoomConfig roomConfig)
        {
            //Build new things
            Sources = new Dictionary<ushort, Source>();
            Dests = new Dictionary<ushort, Dest>();
            Actions = new Dictionary<ushort, RoutingAction>();
            SourceFeedbacks = new List<KeyValuePair<ushort, SourceFeedback>>();
            DestFeedbacks = new List<KeyValuePair<ushort, DestFeedback>>();
            SourceFeedbackToProcess = new List<ushort>();
            DestFeedbackToProcess = new List<ushort>();
            PreviouslyUsedAudioIndexes = new List<ushort>();
            AudioFeedbackToProcess = new List<ushort>();

            if (switcher01 != null)
                switcher01.NumericSwitchChange -= Switcher01OnNumericSwitchChange;
            switcher01 = GetSwitcher("switcher01");
            if (switcher01 != null)
                switcher01.NumericSwitchChange += Switcher01OnNumericSwitchChange;

            if (switcher02 != null)
                switcher02.NumericSwitchChange -= Switcher02OnNumericSwitchChange;
            switcher02 = GetSwitcher("switcher02");
            if (switcher02 != null)
                switcher02.NumericSwitchChange += Switcher02OnNumericSwitchChange;

            if (switcher03 != null)
                switcher03.NumericSwitchChange -= Switcher03OnNumericSwitchChange;
            switcher03 = GetSwitcher("switcher03");
            if (switcher03 != null)
                switcher03.NumericSwitchChange += Switcher03OnNumericSwitchChange;

            for (ushort i = 1; i <= RouterMain.maxSources; i++)
            {
                if (txs.ContainsKey(i) && txs[i] != null)
                {
                    txs[i].NumericSwitchChange -= TxOnNumericSwitchChange;
                }

                if (i < 10)
                {
                    txs[i] = GetTx("tx0" + i);
                }
                else
                {
                    txs[i] = GetTx("tx" + i);
                }

                if (txs.ContainsKey(i) && txs[i] != null)
                {
                    txs[i].NumericSwitchChange += TxOnNumericSwitchChange;
                }
            }

            //Process Sources
            if (roomConfig.Sources != null)
            {
                foreach (Source source in roomConfig.Sources)
                {
                    try
                    {
                        Sources.Add(source.Index, source);
                        if (source.Routes != null)
                        {
                            foreach (Route route in source.Routes)
                            {
                                Debug.Console(0, "Loading route: {0}-{1}", source.Name, route.RouteKey);
                                //Check the source for routes that are marked UseForFeedback
                                if (route.UseForFeedback == true && route.Output != null && route.Input != null)
                                {
                                    SourceFeedbacks.Add(new KeyValuePair<ushort, SourceFeedback>(source.Index,
                                        new SourceFeedback(route.RouteKey, (ushort)route.Output, (ushort)route.Input)));
                                }

                                if ((route.RouteKey == "audioRoute" || route.RouteKey == "audioRoute2" ||
                                     route.RouteKey == "audioRoute3") &&
                                    route.Input != null)
                                {
                                    if (!PreviouslyUsedAudioIndexes.Contains((ushort)route.Input))
                                    {
                                        //Only enable audio routing visibility if audioRoute exists and hasn't been used yet
                                        PreviouslyUsedAudioIndexes.Add(route.Input.Value);
                                        source.HasAudio = true;
                                    }
                                }

                                source.ContentVisible = (route.RouteKey == "contentRoute" && route.Input != null);
                            }
                        }

                        if (debugLevel > 0)
                        {
                            Debug.Console(0, "Source loaded: {0}", source.Name);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.ConsoleWithLog(0, "Exception loading {0}: {1}", source.Name, e.Message);
                    }
                }
            }

            //Process Dests
            if (Dests != null)
            {
                foreach (Dest dest in roomConfig.Dests)
                {
                    try
                    {
                        Dests.Add(dest.Index, dest);
                        if (dest.Routes != null)
                        {
                            foreach (Route route in dest.Routes)
                            {
                                Debug.Console(0, "Loading route: {0}-{1}", dest.Name, route.RouteKey);
                                //Check the destination for routes that are marked UseForFeedback
                                if (route.UseForFeedback == true && route.Output != null)
                                {
                                    string feedbackKey = route.RouteKey;
                                    int dashLoc = route.RouteKey.IndexOf('-');
                                    if (dashLoc > 0)
                                    {
                                        feedbackKey = route.RouteKey.Substring(0, dashLoc);
                                        if (debugLevel > 0)
                                        {
                                            Debug.Console(0, "Found '-' in dest feedback, new feedback key is {0}",
                                                route.RouteKey);
                                        }
                                    }

                                    DestFeedbacks.Add(new KeyValuePair<ushort, DestFeedback>(dest.Index,
                                        new DestFeedback(feedbackKey, (ushort)route.Output, route.Input,
                                            route.DisableInOverflow, route.EnableInOverflow)));
                                }
                            }
                        }

                        if (debugLevel > 0)
                        {
                            Debug.Console(0, "Dest loaded: {0}", dest.Name);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.ConsoleWithLog(0, "Exception loading {0}: {1}", dest.Name, e.Message);
                    }
                }
            }

            //Process Actions
            if (Actions != null)
            {
                if (debugLevel > 0)
                {
                    foreach (RoutingAction action in roomConfig.Actions)
                    {
                        Actions.Add(action.Index, action);
                        Debug.Console(0, "Action loaded: {0}", action.Index);
                    }
                }
            }

            //Finalize initialization
            UpdateAllFeedback();
        }

        private void Switcher01OnNumericSwitchChange(object sender, RoutingNumericEventArgs e)
        {
            if (e.SigType == eRoutingSignalType.Video || e.SigType == eRoutingSignalType.AudioVideo)
                FeedbackFromSimpl("videoRoute", e.Output, e.Input);
            if (e.SigType == eRoutingSignalType.Audio || e.SigType == eRoutingSignalType.AudioVideo)
                FeedbackFromSimpl("audioRoute", e.Output, e.Input);
        }

        private void Switcher02OnNumericSwitchChange(object sender, RoutingNumericEventArgs e)
        {
            if (e.SigType == eRoutingSignalType.Video || e.SigType == eRoutingSignalType.AudioVideo)
                FeedbackFromSimpl("videoRoute2", e.Output, e.Input);
            if (e.SigType == eRoutingSignalType.Audio || e.SigType == eRoutingSignalType.AudioVideo)
                FeedbackFromSimpl("audioRoute2", e.Output, e.Input);
        }

        private void Switcher03OnNumericSwitchChange(object sender, RoutingNumericEventArgs e)
        {
            if (e.SigType == eRoutingSignalType.Video || e.SigType == eRoutingSignalType.AudioVideo)
                FeedbackFromSimpl("videoRoute3", e.Output, e.Input);
            if (e.SigType == eRoutingSignalType.Audio || e.SigType == eRoutingSignalType.AudioVideo)
                FeedbackFromSimpl("audioRoute3", e.Output, e.Input);
        }

        private void TxOnNumericSwitchChange(object sender, RoutingNumericEventArgs e)
        {
            IKeyed ikeySender = sender as IKeyed;
            if (ikeySender != null)
            {
                FeedbackFromSimpl(ikeySender.Key, e.Output, e.Input);
            }
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            joinMap = new RoomRouterJoinMap(joinStart);
            routerTriList = trilist;

            trilist.OnlineStatusChange += TrilistOnOnlineStatusChange;

            bridge.AddJoinMap(Key, joinMap);

            trilist.SetUShortSigAction(joinMap.RoomActionGo.JoinNumber, FireAction);
            trilist.SetUShortSigAction(joinMap.CodecInputFb.JoinNumber, UpdateCodec);

            for (ushort i = 1; i <= RouterMain.numDisplays; i++)
            {
                ushort display = i;
                trilist.SetUShortSigAction(joinMap.DisplayInputFb.JoinNumber + i - 1, o => UpdateDisplay(o, display));
            }


            for (ushort i = 1; i <= RouterMain.maxSources; i++)
            {
                SourceDeviceKeyFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.SourceDevKey.JoinNumber + i - 1]);
            }

            for (ushort i = 1; i <= RouterMain.maxDests; i++)
            {
                ushort offset = i;
                trilist.SetUShortSigAction(joinMap.RoutingInput.JoinNumber + i - 1,
                    o => RouteByIndex(o, offset));

                DestDeviceKeyFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.DestDevKey.JoinNumber + i - 1]);
                CurrentRouteFeedbacks[i]
                    .LinkInputSig(trilist.UShortInput[joinMap.RoutingInput.JoinNumber + i - 1]);
                CurrentRouteNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.CurrentSource.JoinNumber + i - 1]);
            }
        }

        private void TrilistOnOnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            if (debugLevel > 0)
            {
                Debug.Console(0, "Router {0} trilist online status: {1}", Key, args.DeviceOnLine);
            }

            if (!args.DeviceOnLine)
            {
                allowRoutesTimer.Stop();
                allowRoutes = false;
            }
            else
            {
                allowRoutesTimer.Reset(5000);
            }
        }

        private void allowRoutesTimerCallback(object o)
        {
            allowRoutes = true;
        }

        private void UpdateAllFeedback()
        {
            for (ushort i = 0; i <= RouterMain.maxSources; i++)
            {
                SourceDeviceKeyFeedbacks[i].FireUpdate();
            }

            for (ushort i = 0; i <= RouterMain.maxDests; i++)
            {
                DestDeviceKeyFeedbacks[i].FireUpdate();
                CurrentRouteFeedbacks[i].FireUpdate();
                CurrentRouteNameFeedbacks[i].FireUpdate();
            }
        }

        public void FireAction(ushort actionIndex)
        {
            if (!allowRoutes) return;
            if (Actions != null)
            {
                RoutingAction action = Actions[actionIndex];
                if (action != null && action.Routes != null)
                {
                    foreach (Route route in action.Routes)
                    {
                        //Don't route if marked disabled in overflow and overflow is on
                        if (route.DisableInOverflow == true && Overflow != 0)
                            continue;

                        //Don't route if marked enabled in overflow and overflow is off
                        if (route.EnableInOverflow == true && Overflow == 0)
                            continue;

                        //Check that route has an input and an output
                        if (route.Input != null && route.Output != null)
                        {
                            if (route.DelaySeconds != null && route.DelaySeconds > 0)
                            {
                                CrestronEnvironment.Sleep((int)route.DelaySeconds * 1000);
                            }

                            switch (route.RouteKey)
                            {
                                //Special case to make a source -> dest route
                                case "route":
                                {
                                    if (debugLevel > 0)
                                    {
                                        Debug.Console(0, "Making route from source {0} to dest {1}", route.Input,
                                            route.Output);
                                    }

                                    RouteByIndex((ushort)route.Input, (ushort)route.Output);
                                    break;
                                }
                                default:
                                {
                                    if (debugLevel > 0)
                                    {
                                        Debug.Console(0, "Making {0} from input {1} to output {2}", route.RouteKey,
                                            route.Input,
                                            route.Output);
                                    }

                                    MakeDeviceRoute(route.RouteKey, (ushort)route.Output, (ushort)route.Input);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public void RouteByIndex(ushort sourceIndex, ushort destIndex)
        {
            if (!allowRoutes) return;
            // verify that dictionaries contain source index and dest index before making route
            Source source = Sources[sourceIndex];
            Dest dest = Dests[destIndex];
            if (source != null && dest != null)
            {
                if (Overflow == 0 && dest.Overflow == true)
                {
                    //Dont route if overflow dest and not in overflow mode
                    return;
                }

                if (debugLevel > 0)
                {
                    Debug.Console(0, "Routing {0} to {1}", source.Name, dest.Name);
                }

                if (source.Routes != null)
                {
                    MakeRoute(source.Routes, dest.Routes);
                }
            }
        }

        public void MakeRoute(List<Route> sourceRoutes, List<Route> destRoutes)
        {
            if (!allowRoutes) return;
            //First merge the source and dest routes
            List<Route> mergedRoutes = new List<Route>();
            destRoutes.ForEach(x => { mergedRoutes.Add(x.Copy()); });

            if (sourceRoutes == null)
            {
                return;
            }

            foreach (Route sourceRoute in sourceRoutes)
            {
                //Source has both input and output defined, look for destRoute to overwrite, otherwise add as a new route
                if (sourceRoute.Input.HasValue && sourceRoute.Output.HasValue)
                {
                    bool match = false;
                    foreach (Route destRoute in mergedRoutes)
                    {
                        if (sourceRoute.RouteKey == destRoute.RouteKey && sourceRoute.Output == destRoute.Output)
                        {
                            //Found a key and output match
                            destRoute.Input = sourceRoute.Input.Value;
                            destRoute.EnableInOverflow = (destRoute.EnableInOverflow == true ||
                                                          sourceRoute.EnableInOverflow == true);
                            destRoute.DisableInOverflow = (destRoute.DisableInOverflow == true ||
                                                           sourceRoute.DisableInOverflow == true);
                            match = true;
                        }
                    }

                    //If no match, add as a new route
                    if (!match)
                    {
                        mergedRoutes.Add(sourceRoute);
                    }
                }
                else if (sourceRoute.Input.HasValue)
                {
                    foreach (Route destRoute in mergedRoutes)
                    {
                        if (sourceRoute.RouteKey == destRoute.RouteKey)
                        {
                            //Found a key match
                            if (debugLevel > 0)
                            {
                                Debug.Console(0, "Setting {0} input to {1}", destRoute.RouteKey, sourceRoute.Input);
                            }

                            destRoute.Input = sourceRoute.Input.Value;
                        }
                    }
                }
            }

            //Now make the routes
            foreach (Route route in mergedRoutes)
            {
                //Don't route if marked disabled in overflow and overflow is on
                if (route.DisableInOverflow == true && Overflow != 0)
                    continue;

                //Don't route if marked enabled in overflow and overflow is off
                if (route.EnableInOverflow == true && Overflow == 0)
                    continue;

                //Check that route has an input and an output
                if (route.Input.HasValue && route.Output.HasValue)
                {
                    string routeKey = route.RouteKey.Contains("-")
                        ? route.RouteKey.Substring(0, route.RouteKey.IndexOf("-", StringComparison.Ordinal))
                        : route.RouteKey;

                    if (debugLevel > 0)
                    {
                        Debug.Console(0, "Making {0} from input {1} to output {2}", routeKey, route.Input,
                            route.Output);
                    }

                    MakeDeviceRoute(routeKey, (ushort)route.Output, (ushort)route.Input);
                }
            }
        }

        private void UpdateCodec(ushort input)
        {
            FeedbackFromSimpl("contentRoute", 1, input);
        }

        private void UpdateDisplay(ushort input, ushort display)
        {
            FeedbackFromSimpl("displayRoute", display, input);
        }

        public void FeedbackFromSimpl(string key, uint? output, uint? input)
        {
            if (output == null)
                output = 0;
            if (input == null)
                input = 0;

            //Check each feedback in the source list for matching key and index
            foreach (KeyValuePair<ushort, SourceFeedback> feedback in SourceFeedbacks)
            {
                //If key and index match, update the stored feedback value
                if (feedback.Value.RouteKey == key && feedback.Value.Output == output)
                {
                    feedback.Value.FeedbackState = (feedback.Value.Input == input);
                    FeedbackMutex.WaitForMutex();
                    try
                    {
                        if (!SourceFeedbackToProcess.Contains(feedback.Key))
                        {
                            SourceFeedbackToProcess.Add(feedback.Key);
                            FeedbackTimer.Reset(100);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.ConsoleWithLog(0, "Exception in router feedback processing: {0}", ex);
                    }
                    finally
                    {
                        FeedbackMutex.ReleaseMutex();
                    }
                }
            }

            //Check each feedback in the dest list for matching key and index
            foreach (KeyValuePair<ushort, DestFeedback> feedback in DestFeedbacks)
            {
                //If key and index match, update the stored feedback value
                if (feedback.Value.RouteKey == key && feedback.Value.Output == output)
                {
                    feedback.Value.FeedbackInput = (ushort)input;
                    FeedbackMutex.WaitForMutex();
                    try
                    {
                        if (!DestFeedbackToProcess.Contains(feedback.Key))
                        {
                            DestFeedbackToProcess.Add(feedback.Key);
                            FeedbackTimer.Reset(100);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.ConsoleWithLog(0, "Exception in router feedback processing: {0}", ex);
                    }
                    finally
                    {
                        FeedbackMutex.ReleaseMutex();
                    }
                }
            }
        }

        private void FeedbackTimerCallback(object o)
        {
            if (debugLevel > 0)
            {
                Debug.Console(0, "Feedback timer callback");
            }

            FeedbackMutex.WaitForMutex();
            try
            {
                foreach (ushort feedbackIndex in SourceFeedbackToProcess)
                {
                    RecalculateSourceFeedback(feedbackIndex);
                }

                foreach (ushort feedbackIndex in DestFeedbackToProcess)
                {
                    RecalculateDestFeedback(feedbackIndex);
                }
            }
            catch (Exception ex)
            {
                Debug.ConsoleWithLog(0, "Exception in router feedback timer: {0}", ex);
            }
            finally
            {
                SourceFeedbackToProcess.Clear();
                DestFeedbackToProcess.Clear();
                FeedbackTimer.Reset(Timeout.Infinite);
                FeedbackMutex.ReleaseMutex();
            }
        }

        private void RecalculateSourceFeedback(ushort sourceIndex)
        {
            bool newFeedbackState = true;
            foreach (KeyValuePair<ushort, SourceFeedback> feedback in SourceFeedbacks)
            {
                if (feedback.Key == sourceIndex)
                {
                    if (feedback.Value.FeedbackState == false)
                    {
                        newFeedbackState = false;
                    }
                }
            }

            Source source = Sources[sourceIndex];
            if (source != null)
            {
                source.FeedbackState = newFeedbackState;
            }

            //Recheck all destinations
            foreach (Dest dest in Dests.Values)
            {
                //Find if destination is currently on this source and new source feedback is false
                if (!newFeedbackState == false && dest.FeedbackIndex == sourceIndex)
                {
                    RecalculateDestFeedback(dest.Index);
                }
                //Find if destination is currently not on this source and new source feedback is true
                else if (newFeedbackState && dest.FeedbackIndex != sourceIndex)
                {
                    RecalculateDestFeedback(dest.Index);
                }
            }
        }

        private void RecalculateDestFeedback(ushort destIndex)
        {
            //First, select all feedback requirements for this destination
            List<KeyValuePair<ushort, DestFeedback>> destFeedbacks =
                DestFeedbacks.Where(x => x.Key == destIndex).Select(y => y).ToList();

            Source sourceMatch = null; //Tracks the best matching source

            //Now start looking through sources for a match
            foreach (Source source in Sources.Values.Where(x => x.Routes != null))
            {
                bool match = false; //match is used to track this source feedback while iterating through objects.

                try
                {
                    if (destFeedbacks.Count > 0)
                    {
                        //Set to true and only change to false on failure
                        match = true;
                    }

                    foreach (KeyValuePair<ushort, DestFeedback> feedback in destFeedbacks)
                    {
                        if (Overflow != 0 && feedback.Value.DisableInOverflow == true)
                        {
                            continue;
                        }

                        if (Overflow == 0 && feedback.Value.EnableInOverflow == true)
                        {
                            continue;
                        }

                        bool routeMatch = false;
                        foreach (Route sourceRoute in source.Routes)
                        {
                            if (Overflow != 0 && sourceRoute.DisableInOverflow == true)
                            {
                                continue;
                            }

                            if (Overflow == 0 && sourceRoute.EnableInOverflow == true)
                            {
                                continue;
                            }

                            if (sourceRoute.RouteKey == feedback.Value.RouteKey)
                            {
                                //If the route key matches, next check that this isn't for some other output
                                if (sourceRoute.Output == null)
                                {
                                    //Found a matching route. If input values match, set routeMatch=true
                                    routeMatch = (sourceRoute.Input == feedback.Value.FeedbackInput);
                                }
                                else if (sourceRoute.Output == feedback.Value.Output)
                                {
                                    //Found a matching route. If input values match, set routeMatch=true
                                    routeMatch = (sourceRoute.Input == feedback.Value.FeedbackInput);
                                    //Stop searching for this particular feedback condition
                                    break;
                                }
                            }
                        }

                        //Check routes only on dest if the previous step failed
                        if (routeMatch == false && feedback.Value.Input != null)
                        {
                            if (debugLevel > 0)
                            {
                                Debug.Console(0, "Found matching dest only feedback for {0}: feedback value: {1}",
                                    feedback.Value.RouteKey, feedback.Value.FeedbackInput);
                            }

                            routeMatch = (feedback.Value.Input == feedback.Value.FeedbackInput);
                        }

                        if (routeMatch == false)
                        {
                            //Stop checking feedback once one feedback match has failed
                            if (debugLevel > 0)
                            {
                                Debug.Console(0, "Router feedback match for {0} failed at {1}", source.Name,
                                    feedback.Value.RouteKey);
                            }

                            match = false;
                            break;
                        }
                    }

                    //Check if this source was a match for all possibilities
                    if (match)
                    {
                        sourceMatch = source;

                        //Now check if the source's own feedback state is true (or unknown)
                        if (source.FeedbackState != false)
                        {
                            //Found a perfect match, no need to continue
                            if (debugLevel > 0)
                            {
                                Debug.Console(0, "Router found matching feedback for {0}: {1}", Dests[destIndex].Name,
                                    sourceMatch.Name);
                            }

                            UpdateRouteFeedback(destIndex, sourceMatch);
                            return;
                        }

                        if (debugLevel > 0)
                        {
                            CrestronConsole.PrintLine(
                                "Router found matching feedback but source feedback wasn't valid {0}: {1}",
                                Dests[destIndex].Name,
                                sourceMatch.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.ConsoleWithLog(0, "Exception checking {0} for feedback from {1}: {2}", Dests[destIndex].Name,
                        source.Name, ex);
                }
            }

            //Nothing matched perfectly, check if something partially matched
            if (sourceMatch != null)
            {
                if (debugLevel > 0)
                {
                    CrestronConsole.PrintLine("Router found partial matching feedback for {0}: {1}",
                        Dests[destIndex].Name,
                        sourceMatch.Name);
                }

                UpdateRouteFeedback(destIndex, sourceMatch);
                return;
            }

            if (debugLevel > 0)
            {
                Debug.Console(0, "Router found no sources that match feedback for {0}", Dests[destIndex].Name);
            }

            ClearRouteFeedback(destIndex);
        }

        private void ClearRouteFeedback(ushort destIndex)
        {
            Dest dest = Dests[destIndex];
            if (dest == null)
            {
                return;
            }

            dest.FeedbackName = "Off";
            dest.FeedbackIndex = 0;
            dest.SourceDeviceKey = "";
            dest.AudioMode = 0;

            //If dest has followers, don't show audio icon for this dest. Update the follower's feedback
            if (dest.AudioFollowers.Count > 0)
            {
                dest.AudioMode = 0;
                foreach (ushort index in dest.AudioFollowers)
                {
                    if (!AudioFeedbackToProcess.Contains(index))
                        AudioFeedbackToProcess.Add(index);
                }
            }


            UpdateDestinationFeedback(destIndex);
        }

        private void UpdateRouteFeedback(ushort destIndex, Source source)
        {
            Dest dest = Dests[destIndex];
            if (dest == null)
            {
                return;
            }

            try
            {
                dest.FeedbackName = source.Name;
                dest.FeedbackIndex = source.Index;
                dest.SourceDeviceKey = source.DeviceKey != null ? source.DeviceKey.ToLower() : "";
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Exception in UpdateRouteFeedback Step 1: {0}", ex);
            }

            try
            {
                //If dest has followers, don't show audio icon for this dest. Update the follower's feedback
                if (dest.AudioFollowers.Count > 0)
                {
                    dest.AudioMode = 0;
                    foreach (ushort index in dest.AudioFollowers)
                    {
                        if (!AudioFeedbackToProcess.Contains(index))
                            AudioFeedbackToProcess.Add(index);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Exception in UpdateRouteFeedback Step 2: dest: {0}, {1}", dest.Name, ex);
            }


            UpdateDestinationFeedback(destIndex);
        }

        private void UpdateDestinationFeedback(ushort destIndex)
        {
            CurrentRouteFeedbacks[destIndex].FireUpdate();
        }

        private void MakeDeviceRoute(string key, ushort output, ushort input)
        {
            try
            {
                if (fakeFeedback)
                {
                    FeedbackFromSimpl(key, output, input);
                }

                switch (key)
                {
                    case "videoRoute":
                        if (switcher01 != null)
                            switcher01.ExecuteNumericSwitch(input, output, eRoutingSignalType.Video);
                        break;
                    case "audioRoute":
                        if (switcher01 != null)
                            switcher01.ExecuteNumericSwitch(input, output, eRoutingSignalType.Audio);
                        break;
                    case "videoRoute2":
                        if (switcher02 != null)
                            switcher02.ExecuteNumericSwitch(input, output, eRoutingSignalType.Video);
                        break;
                    case "audioRoute2":
                        if (switcher02 != null)
                            switcher02.ExecuteNumericSwitch(input, output, eRoutingSignalType.Audio);
                        break;
                    case "videoRoute3":
                        if (switcher03 != null)
                            switcher03.ExecuteNumericSwitch(input, output, eRoutingSignalType.Video);
                        break;
                    case "audioRoute3":
                        if (switcher03 != null)
                            switcher03.ExecuteNumericSwitch(input, output, eRoutingSignalType.Audio);
                        break;
                    case "displayRoute":
                        routerTriList.StringInput[joinMap.DisplayInputCmd.JoinNumber + output - 1].StringValue =
                            output.ToString();
                        break;
                    case "usbRoute":
                        break;
                    case "txRoute":
                        if (txs.ContainsKey(output) && txs[output] != null)
                        {
                            txs[output].ExecuteNumericSwitch(input, 1, eRoutingSignalType.AudioVideo);
                        }

                        break;
                    case "contentRoute":
                        routerTriList.StringInput[joinMap.CodecInputCmd.JoinNumber].StringValue = output.ToString();
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}