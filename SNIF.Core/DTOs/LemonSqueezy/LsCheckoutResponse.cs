using System.Text.Json.Serialization;

namespace SNIF.Core.DTOs.LemonSqueezy;

public class LsCheckoutResponse
{
    public LsCheckoutResponseData Data { get; set; } = new();
}

public class LsCheckoutResponseData
{
    public LsCheckoutResponseAttributes Attributes { get; set; } = new();
}

public class LsCheckoutResponseAttributes
{
    public string Url { get; set; } = "";
}
