using SNIF.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Specifications
{
    public class MessageSpecification : BaseSpecification<Message>
    {
        public MessageSpecification(string matchId)
        {
            AddCriteria(m => m.MatchId == matchId);
            AddIncludes();
        }

        public MessageSpecification(Expression<Func<Message, bool>> criteria)
        {
            AddCriteria(criteria);
            AddIncludes();
        }

        private void AddIncludes()
        {
            AddInclude(m => m.Sender);
            AddInclude(m => m.Receiver);
            AddInclude(m => m.Match);
        }
    }

    public class ChatSummarySpecification : BaseSpecification<Message>
    {
        public ChatSummarySpecification(string userId)
        {
            AddCriteria(m => m.SenderId == userId || m.ReceiverId == userId);
            AddInclude(m => m.Sender);
            AddInclude(m => m.Receiver);
            AddInclude(m => m.Match);
        }
    }
}
