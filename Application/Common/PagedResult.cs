namespace Application.Common;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public bool HasPreviousPage => Page > 1 && TotalPages > 0;
    public bool HasNextPage => Page < TotalPages;
}
