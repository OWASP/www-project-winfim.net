namespace WinFIM.NET.Service
{
    public static class StringExtensions
    {
        // from https://stackoverflow.com/questions/2776673/how-do-i-truncate-a-net-string
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
