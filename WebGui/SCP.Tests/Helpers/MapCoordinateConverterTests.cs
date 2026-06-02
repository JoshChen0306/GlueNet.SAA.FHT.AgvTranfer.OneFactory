using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SCP.Helpers;
using System.Collections.Generic;

namespace SCP.Tests.Helpers
{
    /// <summary>
    /// Locks the existing ConvertX/ConvertY behaviour that was extracted out of
    /// CommonController/PortController. Expected values are the linear-map formula
    /// output; any drift means the refactor changed behaviour.
    /// </summary>
    [TestClass]
    public class MapCoordinateConverterTests
    {
        private const string Area = "FHT1-1F";

        // Fixed calibration so assertions are deterministic and independent of appsettings.
        private static IConfiguration BuildConfig()
        {
            var dict = new Dictionary<string, string?>
            {
                ["AgvSetting:FHT1-1F:minPercentX"] = "17",
                ["AgvSetting:FHT1-1F:maxPercentX"] = "91.5",
                ["AgvSetting:FHT1-1F:minPercentY"] = "14",
                ["AgvSetting:FHT1-1F:maxPercentY"] = "89",
                ["AgvSetting:FHT1-1F:minX"] = "198700",
                ["AgvSetting:FHT1-1F:maxX"] = "214000",
                ["AgvSetting:FHT1-1F:minY"] = "196000",
                ["AgvSetting:FHT1-1F:maxY"] = "198200",
            };
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        [TestMethod]
        public void ConvertX_AtMinX_ReturnsMinPercent()   // Boundary P0
        {
            Assert.AreEqual("17%", MapCoordinateConverter.ConvertX(BuildConfig(), Area, "198700"));
        }

        [TestMethod]
        public void ConvertX_AtMaxX_ReturnsMaxPercent()   // Boundary P0
        {
            Assert.AreEqual("91.5%", MapCoordinateConverter.ConvertX(BuildConfig(), Area, "214000"));
        }

        [TestMethod]
        public void ConvertY_AtMinY_ReturnsMinPercent()   // Boundary P0
        {
            Assert.AreEqual("14%", MapCoordinateConverter.ConvertY(BuildConfig(), Area, "196000"));
        }

        [TestMethod]
        public void ConvertY_AtMaxY_ReturnsMaxPercent()   // Boundary P0
        {
            Assert.AreEqual("89%", MapCoordinateConverter.ConvertY(BuildConfig(), Area, "198200"));
        }

        [TestMethod]
        public void ConvertX_AtMidpoint_ReturnsMidPercent()   // Happy Path P1
        {
            // midpoint X = 206350 -> 17 + 0.5*(91.5-17) = 54.25
            Assert.AreEqual("54.25%", MapCoordinateConverter.ConvertX(BuildConfig(), Area, "206350"));
        }

        [TestMethod]
        public void ConvertY_AtMidpoint_ReturnsMidPercent()   // Happy Path P1
        {
            // midpoint Y = 197100 -> 14 + 0.5*(89-14) = 51.5
            Assert.AreEqual("51.5%", MapCoordinateConverter.ConvertY(BuildConfig(), Area, "197100"));
        }
    }
}
