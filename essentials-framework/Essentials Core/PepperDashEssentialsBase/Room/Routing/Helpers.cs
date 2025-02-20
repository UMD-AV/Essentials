namespace PepperDash.Essentials.Core.Routing
{
    public static class Helpers
    {
        public static ushort ConvertKeyToIndex(string key)
        {
            try
            {
                ushort index = ushort.Parse(key.Substring(key.Length - 2));
                return index;
            }
            catch
            {
                return 0;
            }
        }

        public static string ConvertIndexToSourceKey(ushort index)
        {
            return ("source" + index.ToString("D2"));
        }

        public static string ConvertIndexToDestKey(ushort index)
        {
            return ("dest" + index.ToString("D2"));
        }

        public static string ConvertIndexToActionKey(ushort index)
        {
            return ("action" + index.ToString("D2"));
        }
    }
}