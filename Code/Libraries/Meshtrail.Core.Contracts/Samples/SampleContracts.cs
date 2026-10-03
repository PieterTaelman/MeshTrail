namespace Meshtrail.Core.Contracts.Samples;

/// <summary>Full sample as returned by the API. RowVersion is base64 and must be sent back on update.</summary>
public sealed record SampleDto(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? ModifiedAt,
    string? ModifiedBy,
    string RowVersion);

public sealed record CreateSampleRequest(string Name, string? Description);

public sealed record UpdateSampleRequest(string Name, string? Description, string RowVersion);

/// <summary>Query-string parameters of the samples grid. Page is 1-based.</summary>
public sealed record SampleGridRequest
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    /// <summary>Free text matched against name and description.</summary>
    public string? Search { get; init; }

    public string? CreatedBy { get; init; }

    /// <summary>One of <see cref="SampleGridSortColumns"/>.</summary>
    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }
}

/// <summary>Column names the grid may sort on. Shared so client and server use the same strings.</summary>
public static class SampleGridSortColumns
{
    public const string Name = "name";
    public const string CreatedAt = "createdAt";
    public const string CreatedBy = "createdBy";

    public static readonly IReadOnlyList<string> All = [Name, CreatedAt, CreatedBy];
}

/// <summary>Light row for the grid: only what the list shows.</summary>
public sealed record SampleGridItemDto(Guid Id, string Name, string? Description, DateTimeOffset CreatedAt, string CreatedBy);

/// <summary>Values the grid filter dropdowns can offer.</summary>
public sealed record SampleFilterOptionsDto(IReadOnlyList<string> CreatedBy);
