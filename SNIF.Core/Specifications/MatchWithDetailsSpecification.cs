using SNIF.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Specifications
{
    public class MatchWithDetailsSpecification : BaseSpecification<Match>
    {
        public MatchWithDetailsSpecification(string matchId)
        {
            AddCriteria(m => m.Id == matchId);
            AddIncludes();
        }

        public MatchWithDetailsSpecification(Expression<Func<Match, bool>> criteria)
        {
            AddCriteria(criteria);
            AddIncludes();
        }

        private void AddIncludes()
        {
            AddInclude(m => m.InitiatiorPet);
            AddInclude(m => m.TargetPet);
            AddInclude("InitiatiorPet.Location");
            AddInclude("TargetPet.Location");
            AddInclude("InitiatiorPet.MedicalHistory");
            AddInclude("TargetPet.MedicalHistory");
            AddInclude("InitiatiorPet.Media");
            AddInclude("TargetPet.Media");
        }


    }
}
