namespace LC.Crawler.Core.Extensions
{
    public static class NumericExtensions
    {
        public static string ToCommaStyle(this decimal value, string zeroString = "---")
        {
            if (value == 0) return zeroString;

            return $"{value:###,###}";
        }

        public static string ToCommaStyle(this object value, string zeroString = "---")
        {
            decimal.TryParse(value.ToString(), out var number);
            return ToCommaStyle(number, zeroString);
        }

        public static DateTime? UnixTimeStampToDateTime(this float? unixTimeStamp)
        {
            if (unixTimeStamp == null) return null;
            var value = (double?) unixTimeStamp;

            return UnixTimeStampToDateTime(value);
        }
        
        public static DateTime? UnixTimeStampToDateTime(this double? unixTimeStamp)
        {
            if (unixTimeStamp == null) return null;
            // Unix timestamp is seconds past epoch
            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp.Value).ToLocalTime();
            return dtDateTime;
        }

        public static List<int> ToList(this int number)
        {
            var indexes = new List<int>();
            for (var i = 0; i < number; i++)
            {
                indexes.Add(i);
            }

            return indexes;
        }
    }
}