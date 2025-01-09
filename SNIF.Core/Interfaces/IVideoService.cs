using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Interfaces
{
    public interface IVideoService
    {
        Task InitiateVideoCall(string matchId, string callerId, string receiverId);
        Task EndVideoCall(string matchId, string userId);
    }
}
