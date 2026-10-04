using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.Repositories;

/// <summary>Storage for <see cref="NodeRegistration"/>. SaveChangesAsync throws ConcurrencyException on a conflict.</summary>
public interface INodeRegistrationRepository
{
    Task<NodeRegistration?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The Claimed or Verified registration of a node, if any (there is at most one).</summary>
    Task<NodeRegistration?> GetActiveForNodeAsync(uint nodeNum, CancellationToken cancellationToken);

    Task<NodeRegistration?> GetByVerificationMessageAsync(Guid messageId, CancellationToken cancellationToken);

    Task<IReadOnlyList<NodeRegistration>> GetActiveForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Which of these nodes have a verified registration.</summary>
    Task<IReadOnlySet<uint>> GetVerifiedNodeNumsAsync(IReadOnlyCollection<uint> nodeNums, CancellationToken cancellationToken);

    Task AddAsync(NodeRegistration registration, CancellationToken cancellationToken);

    Task UpdateAsync(NodeRegistration registration, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
