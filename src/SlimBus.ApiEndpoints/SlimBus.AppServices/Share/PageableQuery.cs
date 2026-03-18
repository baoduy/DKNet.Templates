namespace SlimBus.AppServices.Share;

/// <summary>
///
/// </summary>
public record PageableQuery
{
    public bool? IsDescending { get; init; }
    public string? OrderBy { get; init; }
    public int? PageNumber { get; init; } = 1;
    public int PageNumberValue => PageNumber ?? 1;
    public int? PageSize { get; init; } = 100;
    public int PageSizeValue => PageSize ?? 100;
    public string? SearchText { get; init; }
}