using System.Configuration;

namespace HikAGVDll
{
    public class SectionDB : ConfigurationSection
    {
        [ConfigurationProperty("DBSettings")]
        public DBSettings DBSettings
        {
            get
            {
                return (DBSettings)this["DBSettings"];
            }
            set
            {
                //value = (DBSettings)this["DBSettings"];
                this["DBSettings"] = value;
            }
        }
    }

    public class DBSettings : ConfigurationElement
    {
        [ConfigurationProperty("DBIP", DefaultValue = "127.0.0.1", IsRequired = true)]
        public string DBIP
        {
            get
            {
                return (string)this["DBIP"];
            }
            set
            {
                //value = (string)this["DBIP"];
                this["DBIP"] = value;
            }
        }

        [ConfigurationProperty("DBName", DefaultValue = "127.0.0.1", IsRequired = true)]
        public string DBName
        {
            get
            {
                return (string)this["DBName"];
            }
            set
            {
                //value = (string)this["DBName"];
                this["DBName"] = value;
            }
        }

        [ConfigurationProperty("UserID", DefaultValue = "", IsRequired = true)]
        public string UserID
        {
            get
            {
                return (string)this["UserID"];
            }
            set
            {
                //value = (string)this["UserID"];
                this["UserID"] = value;
            }
        }

        [ConfigurationProperty("Password", DefaultValue = "", IsRequired = true)]
        public string Password
        {
            get
            {
                return (string)this["Password"];
            }
            set
            {
                //value = (string)this["Password"];
                this["Password"] = value;
            }
        }
    }
}