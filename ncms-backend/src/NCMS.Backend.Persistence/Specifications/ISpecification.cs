using System.Linq.Expressions;

namespace NCMS.Backend.Persistence.Specifications
{
    public interface ISpecification<T>
        where T : class
    {
    
        Expression<Func<T, bool>> Criteria { get; }
        IReadOnlyList<Expression<Func<T, object>>> Includes { get; }
        IReadOnlyList<string> IncludeStrings { get; }
        IReadOnlyList<OrderExpression<T>> OrderExpressions { get; }
        bool AsNoTracking { get; }
        bool AsSplitQuery { get; }
        bool IgnoreQueryFilters { get; }
    }
}