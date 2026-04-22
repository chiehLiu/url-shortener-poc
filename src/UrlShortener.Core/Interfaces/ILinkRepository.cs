using UrlShortener.Core.Entities;

namespace UrlShortener.Core.Interfaces;

// Every DB interaction goes through this interface.
// The rest of the app never touches ISqlSugarClient directly — same rule as api-member.
public interface ILinkRepository
{
    Task<ShortLink?> FindBySlugAsync(string slug);
    Task<int> InsertAsync(ShortLink link);
    Task IncrementClickCountAsync(string slug, long delta);
    Task<List<ShortLink>> GetTopByClickCountAsync(int n);
}
