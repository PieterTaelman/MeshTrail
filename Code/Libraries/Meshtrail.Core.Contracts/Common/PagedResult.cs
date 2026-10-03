namespace Meshtrail.Core.Contracts.Common;

/// <summary>One page of a grid plus the total row count, so the client can draw the pager.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
