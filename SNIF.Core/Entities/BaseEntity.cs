namespace SNIF.Core.Entities
{
    public abstract class BaseEntity
    {
        public required string Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}