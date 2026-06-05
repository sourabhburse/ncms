using System.Linq.Expressions;
namespace NCMS.Backend.Persistence.Specifications
{
    public sealed record OrderExpression<T>(
        Expression<Func<T, object>> KeySelector,
        bool IsDescending);
}