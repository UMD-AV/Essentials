﻿using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

//using System.IO;

namespace PepperDash.Core.JsonToSimpl
{
    public abstract class JsonToSimplMaster : IKeyed
    {
        /*****************************************************************************************/
        /** Events **/
        public event EventHandler<BoolChangeEventArgs> BoolChange;

        public event EventHandler<UshrtChangeEventArgs> UshrtChange;
        public event EventHandler<StringChangeEventArgs> StringChange;

        protected List<JsonToSimplChildObjectBase> Children = new List<JsonToSimplChildObjectBase>();

        /*****************************************************************************************/

        /// <summary>
        /// Mirrors the Unique ID for now.
        /// </summary>
        public string Key
        {
            get { return UniqueID; }
        }

        public string UniqueID { get; protected set; }

        /// <summary>
        /// Merely for use in debug messages
        /// </summary>
        public string DebugName
        {
            get { return _DebugName; }
            set
            {
                if (DebugName == null) _DebugName = "";
                else _DebugName = value;
            }
        }

        private string _DebugName = "";

        /// <summary>
        /// This will be prepended to all paths to allow path swapping or for more organized
        /// sub-paths
        /// </summary>
        public string PathPrefix { get; set; }

        /// <summary>
        /// This is added to the end of all paths
        /// </summary>
        public string PathSuffix { get; set; }

        /// <summary>
        /// Enables debugging output to the console.  Certain error messages will be logged to the 
        /// system's error log regardless of this setting
        /// </summary>
        public bool DebugOn { get; set; }

        /// <summary>
        /// Ushort helper for Debug property
        /// </summary>
        public ushort UDebug
        {
            get { return (ushort)(DebugOn ? 1 : 0); }
            set
            {
                DebugOn = (value == 1);
                CrestronConsole.PrintLine("JsonToSimpl debug={0}", DebugOn);
            }
        }

        public JObject JsonObject { get; protected set; }

        /*****************************************************************************************/
        /** Privates **/

        // The JSON file in JObject form
        // For gathering the incoming data
        protected Dictionary<string, JValue> UnsavedValues = new Dictionary<string, JValue>();

        /*****************************************************************************************/

        /// <summary>
        /// SIMPL+ default constructor.
        /// </summary>
        public JsonToSimplMaster()
        {
        }


        /// <summary>
        /// Sets up class - overriding methods should always call this.
        /// </summary>
        /// <param name="uniqueId"></param>
        public virtual void Initialize(string uniqueId)
        {
            UniqueID = uniqueId;
            J2SGlobal.AddMaster(this); // Should not re-add
        }

        /// <summary>
        /// Adds a child "module" to this master
        /// </summary>
        /// <param name="child"></param>
        public void AddChild(JsonToSimplChildObjectBase child)
        {
            if (!Children.Contains(child))
            {
                Children.Add(child);
            }
        }

        /// <summary>
        /// Called from the child to add changed or new values for saving
        /// </summary>
        public void AddUnsavedValue(string path, JValue value)
        {
            if (UnsavedValues.ContainsKey(path))
            {
                Debug.Console(0,
                    "Master[{0}] WARNING - Attempt to add duplicate value for path '{1}'.\r Ingoring. Please ensure that path does not exist on multiple modules.",
                    UniqueID, path);
            }
            else
                UnsavedValues.Add(path, value);
            //Debug.Console(0, "Master[{0}] Unsaved size={1}", UniqueID, UnsavedValues.Count);
        }

        public abstract void Save();


        //******************************************************************************************
        public static class JsonFixes
        {
            public static JObject ParseObject(string json)
            {
                using (JsonTextReader reader =
                       new JsonTextReader(new Crestron.SimplSharp.CrestronIO.StringReader(json)))
                {
                    int startDepth = reader.Depth;
                    JObject obj = JObject.Load(reader);
                    if (startDepth != reader.Depth)
                        throw new JsonSerializationException("Unenclosed json found");
                    return obj;
                }
            }

            public static JArray ParseArray(string json)
            {
                using (JsonTextReader reader =
                       new JsonTextReader(new Crestron.SimplSharp.CrestronIO.StringReader(json)))
                {
                    int startDepth = reader.Depth;
                    JArray obj = JArray.Load(reader);
                    if (startDepth != reader.Depth)
                        throw new JsonSerializationException("Unenclosed json found");
                    return obj;
                }
            }
        }

        // Helpers for events
        //******************************************************************************************
        protected void OnBoolChange(bool state, ushort index, ushort type)
        {
            if (BoolChange != null)
            {
                BoolChangeEventArgs args = new BoolChangeEventArgs(state, type);
                args.Index = index;
                BoolChange(this, args);
            }
        }

        //******************************************************************************************
        protected void OnUshrtChange(ushort state, ushort index, ushort type)
        {
            if (UshrtChange != null)
            {
                UshrtChangeEventArgs args = new UshrtChangeEventArgs(state, type);
                args.Index = index;
                UshrtChange(this, args);
            }
        }

        protected void OnStringChange(string value, ushort index, ushort type)
        {
            if (StringChange != null)
            {
                StringChangeEventArgs args = new StringChangeEventArgs(value, type);
                args.Index = index;
                StringChange(this, args);
            }
        }
    }
}