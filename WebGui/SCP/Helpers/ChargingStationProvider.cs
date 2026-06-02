using Microsoft.Extensions.Configuration;
using SCP.Models;
using System.Collections.Generic;
using System.Linq;

namespace SCP.Helpers
{
    /// <summary>
    /// Reads charging-station positions from appsettings "ChargingStations:{area}"
    /// (an array of "X,Y,angle" strings, same format as oPort.Remark) and converts
    /// them to screen positions using the same calibration + swapXY rules as racks,
    /// so charging stations follow the map calibration just like racks/AGV do.
    /// </summary>
    public static class ChargingStationProvider
    {
        /// <summary>
        /// Returns the charging stations configured for <paramref name="area"/>.
        /// Missing section -> empty list. Malformed entries are skipped (logged), not thrown.
        /// </summary>
        public static List<Position> Get(IConfiguration configuration, string area)
        {
            var result = new List<Position>();

            // Read the "X,Y,angle" array via GetChildren so we do not depend on the
            // configuration binder assembly (keeps the unit test host lean).
            var entries = configuration.GetSection($"ChargingStations:{area}")
                                       .GetChildren()
                                       .Select(c => c.Value)
                                       .Where(v => !string.IsNullOrWhiteSpace(v))
                                       .ToList();
            if (entries.Count == 0)
            {
                return result;
            }

            bool swapXY = configuration.GetSection($"AgvSetting:{area}")["swapXY"] == "true";

            foreach (var entry in entries)
            {
                var parts = entry!.Split(',');
                if (parts.Length < 3
                    || !double.TryParse(parts[0], out _)
                    || !double.TryParse(parts[1], out _))
                {
                    SCP.LogMgt.Logger?.Warn(
                        $"[ChargingStationProvider] area={area} invalid charging-station setting, skipped: '{entry}'");
                    continue;
                }

                string posX = parts[0].Trim();
                string posY = parts[1].Trim();
                string angle = parts[2].Trim();

                // Same swap rule as GetTrac: 3F stores X/Y swapped.
                if (swapXY)
                {
                    (posX, posY) = (posY, posX);
                }

                result.Add(new Position
                {
                    Left = MapCoordinateConverter.ConvertX(configuration, area, posX),
                    Bottom = MapCoordinateConverter.ConvertY(configuration, area, posY),
                    Transform = "rotate(" + angle + "deg)"
                });
            }

            return result;
        }
    }
}
