namespace Application.Dtos.Business;

public class BusinessListQuery
{
    public string? Search { get; set; }
    public string? City { get; set; }
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
