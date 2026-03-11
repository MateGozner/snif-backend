namespace SNIF.Core.Entities
{
    public class MessageReaction : BaseEntity
    {
        public string MessageId { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public string Emoji { get; set; } = null!; // e.g., "❤️", "😂", "👍", "😮", "😢", "🙏"

        public virtual Message Message { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
