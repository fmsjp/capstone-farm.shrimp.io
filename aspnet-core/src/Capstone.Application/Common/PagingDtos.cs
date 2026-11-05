using Abp.Application.Services.Dto;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Capstone.Common;

public class PagedInput
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    [Range(0, int.MaxValue)]
    public int SkipCount { get; set; }

    [Range(1, MaxPageSize)]
    public int MaxResultCount { get; set; } = DefaultPageSize;

    [StringLength(60)]
    public string Sorting { get; set; }

    [StringLength(60)]
    public string Search { get; set; }
}

public static class SearchText
{
    public static bool Matches(string search, params string[] fields)
    {
        var words = (search ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.All(w => fields.Any(f => f != null && f.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }
}

public readonly record struct SortSpec(string Key, bool Descending)
{
    public static SortSpec Parse(string sorting, IReadOnlyCollection<string> allowed, string defaultKey, bool defaultDescending)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return new SortSpec(defaultKey, defaultDescending);
        }

        var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var key = allowed.FirstOrDefault(a => string.Equals(a, parts[0], StringComparison.OrdinalIgnoreCase));
        if (key == null)
        {
            return new SortSpec(defaultKey, defaultDescending);
        }

        var descending = parts.Length > 1 ? string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase) : false;
        return new SortSpec(key, descending);
    }

    public IOrderedQueryable<T> Apply<T, TKey>(IQueryable<T> query, System.Linq.Expressions.Expression<Func<T, TKey>> key) =>
        Descending ? query.OrderByDescending(key) : query.OrderBy(key);

    public IOrderedEnumerable<T> Apply<T, TKey>(IEnumerable<T> source, Func<T, TKey> key) =>
        Descending ? source.OrderByDescending(key) : source.OrderBy(key);
}

public static class PagingExtensions
{
    public static async Task<PagedResultDto<T>> ToPageAsync<T>(this IQueryable<T> ordered, PagedInput input)
    {
        var total = await ordered.CountAsync();
        var items = await ordered.Skip(input.SkipCount).Take(input.MaxResultCount).ToListAsync();
        return new PagedResultDto<T>(total, items);
    }

    public static PagedResultDto<T> ToPage<T>(this IReadOnlyList<T> ordered, PagedInput input) =>
        new(ordered.Count, ordered.Skip(input.SkipCount).Take(input.MaxResultCount).ToList());
}
