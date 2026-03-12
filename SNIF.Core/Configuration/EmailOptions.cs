namespace SNIF.Core.Configuration
{
    public class EmailOptions
    {
        public const string SectionName = "Email";

        public string Provider { get; set; } = "Logging";
        public string ConnectionString { get; set; } = string.Empty;
        public string SenderAddress { get; set; } = string.Empty;
        public string SenderDisplayName { get; set; } = "SNIF";
    }
}