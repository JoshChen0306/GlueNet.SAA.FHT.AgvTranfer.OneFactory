using Microsoft.Extensions.Configuration;

namespace SCP.Helpers
{
    /// <summary>
    /// Maps RCS world coordinates to screen percentage strings (e.g. "43.48%"),
    /// using the per-area calibration under appsettings "AgvSetting:{area}".
    /// Shared by CommonController, PortController and ChargingStationProvider so the
    /// conversion lives in exactly one place.
    /// </summary>
    public static class MapCoordinateConverter
    {
        /// <summary>RCS X -> "left%" string.</summary>
        public static string ConvertX(IConfiguration configuration, string area, string posX)
        {
            var setting = configuration.GetSection($"AgvSetting:{area}");
            double minPercentX = Convert.ToDouble(setting["minPercentX"]);
            double maxPercentX = Convert.ToDouble(setting["maxPercentX"]);
            double minX = Convert.ToDouble(setting["minX"]);
            double maxX = Convert.ToDouble(setting["maxX"]);
            double percentRangeX = maxPercentX - minPercentX;
            double rangeX = maxX - minX;

            double normalizedX = (Convert.ToDouble(posX) - minX) / rangeX;
            // Map normalized 0-1 into the minPercent..maxPercent band.
            return (minPercentX + (normalizedX * percentRangeX)).ToString() + "%";
        }

        /// <summary>RCS Y -> "bottom%" string.</summary>
        public static string ConvertY(IConfiguration configuration, string area, string posY)
        {
            var setting = configuration.GetSection($"AgvSetting:{area}");
            double minPercentY = Convert.ToDouble(setting["minPercentY"]);
            double maxPercentY = Convert.ToDouble(setting["maxPercentY"]);
            double minY = Convert.ToDouble(setting["minY"]);
            double maxY = Convert.ToDouble(setting["maxY"]);
            double percentRangeY = maxPercentY - minPercentY;
            double rangeY = maxY - minY;

            double normalizedY = (Convert.ToDouble(posY) - minY) / rangeY;
            // Map normalized 0-1 into the minPercent..maxPercent band.
            return (minPercentY + (normalizedY * percentRangeY)).ToString() + "%";
        }
    }
}
