using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharp.CrestronIO;
using PepperDash.Core;
using PepperDash.Essentials.Core.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PepperDash.Essentials.Core
{
    public static class JoinMapHelper
    {
        /// <summary>
        /// Attempts to get the serialized join map from config
        /// </summary>
        /// <param name="joinMapKey"></param>
        /// <returns></returns>
        public static string GetSerializedJoinMapForDevice(string joinMapKey)
        {
            if (string.IsNullOrEmpty(joinMapKey))
                return null;

            JObject joinMap = ConfigReader.ConfigObject.JoinMaps[joinMapKey];

            return joinMap.ToString();
        }

        /// <summary>
        /// Attempts to get the serialized join map from config
        /// </summary>
        /// <param name="joinMapKey"></param>
        /// <returns></returns>
        public static string GetJoinMapForDevice(string joinMapKey)
        {
            return GetSerializedJoinMapForDevice(joinMapKey);
        }

        /// <summary>
        /// Attempts to find a custom join map by key and returns it deserialized if found
        /// </summary>
        /// <param name="joinMapKey"></param>
        /// <returns></returns>
        public static Dictionary<string, JoinData> TryGetJoinMapAdvancedForDevice(string joinMapKey)
        {
            try
            {
                if (string.IsNullOrEmpty(joinMapKey))
                    return null;

                if (!ConfigReader.ConfigObject.JoinMaps.ContainsKey(joinMapKey))
                {
                    Debug.Console(2, "No Join Map found in config with key: '{0}'", joinMapKey);
                    return null;
                }

                Debug.Console(2, "Attempting to load custom join map with key: {0}", joinMapKey);

                JObject joinMapJToken = ConfigReader.ConfigObject.JoinMaps[joinMapKey];

                if (joinMapJToken == null)
                    return null;

                Dictionary<string, JoinData> joinMapData = joinMapJToken.ToObject<Dictionary<string, JoinData>>();

                return joinMapData;
            }
            catch (Exception e)
            {
                Debug.Console(2, "Error getting join map for key: '{0}'.  Error: {1}", joinMapKey, e);
                return null;
            }
        }
    }

    /// <summary>
    /// Base class for join maps
    /// </summary>
    [Obsolete("This is being deprecated in favor of JoinMapBaseAdvanced")]
    public abstract class JoinMapBase
    {
        /// <summary>
        /// Modifies all the join numbers by adding the offset.  This should never be called twice
        /// </summary>
        /// <param name="joinStart"></param>
        public abstract void OffsetJoinNumbers(uint joinStart);

        /// <summary>
        /// The collection of joins and associated metadata
        /// </summary>
        public Dictionary<string, JoinMetadata> Joins = new Dictionary<string, JoinMetadata>();

        /// <summary>
        /// Prints the join information to console
        /// </summary>
        public void PrintJoinMapInfo()
        {
            Debug.Console(0, "{0}:\n", GetType().Name);

            // Get the joins of each type and print them
            Debug.Console(0, "Digitals:");
            Dictionary<string, JoinMetadata> digitals = Joins
                .Where(j => (j.Value.JoinType & eJoinType.Digital) == eJoinType.Digital)
                .ToDictionary(j => j.Key, j => j.Value);
            Debug.Console(2, "Found {0} Digital Joins", digitals.Count);
            PrintJoinList(GetSortedJoins(digitals));

            Debug.Console(0, "Analogs:");
            Dictionary<string, JoinMetadata> analogs = Joins
                .Where(j => (j.Value.JoinType & eJoinType.Analog) == eJoinType.Analog)
                .ToDictionary(j => j.Key, j => j.Value);
            Debug.Console(2, "Found {0} Analog Joins", analogs.Count);
            PrintJoinList(GetSortedJoins(analogs));

            Debug.Console(0, "Serials:");
            Dictionary<string, JoinMetadata> serials = Joins
                .Where(j => (j.Value.JoinType & eJoinType.Serial) == eJoinType.Serial)
                .ToDictionary(j => j.Key, j => j.Value);
            Debug.Console(2, "Found {0} Serial Joins", serials.Count);
            PrintJoinList(GetSortedJoins(serials));
        }

        /// <summary>
        /// Returns  a sorted list by JoinNumber
        /// </summary>
        /// <param name="joins"></param>
        /// <returns></returns>
        private List<KeyValuePair<string, JoinMetadata>> GetSortedJoins(Dictionary<string, JoinMetadata> joins)
        {
            List<KeyValuePair<string, JoinMetadata>> sortedJoins = joins.ToList();

            sortedJoins.Sort((pair1, pair2) => pair1.Value.JoinNumber.CompareTo(pair2.Value.JoinNumber));

            return sortedJoins;
        }

        private void PrintJoinList(List<KeyValuePair<string, JoinMetadata>> joins)
        {
            foreach (KeyValuePair<string, JoinMetadata> join in joins)
            {
                Debug.Console(0,
                    @"Join Number: {0} | Label: '{1}' | JoinSpan: '{2}' | Type: '{3}' | Capabilities: '{4}'",
                    join.Value.JoinNumber,
                    join.Value.Label,
                    join.Value.JoinSpan,
                    join.Value.JoinType.ToString(),
                    join.Value.JoinCapabilities.ToString());
            }
        }

        /// <summary>
        /// Returns the join number for the join with the specified key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public uint GetJoinForKey(string key)
        {
            return Joins.ContainsKey(key) ? Joins[key].JoinNumber : 0;
        }

        /// <summary>
        /// Returns the join span for the join with the specified key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public uint GetJoinSpanForKey(string key)
        {
            return Joins.ContainsKey(key) ? Joins[key].JoinSpan : 0;
        }
    }

    /// <summary>
    /// Base class for join maps
    /// </summary>
    public abstract class JoinMapBaseAdvanced
    {
        protected uint JoinOffset;

        /// <summary>
        /// The collection of joins and associated metadata
        /// </summary>
        public Dictionary<string, JoinDataComplete> Joins { get; private set; }

        protected JoinMapBaseAdvanced(uint joinStart)
        {
            Joins = new Dictionary<string, JoinDataComplete>();

            JoinOffset = joinStart - 1;
        }

        protected JoinMapBaseAdvanced(uint joinStart, Type type) : this(joinStart)
        {
            AddJoins(type);
        }

        protected void AddJoins(Type type)
        {
            IEnumerable<FieldInfo> fields =
                type.GetCType()
                    .GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => f.IsDefined(typeof(JoinNameAttribute), true));

            foreach (FieldInfo field in fields)
            {
                object childClass = Convert.ChangeType(this, type, null);

                JoinDataComplete value =
                    field.GetValue(
                            childClass) as
                        JoinDataComplete; //this here is JoinMapBaseAdvanced, not the child class. JoinMapBaseAdvanced has no fields.

                if (value == null)
                {
                    Debug.Console(0, "Unable to cast base class to {0}", type.Name);
                    continue;
                }

                value.SetJoinOffset(JoinOffset);

                string joinName = value.GetNameAttribute(field);

                if (string.IsNullOrEmpty(joinName)) continue;

                Joins.Add(joinName, value);
            }


            if (Debug.Level > 0)
            {
                PrintJoinMapInfo();
            }
        }

        /// <summary>
        /// Prints the join information to console
        /// </summary>
        public void PrintJoinMapInfo()
        {
            Debug.Console(0, "{0}:\n", GetType().Name);

            // Get the joins of each type and print them
            Debug.Console(0, "Digitals:");
            Dictionary<string, JoinDataComplete> digitals = Joins
                .Where(j => (j.Value.Metadata.JoinType & eJoinType.Digital) == eJoinType.Digital)
                .ToDictionary(j => j.Key, j => j.Value);
            Debug.Console(2, "Found {0} Digital Joins", digitals.Count);
            PrintJoinList(GetSortedJoins(digitals));

            Debug.Console(0, "Analogs:");
            Dictionary<string, JoinDataComplete> analogs = Joins
                .Where(j => (j.Value.Metadata.JoinType & eJoinType.Analog) == eJoinType.Analog)
                .ToDictionary(j => j.Key, j => j.Value);
            Debug.Console(2, "Found {0} Analog Joins", analogs.Count);
            PrintJoinList(GetSortedJoins(analogs));

            Debug.Console(0, "Serials:");
            Dictionary<string, JoinDataComplete> serials = Joins
                .Where(j => (j.Value.Metadata.JoinType & eJoinType.Serial) == eJoinType.Serial)
                .ToDictionary(j => j.Key, j => j.Value);
            Debug.Console(2, "Found {0} Serial Joins", serials.Count);
            PrintJoinList(GetSortedJoins(serials));
        }

        /// <summary>
        /// Prints the join information to console
        /// </summary>
        public void MarkdownJoinMapInfo(string deviceKey, string bridgeKey)
        {
            string pluginType = GetType().Name;

            Debug.Console(0, "{0}:\n", pluginType);

            StringBuilder sb = new StringBuilder();

            sb.AppendLine(string.Format("# {0}", GetType().Name));
            sb.AppendLine(string.Format("Generated from '{0}' on bridge '{1}'", deviceKey, bridgeKey));
            sb.AppendLine();
            sb.AppendLine("## Digitals");
            // Get the joins of each type and print them
            Dictionary<string, JoinDataComplete> digitals = Joins
                .Where(j => (j.Value.Metadata.JoinType & eJoinType.Digital) == eJoinType.Digital)
                .ToDictionary(j => j.Key, j => j.Value);
            Debug.Console(2, "Found {0} Digital Joins", digitals.Count);
            StringBuilder digitalSb = AppendJoinList(GetSortedJoins(digitals));
            digitalSb.AppendLine("## Analogs");
            digitalSb.AppendLine();

            Debug.Console(0, "Analogs:");
            Dictionary<string, JoinDataComplete> analogs = Joins
                .Where(j => (j.Value.Metadata.JoinType & eJoinType.Analog) == eJoinType.Analog)
                .ToDictionary(j => j.Key, j => j.Value);
            Debug.Console(2, "Found {0} Analog Joins", analogs.Count);
            StringBuilder analogSb = AppendJoinList(GetSortedJoins(analogs));
            analogSb.AppendLine("## Serials");
            analogSb.AppendLine();

            Debug.Console(0, "Serials:");
            Dictionary<string, JoinDataComplete> serials = Joins
                .Where(j => (j.Value.Metadata.JoinType & eJoinType.Serial) == eJoinType.Serial)
                .ToDictionary(j => j.Key, j => j.Value);
            Debug.Console(2, "Found {0} Serial Joins", serials.Count);
            StringBuilder serialSb = AppendJoinList(GetSortedJoins(serials));

            sb.EnsureCapacity(sb.Length + digitalSb.Length + analogSb.Length + serialSb.Length);
            sb.Append(digitalSb).Append(analogSb).Append(serialSb);

            WriteJoinmapMarkdown(sb, pluginType, bridgeKey, deviceKey);
        }

        private static void WriteJoinmapMarkdown(StringBuilder stringBuilder, string pluginType, string bridgeKey,
            string deviceKey)
        {
            string fileName = string.Format("{0}{1}{2}__{3}__{4}.md", Global.FilePathPrefix, "joinMaps/", pluginType,
                bridgeKey, deviceKey);

            using (StreamWriter sw = new StreamWriter(fileName))
            {
                sw.WriteLine(stringBuilder.ToString());
                Debug.Console(0, "Joinmap Readme generated and written to {0}", fileName);
            }
        }

        /// <summary>
        /// Returns  a sorted list by JoinNumber
        /// </summary>
        /// <param name="joins"></param>
        /// <returns></returns>
        private List<KeyValuePair<string, JoinDataComplete>> GetSortedJoins(Dictionary<string, JoinDataComplete> joins)
        {
            List<KeyValuePair<string, JoinDataComplete>> sortedJoins = joins.ToList();

            sortedJoins.Sort((pair1, pair2) => pair1.Value.JoinNumber.CompareTo(pair2.Value.JoinNumber));

            return sortedJoins;
        }

        private void PrintJoinList(List<KeyValuePair<string, JoinDataComplete>> joins)
        {
            foreach (KeyValuePair<string, JoinDataComplete> join in joins)
            {
                Debug.Console(0,
                    @"Join Number: {0} | JoinSpan: '{1}' | JoinName: {2} | Description: '{3}' | Type: '{4}' | Capabilities: '{5}'",
                    join.Value.JoinNumber,
                    join.Value.JoinSpan,
                    join.Key,
                    string.IsNullOrEmpty(join.Value.AttributeName)
                        ? join.Value.Metadata.Label
                        : join.Value.AttributeName,
                    join.Value.Metadata.JoinType.ToString(),
                    join.Value.Metadata.JoinCapabilities.ToString());
            }
        }

        private static StringBuilder AppendJoinList(List<KeyValuePair<string, JoinDataComplete>> joins)
        {
            StringBuilder sb = new StringBuilder();
            const string stringFormatter = "| {0} | {1} | {2} | {3} | {4} |";
            const int joinNumberLen = 11;
            const int joinSpanLen = 9;
            const int typeLen = 19;
            const int capabilitiesLen = 12;
            int descriptionLen = (from @join in joins select @join.Value into j select j.Metadata.Description.Length)
                .Concat(new[] { 11 }).Max();

            //build header
            sb.AppendLine(string.Format(stringFormatter,
                string.Format("Join Number").PadRight(joinNumberLen, ' '),
                string.Format("Join Span").PadRight(joinSpanLen, ' '),
                string.Format("Description").PadRight(descriptionLen, ' '),
                string.Format("Type").PadRight(typeLen, ' '),
                string.Format("Capabilities").PadRight(capabilitiesLen, ' ')));
            //build table seperator
            sb.AppendLine(string.Format(stringFormatter,
                new string('-', joinNumberLen),
                new string('-', joinSpanLen),
                new string('-', descriptionLen),
                new string('-', typeLen),
                new string('-', capabilitiesLen)));

            foreach (KeyValuePair<string, JoinDataComplete> join in joins)
            {
                sb.AppendLine(join.Value.GetMarkdownFormattedData(stringFormatter, descriptionLen));
            }

            sb.AppendLine();
            return sb;
        }

        /// <summary>
        /// Attempts to find the matching key for the custom join and if found overwrites the default JoinData with the custom
        /// </summary>
        /// <param name="joinData"></param>
        public void SetCustomJoinData(Dictionary<string, JoinData> joinData)
        {
            foreach (KeyValuePair<string, JoinData> customJoinData in joinData)
            {
                JoinDataComplete join = Joins[customJoinData.Key];

                if (join != null)
                {
                    join.SetCustomJoinData(customJoinData.Value);
                }
                else
                {
                    Debug.Console(2, "No matching key found in join map for: '{0}'", customJoinData.Key);
                }
            }

            PrintJoinMapInfo();
        }

        ///// <summary>
        ///// Returns the join number for the join with the specified key
        ///// </summary>
        ///// <param name="key"></param>
        ///// <returns></returns>
        //public uint GetJoinForKey(string key)
        //{
        //    return Joins.ContainsKey(key) ? Joins[key].JoinNumber : 0;
        //}


        ///// <summary>
        ///// Returns the join span for the join with the specified key
        ///// </summary>
        ///// <param name="key"></param>
        ///// <returns></returns>
        //public uint GetJoinSpanForKey(string key)
        //{
        //    return Joins.ContainsKey(key) ? Joins[key].JoinSpan : 0;
        //}
    }

    /// <summary>
    /// Read = Provides feedback to SIMPL
    /// Write = Responds to sig values from SIMPL
    /// </summary>
    [Flags]
    public enum eJoinCapabilities
    {
        None = 0,
        ToSIMPL = 1,
        FromSIMPL = 2,
        ToFromSIMPL = ToSIMPL | FromSIMPL,
        ToFusion = 4,
        FromFusion = 8,
        ToFromFusion = ToFusion | FromFusion,
    }

    [Flags]
    public enum eJoinType
    {
        None = 0,
        Digital = 1,
        Analog = 2,
        Serial = 4,
        DigitalAnalog = Digital | Analog,
        DigitalSerial = Digital | Serial,
        AnalogSerial = Analog | Serial,
        DigitalAnalogSerial = Digital | Analog | Serial,
    }

    /// <summary>
    /// Metadata describing the join
    /// </summary>
    public class JoinMetadata
    {
        private string _description;

        /// <summary>
        /// Join number (based on join offset value)
        /// </summary>
        [JsonProperty("joinNumber")]
        [Obsolete]
        public uint JoinNumber { get; set; }

        /// <summary>
        /// Join range span.  If join indicates the start of a range of joins, this indicated the maximum number of joins in the range
        /// </summary>
        [Obsolete]
        [JsonProperty("joinSpan")]
        public uint JoinSpan { get; set; }

        /// <summary>
        /// A label for the join to better describe its usage
        /// </summary>
        [Obsolete("Use Description instead")]
        [JsonProperty("label")]
        public string Label
        {
            get { return _description; }
            set { _description = value; }
        }

        /// <summary>
        /// A description for the join to better describe its usage
        /// </summary>
        [JsonProperty("description")]
        public string Description
        {
            get { return _description; }
            set { _description = value; }
        }

        /// <summary>
        /// Signal type(s)
        /// </summary>
        [JsonProperty("joinType")]
        public eJoinType JoinType { get; set; }

        /// <summary>
        /// Indicates whether the join is read and/or write
        /// </summary>
        [JsonProperty("joinCapabilities")]
        public eJoinCapabilities JoinCapabilities { get; set; }

        /// <summary>
        /// Indicates a set of valid values (particularly if this translates to an enum
        /// </summary>
        [JsonProperty("validValues")]
        public string[] ValidValues { get; set; }
    }

    /// <summary>
    /// Data describing the join.  Can be overridden from configuratino
    /// </summary>
    public class JoinData
    {
        /// <summary>
        /// Join number (based on join offset value)
        /// </summary>
        [JsonProperty("joinNumber")]
        public uint JoinNumber { get; set; }

        /// <summary>
        /// Join range span.  If join indicates the start of a range of joins, this indicated the maximum number of joins in the range
        /// </summary>
        [JsonProperty("joinSpan")]
        public uint JoinSpan { get; set; }

        /// <summary>
        /// Fusion Attribute Name (optional)
        /// </summary>
        [JsonProperty("attributeName")]
        public string AttributeName { get; set; }
    }

    /// <summary>
    /// A class to aggregate the JoinData and JoinMetadata for a join
    /// </summary>
    public class JoinDataComplete
    {
        private uint _joinOffset;

        private JoinData _data;
        public JoinMetadata Metadata { get; set; }

        /// <summary>
        /// To store some future information as you please
        /// </summary>
        public object UserObject { get; private set; }

        public JoinDataComplete(JoinData data, JoinMetadata metadata)
        {
            _data = data;
            Metadata = metadata;
        }

        public string GetMarkdownFormattedData(string stringFormatter, int descriptionLen)
        {
            //Fixed Width Headers
            int joinNumberLen = string.Format("Join Number").Length;
            int joinSpanLen = string.Format("Join Span").Length;
            int typeLen = string.Format("AnalogDigitalSerial").Length;
            int capabilitiesLen = string.Format("ToFromFusion").Length;

            //Track which one failed, if it did
            const string placeholder = "unknown";
            Dictionary<string, string> dataArray = new Dictionary<string, string>
            {
                { "joinNumber", placeholder.PadRight(joinNumberLen, ' ') },
                { "joinSpan", placeholder.PadRight(joinSpanLen, ' ') },
                { "description", placeholder.PadRight(descriptionLen, ' ') },
                { "joinType", placeholder.PadRight(typeLen, ' ') },
                { "capabilities", placeholder.PadRight(capabilitiesLen, ' ') }
            };


            try
            {
                dataArray["joinNumber"] =
                    string.Format("{0}",
                            JoinNumber.ToString(CultureInfo.InvariantCulture).ReplaceIfNullOrEmpty(placeholder))
                        .PadRight(joinNumberLen, ' ');
                dataArray["joinSpan"] =
                    string.Format("{0}",
                            JoinSpan.ToString(CultureInfo.InvariantCulture).ReplaceIfNullOrEmpty(placeholder))
                        .PadRight(joinSpanLen, ' ');
                dataArray["description"] = string.Format("{0}", Metadata.Description.ReplaceIfNullOrEmpty(placeholder))
                    .PadRight(descriptionLen, ' ');
                dataArray["joinType"] =
                    string.Format("{0}", Metadata.JoinType.ToString().ReplaceIfNullOrEmpty(placeholder))
                        .PadRight(typeLen, ' ');
                dataArray["capabilities"] = string
                    .Format("{0}", Metadata.JoinCapabilities.ToString().ReplaceIfNullOrEmpty(placeholder))
                    .PadRight(capabilitiesLen, ' ');

                return string.Format(stringFormatter,
                    dataArray["joinNumber"],
                    dataArray["joinSpan"],
                    dataArray["description"],
                    dataArray["joinType"],
                    dataArray["capabilities"]);
            }
            catch (Exception e)
            {
                //Don't Throw - we don't want to kill the system if this falls over - it's not mission critical. Print the error, use placeholder data
                string errorKey = string.Empty;
                foreach (KeyValuePair<string, string> item in dataArray)
                {
                    if (item.Value.TrimEnd() == placeholder)
                        errorKey = item.Key;
                    break;
                }

                Debug.Console(0, "Unable to decode join metadata {1}- {0}", e.Message,
                    !string.IsNullOrEmpty(errorKey) ? (' ' + errorKey) : string.Empty);
                return string.Format(stringFormatter,
                    dataArray["joinNumber"],
                    dataArray["joinSpan"],
                    dataArray["description"],
                    dataArray["joinType"],
                    dataArray["capabilities"]);
            }
        }


        /// <summary>
        /// Sets the join offset value
        /// </summary>
        /// <param name="joinOffset"></param>
        public void SetJoinOffset(uint joinOffset)
        {
            _joinOffset = joinOffset;
        }

        /// <summary>
        /// The join number (including the offset)
        /// </summary>
        public uint JoinNumber
        {
            get { return _data.JoinNumber + _joinOffset; }
            set { _data.JoinNumber = value; }
        }

        public uint JoinSpan
        {
            get { return _data.JoinSpan; }
        }

        public string AttributeName
        {
            get { return _data.AttributeName; }
        }

        public void SetCustomJoinData(JoinData customJoinData)
        {
            _data = customJoinData;
        }

        public string GetNameAttribute(MemberInfo memberInfo)
        {
            string name = string.Empty;
            JoinNameAttribute attribute =
                (JoinNameAttribute)CAttribute.GetCustomAttribute(memberInfo, typeof(JoinNameAttribute));

            if (attribute == null) return name;

            name = attribute.Name;
            Debug.Console(2, "JoinName Attribute value: {0}", name);
            return name;
        }
    }


    [AttributeUsage(AttributeTargets.All)]
    public class JoinNameAttribute : CAttribute
    {
        private string _Name;

        public JoinNameAttribute(string name)
        {
            Debug.Console(2, "Setting Attribute Name: {0}", name);
            _Name = name;
        }

        public string Name
        {
            get { return _Name; }
        }
    }
}