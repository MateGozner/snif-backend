using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Entities
{
    public class Message : BaseEntity
    {
        public string Content { get; set; } = null!;
        public string SenderId { get; set; } = null!;
        public string ReceiverId { get; set; } = null!;
        public string MatchId { get; set; } = null!;
        public bool IsRead { get; set; }

        public virtual User Sender { get; set; } = null!;
        public virtual User Receiver { get; set; } = null!;
        public virtual Match Match { get; set; } = null!;
    }
}
