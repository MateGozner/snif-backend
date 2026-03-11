namespace SNIF.Core.DTOs.LemonSqueezy;

public class LsCheckoutRequest
{
    public LsCheckoutData Data { get; set; } = new();
}

public class LsCheckoutData
{
    public string Type { get; set; } = "checkouts";
    public LsCheckoutAttributes Attributes { get; set; } = new();
    public LsCheckoutRelationships Relationships { get; set; } = new();
}

public class LsCheckoutAttributes
{
    public LsCheckoutCustomData CheckoutData { get; set; } = new();
    public LsProductOptions ProductOptions { get; set; } = new();
}

public class LsCheckoutCustomData
{
    public Dictionary<string, string> Custom { get; set; } = new();
}

public class LsProductOptions
{
    public string RedirectUrl { get; set; } = "";
}

public class LsCheckoutRelationships
{
    public LsRelationship Store { get; set; } = new();
    public LsRelationship Variant { get; set; } = new();
}

public class LsRelationship
{
    public LsRelationshipData Data { get; set; } = new();
}

public class LsRelationshipData
{
    public string Type { get; set; } = "";
    public string Id { get; set; } = "";
}
