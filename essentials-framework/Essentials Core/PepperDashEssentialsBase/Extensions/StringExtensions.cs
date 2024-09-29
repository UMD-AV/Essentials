namespace PepperDash.Essentials.Core
{
    public static class StringExtensions
    {
        public static string NullIfEmpty(this string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }
        public static string NullIfWhiteSpace(this string s)
        {
            return string.IsNullOrEmpty(s.Trim()) ? null : s;
        }
        public static string ReplaceIfNullOrEmpty(this string s, string newString)
        {
            return string.IsNullOrEmpty(s) ? newString : s;
        }
    }
}