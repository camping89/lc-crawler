using System.Globalization;

namespace LC.Crawler.Core.Extensions
{
    public static class DateTimeExtensions
    {
        public static DateTime AddWeeks(this DateTime fromDate, int numberWeek) 
        {
            return fromDate.AddDays(7 * numberWeek);
        }
        
        public static DateTime? ToDateTime(this string datetime) 
        {
            try
            {
                return Convert.ToDateTime(datetime, CultureInfo.InvariantCulture);    
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }
                
        public static DateTime TimeAgoToDateTime(this string timeAgo)
        {
            try
            {
                timeAgo = timeAgo.Replace("trước", string.Empty);
                if (timeAgo.Contains("ngày"))
                {
                    var dayNumber = timeAgo.Replace("ngày", string.Empty).ToIntODefault();
                    return DateTime.UtcNow.AddDays(-dayNumber);
                }
        
                if (timeAgo.Contains("tuần"))
                {
                    var weekNumber = timeAgo.Replace("tuần", string.Empty).ToIntODefault();;
                    return DateTime.UtcNow.AddWeeks(-weekNumber);
                }
        
                if (timeAgo.Contains("tháng"))
                {
                    var monthNumber = timeAgo.Replace("tháng", string.Empty).ToIntODefault();
                    return DateTime.UtcNow.AddMonths(-monthNumber);
                }
        
                if (timeAgo.Contains("năm"))
                {
                    var yearNumber = timeAgo.Replace("năm", string.Empty).ToIntODefault();
                    return DateTime.UtcNow.AddYears(-yearNumber);
                }

                if (timeAgo.Contains("giờ"))
                {
                    var hourNumber = timeAgo.Replace("giờ", string.Empty).ToIntODefault();
                    return DateTime.UtcNow.AddHours(-hourNumber);
                }
                
                if (timeAgo.Contains("phút"))
                {
                    var minuteNumber = timeAgo.Replace("phút", string.Empty).ToIntODefault();
                    return DateTime.UtcNow.AddMinutes(-minuteNumber);
                }
                        
        
                return DateTime.ParseExact(timeAgo, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return DateTime.UtcNow;
            }
        }
    }
}