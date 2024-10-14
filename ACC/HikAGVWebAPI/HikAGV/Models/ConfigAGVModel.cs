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
                //value = (AGVUrlSettings)this["AGVUrlSettings"];
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
                //value = (AGVSettings)this["AGVSettings"];
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
                //value = (string)this["RestURL"];
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
                //value = (string)this["AGVStatusURL"];
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
                //value = (string)this["CallBackURL"];
                this["CallBackURL"] = value;
            }
        }
    }

    public class AGVSettings : ConfigurationElement
    {
        [ConfigurationProperty("AGVMapCode", DefaultValue = "")]
        public string AGVMapCode
        {
            get
            {
                return (string)this["AGVMapCode"];
            }
            private set
            {
                //value = (string)this["AGVMapCode"];
                this["AGVMapCode"] = value;
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
                //value = (string)this["AGVTaskType"];
                this["AGVTaskType"] = value;
            }
        }
    }
}