using SNIF.Core.DTOs;
using SNIF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Interfaces
{
    public interface IMatchService
    {
        Task<MatchDto> CreateMatchAsync(string initiatorPetId, CreateMatchDto createMatchDto);
        Task<MatchDto> GetMatchByIdAsync(string matchId);
        Task<IEnumerable<MatchDto>> GetPetMatchesAsync(string petId);
        Task<IEnumerable<PetDto>> GetPotentialMatchesAsync(string petId, PetPurpose purpose);
        Task<MatchDto> UpdateMatchStatusAsync(string matchId, MatchStatus status);
        Task DeleteMatchAsync(string matchId);
        Task<IEnumerable<MatchDto>> GetPendingMatchesForPetAsync(string petId);
    }
}
