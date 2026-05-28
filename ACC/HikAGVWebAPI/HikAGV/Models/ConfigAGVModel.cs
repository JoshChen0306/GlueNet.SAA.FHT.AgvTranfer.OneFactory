using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace HikAGVDll
{
    public class SectionAGV : ConfigurationSection
    {
        [ConfigurationProperty("AGVUrlSettings")]
        public AGVUrlSettings AGVUrlSettings
        {
            get
            {
                return (AGVUrlSettings)this["AGVUrlSettings"];
            }
            set
            {
                this["AGVUrlSettings"] = value;
            }
        }

        [ConfigurationProperty("AGVSettings")]
        public AGVSettings AGVSettings
        {
            get
            {
                return (AGVSettings)this["AGVSettings"];
            }
            set
            {
                this["AGVSettings"] = value;
            }
        }
    }

    public class AGVUrlSettings : ConfigurationElement
    {
        [ConfigurationProperty("RestURL", DefaultValue = "")]
        public string RestURL
        {
            get
            {
                return (string)this["RestURL"];
            }
            private set
            {
                this["RestURL"] = value;
            }
        }

        [ConfigurationProperty("AGVStatusURL", DefaultValue = "")]
        public string AGVStatusURL
        {
            get
            {
                return (string)this["AGVStatusURL"];
            }
            private set
            {
                this["AGVStatusURL"] = value;
            }
        }

        [ConfigurationProperty("ContentType", DefaultValue = "application/json")]
        public string ContentType
        {
            get
            {
                return (string)this["ContentType"];
            }
            private set
            {
                this["ContentType"] = value;
            }
        }

        [ConfigurationProperty("CallBackURL", DefaultValue = "http://localhost:54632/agv/agvCallbackService/{0}")]
        internal string CallBackURL
        {
            get
            {
                return (string)this["CallBackURL"];
            }
            private set
            {
                this["CallBackURL"] = value;
            }
        }
    }

    public class AGVSettings : ConfigurationElement
    {
        [ConfigurationProperty("AGVTaskType", DefaultValue = "")]
        public string AGVTaskType
        {
            get
            {
                return (string)this["AGVTaskType"];
            }
            private set
            {
                this["AGVTaskType"] = value;
            }
        }

        [ConfigurationProperty("WarnContent", DefaultValue = "安全告警-前碰撞条触发,安全告警-后碰撞条触发")]
        public string WarnContent
        {
            get
            {
                return (string)this["WarnContent"];
            }
            private set
            {
                this["WarnContent"] = value;
            }
        }

        /// <summary>
        /// 跨樓層車輛編號（歸位機制與預調度機制共用，一廠預設 3）
        /// </summary>
        [ConfigurationProperty("CrossFloorShuttleId", DefaultValue = "3")]
        public string CrossFloorShuttleId
        {
            get
            {
                return (string)this["CrossFloorShuttleId"];
            }
            private set
            {
                this["CrossFloorShuttleId"] = value;
            }
        }

        /// <summary>
        /// 跨樓層車輛閒置歸位超時秒數（一廠預設 600 秒）
        /// </summary>
        [ConfigurationProperty("IdleReturnTimeout", DefaultValue = 600)]
        public int IdleReturnTimeout
        {
            get
            {
                return (int)this["IdleReturnTimeout"];
            }
            private set
            {
                this["IdleReturnTimeout"] = value;
            }
        }

        /// <summary>
        /// 跨樓層車輛歸位目的地樓層（一廠預設 3F）
        /// </summary>
        [ConfigurationProperty("IdleReturnFloor", DefaultValue = "3F")]
        public string IdleReturnFloor
        {
            get
            {
                return (string)this["IdleReturnFloor"];
            }
            private set
            {
                this["IdleReturnFloor"] = value;
            }
        }

        /// <summary>
        /// MapCode 對應樓層（格式：MapCode:Floor,MapCode:Floor，一廠僅 1F/3F）
        /// </summary>
        [ConfigurationProperty("MapCodeFloorMapping", DefaultValue = "AA:1F,DD:3F")]
        public string MapCodeFloorMapping
        {
            get
            {
                return (string)this["MapCodeFloorMapping"];
            }
            private set
            {
                this["MapCodeFloorMapping"] = value;
            }
        }
    }
}