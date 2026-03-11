namespace SNIF.Core.Entities
{
    public class UserBlock : BaseEntity
    {
        public string BlockerUserId { get; set; } = null!;
        public virtual User BlockerUser { get; set; } = null!;

        public string BlockedUserId { get; set; } = null!;
        public virtual User BlockedUser { get; set; } = null!;
    }
}
