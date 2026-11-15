using BankarskiSistem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Repositories
{
    internal static class QueryableExtensions
    {
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PageRequest page)
        {
            var totalCount = await query.CountAsync();
            var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync();

            return new PagedResult<T>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page.Page,
                PageSize = page.PageSize
            };
        }

        public static string? ToLikePattern(this string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return null;

            var escaped = search.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

            return $"%{escaped}%";
        }
    }
}
