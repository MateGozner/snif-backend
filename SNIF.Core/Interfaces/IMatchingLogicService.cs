using SNIF.Core.Entities;
using SNIF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Interfaces
{
    public interface IMatchingLogicService
    {
        Task<IEnumerable<(Pet Pet, double Distance)>> FindPotentialMatches(
            Pet sourcePet,
            IEnumerable<Pet> potentialMatches,
            User ownerWithPreferences,
            PetPurpose? purposeFilter = null);
    }
}
