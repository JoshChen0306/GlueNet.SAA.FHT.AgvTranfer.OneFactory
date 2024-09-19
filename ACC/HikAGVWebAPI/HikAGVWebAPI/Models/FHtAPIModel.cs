using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class FHtAPI
{
    public string account { get; set; }
    public string api_key { get; set; }
    public string team_sn { get; set; }
    public string content_type { get; set; } = "1";
    public string text_content { get; set; }
    public string media_content { get; set; }
    public string file_show_name { get; set; }
    public string subject { get; set; } = "AGV 異常警報通知";

    public Dictionary<string, string> ToDictionary()
    {
        return this.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).ToDictionary(prop => prop.Name, prop => (string)prop.GetValue(this, null));
    }

    public override string ToString()
    {
        return $@"[Account] : {account}; [API Key] : {api_key}; [Team SN] : {team_sn}; [Content Type] : {content_type}; [Text Content] : {text_content}; [Media Content] : {media_content}; [File Show Name] : {file_show_name}; [Subject] : {subject}";
    }
}

public class FHtAPIAck
{
    public bool IsSuccess { get; set; }
    public string Description { get; set; }
    public int ErrorCode { get; set; }
    public string BatchID { get; set; }

    public override string ToString()
    {
        return $@"[Is Success] : {IsSuccess}; [Description] : {Description}; [Error Code] : {ErrorCode}; [Batch ID] : {BatchID}";
    }
}