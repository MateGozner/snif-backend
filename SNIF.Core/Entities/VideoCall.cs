namespace SNIF.Core.Entities
{
    public class VideoCall : BaseEntity
    {
        public string MatchId { get; set; } = null!;
        public virtual Match Match { get; set; } = null!;
        public string CallerUserId { get; set; } = null!;
        public virtual User Caller { get; set; } = null!;
        public string ReceiverUserId { get; set; } = null!;
        public virtual User Receiver { get; set; } = null!;
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public int DurationSeconds { get; set; }
    }
}
