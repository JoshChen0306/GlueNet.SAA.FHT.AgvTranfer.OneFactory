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
                value = (DBSettings)this["DBSettings"];
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
                value = (string)this["DBIP"];
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
                value = (string)this["DBName"];
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
                value = (string)this["UserID"];
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
                value = (string)this["Password"];
            }
        }
    }
}