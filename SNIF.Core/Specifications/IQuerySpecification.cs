using System.Linq.Expressions;

namespace SNIF.Core.Specifications
{
    public interface IQuerySpecification<T>
    {
        Expression<Func<T, bool>>? Criteria { get; }
        List<Expression<Func<T, object>>> Includes { get; }
        List<string> IncludeStrings { get; }
    }
}