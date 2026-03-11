namespace SNIF.Core.Entities
{
    public class CreditBalance : BaseEntity
    {
        public string UserId { get; set; } = null!;
        public virtual User User { get; set; } = null!;

        public int Credits { get; set; }
        public DateTime? LastPurchasedAt { get; set; }
    }
}
