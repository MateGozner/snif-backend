using Microsoft.AspNetCore.Mvc;
using SNIF.API.Filters;
using SNIF.Core.Enums;

namespace SNIF.API.Attributes
{
    public class EnforceUsageLimitAttribute : TypeFilterAttribute
    {
        public EnforceUsageLimitAttribute(UsageType usageType)
            : base(typeof(UsageEnforcementFilter))
        {
            Arguments = [usageType];
        }
    }
}
