using System.Globalization;

namespace project.Services
{
    /// <summary>
    /// Helper service for localization of numbers, dates, and currencies
    /// </summary>
    public class LocalizationHelper
    {
        /// <summary>
        /// Format currency value according to culture
        /// </summary>
        public static string FormatCurrency(decimal amount, string culture = "en")
        {
            try
            {
                var cultureInfo = new CultureInfo(culture);
                return amount.ToString("C", cultureInfo);
            }
            catch
            {
                return amount.ToString("C", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Format date according to culture
        /// </summary>
        public static string FormatDate(DateTime date, string culture = "en")
        {
            try
            {
                var cultureInfo = new CultureInfo(culture);
                return date.ToString("d", cultureInfo);
            }
            catch
            {
                return date.ToString("d", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Format number according to culture
        /// </summary>
        public static string FormatNumber(decimal number, string culture = "en")
        {
            try
            {
                var cultureInfo = new CultureInfo(culture);
                return number.ToString("N", cultureInfo);
            }
            catch
            {
                return number.ToString("N", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Get current culture from HttpContext
        /// </summary>
        public static string GetCulture(HttpContext context)
        {
            return CultureInfo.CurrentCulture.Name ?? "en";
        }

        /// <summary>
        /// Format date and time according to culture
        /// </summary>
        public static string FormatDateTime(DateTime dateTime, string culture = "en")
        {
            try
            {
                var cultureInfo = new CultureInfo(culture);
                return dateTime.ToString("g", cultureInfo);
            }
            catch
            {
                return dateTime.ToString("g", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Format percentage according to culture
        /// </summary>
        public static string FormatPercentage(decimal percentage, string culture = "en")
        {
            try
            {
                var cultureInfo = new CultureInfo(culture);
                return percentage.ToString("P", cultureInfo);
            }
            catch
            {
                return percentage.ToString("P", CultureInfo.InvariantCulture);
            }
        }
    }
}
