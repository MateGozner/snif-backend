using System.Linq.Expressions;
using SNIF.Core.Entities;

namespace SNIF.Core.Specifications
{
    public class PetWithDetailsSpecification : BaseSpecification<Pet>
    {
        public PetWithDetailsSpecification(string id)
        {
            AddCriteria(x => x.Id == id);
            AddInclude(x => x.MedicalHistory!);
            AddInclude(x => x.Location!);
            AddInclude(x => x.Media);
            AddInclude(x => x.DiscoveryPreferences!);
        }

        public PetWithDetailsSpecification(Expression<Func<Pet, bool>> criteria)
        {
            AddCriteria(criteria);
            AddInclude(x => x.MedicalHistory!);
            AddInclude(x => x.Location!);
            AddInclude(x => x.Media);
            AddInclude(x => x.DiscoveryPreferences!);
        }
    }

    public class PetWithMediaSpecification : BaseSpecification<Pet>
    {
        public PetWithMediaSpecification(string mediaId)
        {
            AddCriteria(x => x.Media.Any(m => m.Id == mediaId));
            AddInclude(x => x.Media);
        }
    }

}