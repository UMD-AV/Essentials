﻿using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PepperDash.Core.JsonToSimpl
{
    public class JsonToSimplArrayLookupChild : JsonToSimplChildObjectBase
    {
        public string SearchPropertyName { get; set; }
        public string SearchPropertyValue { get; set; }

        private int ArrayIndex;

        /// <summary>
        /// For <2.4.1 array lookups
        /// </summary>
        /// <param name="file"></param>
        /// <param name="key"></param>
        /// <param name="pathPrefix"></param>
        /// <param name="pathSuffix"></param>
        /// <param name="searchPropertyName"></param>
        /// <param name="searchPropertyValue"></param>
        public void Initialize(string file, string key, string pathPrefix, string pathSuffix,
            string searchPropertyName, string searchPropertyValue)
        {
            base.Initialize(file, key, pathPrefix, pathSuffix);
            SearchPropertyName = searchPropertyName;
            SearchPropertyValue = searchPropertyValue;
        }


        /// <summary>
        /// For newer >=2.4.1 array lookups. 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="key"></param>
        /// <param name="pathPrefix"></param>
        /// <param name="pathAppend"></param>
        /// <param name="pathSuffix"></param>
        /// <param name="searchPropertyName"></param>
        /// <param name="searchPropertyValue"></param>
        public void InitializeWithAppend(string file, string key, string pathPrefix, string pathAppend,
            string pathSuffix, string searchPropertyName, string searchPropertyValue)
        {
            string pathPrefixWithAppend = (pathPrefix != null ? pathPrefix : "") + GetPathAppend(pathAppend);
            base.Initialize(file, key, pathPrefixWithAppend, pathSuffix);

            SearchPropertyName = searchPropertyName;
            SearchPropertyValue = searchPropertyValue;
        }


        //PathPrefix+ArrayName+[x]+path+PathSuffix
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        protected override string GetFullPath(string path)
        {
            return string.Format("{0}[{1}].{2}{3}",
                PathPrefix == null ? "" : PathPrefix,
                ArrayIndex,
                path,
                PathSuffix == null ? "" : PathSuffix);
        }

        public override void ProcessAll()
        {
            if (FindInArray())
                base.ProcessAll();
        }

        /// <summary>
        /// Provides the path append for GetFullPath
        /// </summary>
        /// <returns></returns>
        private string GetPathAppend(string a)
        {
            if (string.IsNullOrEmpty(a))
            {
                return "";
            }

            if (a.StartsWith("."))
            {
                return a;
            }
            else
            {
                return "." + a;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool FindInArray()
        {
            if (Master == null)
                throw new InvalidOperationException("Cannot do operations before master is linked");
            if (Master.JsonObject == null)
                throw new InvalidOperationException("Cannot do operations before master JSON has read");
            if (PathPrefix == null)
                throw new InvalidOperationException("Cannot do operations before PathPrefix is set");


            JToken token = Master.JsonObject.SelectToken(PathPrefix);
            if (token is JArray)
            {
                JArray array = token as JArray;
                try
                {
                    JToken item = array.FirstOrDefault(o =>
                    {
                        JToken prop = o[SearchPropertyName];
                        return prop != null && prop.Value<string>()
                            .Equals(SearchPropertyValue, StringComparison.OrdinalIgnoreCase);
                    });
                    if (item == null)
                    {
                        Debug.Console(1, "JSON Child[{0}] Array '{1}' '{2}={3}' not found: ", Key,
                            PathPrefix, SearchPropertyName, SearchPropertyValue);
                        this.LinkedToObject = false;
                        return false;
                    }

                    this.LinkedToObject = true;
                    ArrayIndex = array.IndexOf(item);
                    OnStringChange(string.Format("{0}[{1}]", PathPrefix, ArrayIndex), 0,
                        JsonToSimplConstants.FullPathToArrayChange);
                    Debug.Console(1, "JSON Child[{0}] Found array match at index {1}", Key, ArrayIndex);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.Console(1, "JSON Child[{0}] Array '{1}' lookup error: '{2}={3}'\r{4}", Key,
                        PathPrefix, SearchPropertyName, SearchPropertyValue, e);
                }
            }
            else
            {
                Debug.Console(1, "JSON Child[{0}] Path '{1}' is not an array", Key, PathPrefix);
            }

            return false;
        }
    }
}