using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SCP.Helpers;
using System.Collections.Generic;

namespace SCP.Tests.Helpers
{
    [TestClass]
    public class ChargingStationProviderTests
    {
        // Base calibration: FHT1-1F (no swap) + FHT1-3F (swapXY, clean values so a swap is detectable).
        private static Dictionary<string, string?> BaseCalibration() => new Dictionary<string, string?>
        {
            ["AgvSetting:FHT1-1F:minPercentX"] = "17",
            ["AgvSetting:FHT1-1F:maxPercentX"] = "91.5",
            ["AgvSetting:FHT1-1F:minPercentY"] = "14",
            ["AgvSetting:FHT1-1F:maxPercentY"] = "89",
            ["AgvSetting:FHT1-1F:minX"] = "198700",
            ["AgvSetting:FHT1-1F:maxX"] = "214000",
            ["AgvSetting:FHT1-1F:minY"] = "196000",
            ["AgvSetting:FHT1-1F:maxY"] = "198200",
            ["AgvSetting:FHT1-3F:minPercentX"] = "0",
            ["AgvSetting:FHT1-3F:maxPercentX"] = "100",
            ["AgvSetting:FHT1-3F:minPercentY"] = "0",
            ["AgvSetting:FHT1-3F:maxPercentY"] = "100",
            ["AgvSetting:FHT1-3F:minX"] = "0",
            ["AgvSetting:FHT1-3F:maxX"] = "1000",
            ["AgvSetting:FHT1-3F:minY"] = "0",
            ["AgvSetting:FHT1-3F:maxY"] = "2000",
            ["AgvSetting:FHT1-3F:swapXY"] = "true",
        };

        private static IConfiguration Build(Dictionary<string, string?> dict)
            => new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        [TestMethod]
        public void Get_SingleStation_ReturnsOneConvertedPosition()   // Happy Path P0
        {
            var d = BaseCalibration();
            d["ChargingStations:FHT1-1F:0"] = "206350,197100,0";
            var list = ChargingStationProvider.Get(Build(d), "FHT1-1F");
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("54.25%", list[0].Left);   // ConvertX(206350)
            Assert.AreEqual("51.5%", list[0].Bottom);  // ConvertY(197100)
            Assert.AreEqual("rotate(0deg)", list[0].Transform);
        }

        [TestMethod]
        public void Get_MultipleStations_ReturnsAll()   // Happy Path P0
        {
            var d = BaseCalibration();
            d["ChargingStations:FHT1-1F:0"] = "206350,197100,0";
            d["ChargingStations:FHT1-1F:1"] = "198700,196000,0";
            d["ChargingStations:FHT1-1F:2"] = "214000,198200,90";
            var list = ChargingStationProvider.Get(Build(d), "FHT1-1F");
            Assert.AreEqual(3, list.Count);
        }

        [TestMethod]
        public void Get_ThreeFloorSwapXY_SwapsBeforeConvert()   // Edge Case P0
        {
            var d = BaseCalibration();
            d["ChargingStations:FHT1-3F:0"] = "500,1000,0";
            var list = ChargingStationProvider.Get(Build(d), "FHT1-3F");
            Assert.AreEqual(1, list.Count);
            // swap -> X=1000 -> 100%, Y=500 -> 25% (proves swap happened before convert)
            Assert.AreEqual("100%", list[0].Left);
            Assert.AreEqual("25%", list[0].Bottom);
        }

        [TestMethod]
        public void Get_NoConfigForArea_ReturnsEmpty()   // Edge Case P0
        {
            var list = ChargingStationProvider.Get(Build(BaseCalibration()), "FHT1-1F");
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void Get_MalformedEntries_SkippedWithoutThrow()   // Error P0
        {
            var d = BaseCalibration();
            d["ChargingStations:FHT1-1F:0"] = "bad";             // too few parts
            d["ChargingStations:FHT1-1F:1"] = "abc,def,0";       // non-numeric
            d["ChargingStations:FHT1-1F:2"] = "206350,197100,0"; // valid
            var list = ChargingStationProvider.Get(Build(d), "FHT1-1F");
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("54.25%", list[0].Left);
        }

        [TestMethod]
        public void Get_AngleApplied_SetsRotateTransform()   // Happy Path P1
        {
            var d = BaseCalibration();
            d["ChargingStations:FHT1-1F:0"] = "206350,197100,90";
            var list = ChargingStationProvider.Get(Build(d), "FHT1-1F");
            Assert.AreEqual("rotate(90deg)", list[0].Transform);
        }
    }
}
