using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.Repositories;

/// <summary>Storage for <see cref="NodeReception"/> ("gateway G heard node N").</summary>
public interface INodeReceptionRepository
{
    /// <summary>Every gateway that ever heard this node (read-only).</summary>
    Task<IReadOnlyList<NodeReception>> GetForNodeAsync(uint nodeNum, CancellationToken cancellationToken);

    /// <summary>Records a reception: creates the row or updates it when the new one is not older. Staged; written by SaveChangesAsync.</summary>
    Task RecordAsync(uint nodeNum, uint gatewayNodeNum, DateTimeOffset heardAt, double? snr, int? rssi, int? hopsAway, DateTimeOffset now, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
