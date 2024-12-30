using SNIF.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Interfaces
{
    public interface INotificationService
    {
        Task NotifyNewMatch(string userId, MatchDto match);
        Task NotifyMatchStatusUpdate(string userId, MatchDto match);
    }
}
