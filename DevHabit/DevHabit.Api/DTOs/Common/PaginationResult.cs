using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.DTOs.Common;

public sealed record PaginationResult<T> : ICollectionResponse<T>
{
    public List<T> Item { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    public static async Task<PaginationResult<T>> CreateAsync(IQueryable<T> query, int page, int PageSize)
    {
        int totalCount = await query.CountAsync();
        List<T> items = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        return new PaginationResult<T>
        {
            Item = items,
            Page = page,
            PageSize = PageSize,
            TotalCount = totalCount
        };
    } 
}
