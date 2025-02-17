using SNIF.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Specifications
{
    public class UserWithDetailsSpecification : BaseSpecification<User>
    {
        public UserWithDetailsSpecification(string userId)
        {
            AddCriteria(u => u.Id == userId);
            AddIncludes();
        }

        public UserWithDetailsSpecification(Expression<Func<User, bool>> criteria)
        {
            AddCriteria(criteria);
            AddIncludes();
        }

        private void AddIncludes()
        {
            AddInclude(u => u.Location!);
            AddInclude(u => u.Pets!);
            AddInclude("Preferences");
            AddInclude("Preferences.NotificationSettings");
        }
    }
}
