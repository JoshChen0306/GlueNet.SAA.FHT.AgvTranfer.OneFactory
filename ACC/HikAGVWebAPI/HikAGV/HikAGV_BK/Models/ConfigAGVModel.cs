using System.Configuration;

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
                value = (AGVUrlSettings)this["AGVUrlSettings"];
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
                value = (AGVSettings)this["AGVSettings"];
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
                value = (string)this["RestURL"];
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
                value = (string)this["AGVStatusURL"];
            }
        }

        //[ConfigurationProperty("CallBackURL", DefaultValue = "http://10.46.73.128:11233/agv/agvCallbackService/{0}")]
        [ConfigurationProperty("CallBackURL", DefaultValue = "http://localhost:54632/agv/agvCallbackService/{0}")]
        internal string CallBackURL
        {
            get
            {
                return (string)this["CallBackURL"];
            }
            private set
            {
                value = (string)this["CallBackURL"];
            }
        }
    }

    public class AGVSettings : ConfigurationElement
    {
        [ConfigurationProperty("ThreadEnable", DefaultValue = false)]
        public bool ThreadEnable
        {
            get
            {
                return (bool)this["ThreadEnable"];
            }
            private set
            {
                value = (bool)this["ThreadEnable"];
            }
        }

        [ConfigurationProperty("AGVMapCode", DefaultValue = "")]
        public string AGVMapCode
        {
            get
            {
                return (string)this["AGVMapCode"];
            }
            private set
            {
                value = (string)this["AGVMapCode"];
            }
        }

        [ConfigurationProperty("AGVTaskType", DefaultValue = "")]
        public string AGVTaskType
        {
            get
            {
                return (string)this["AGVTaskType"];
            }
            private set
            {
                value = (string)this["AGVTaskType"];
            }
        }
    }
}