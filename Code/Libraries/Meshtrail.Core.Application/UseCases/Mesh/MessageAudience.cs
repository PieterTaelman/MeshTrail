using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh;

/// <summary>
/// Who may see a message. Team chat: the team's members. A direct-message conversation: the node's verified owner and
/// everybody who wrote to the node. A verification message: only the person registering. Channel messages outside a
/// team: nobody (there is no worldwide channel).
/// </summary>
public sealed class MessageAudience(
    IMeshMessageRepository messages,
    ITeamRepository teams,
    INodeRegistrationRepository registrations)
{
    public async Task<IReadOnlyCollection<string>> ForAsync(MessageDto message, CancellationToken cancellationToken)
    {
        if (message.TeamId is { } teamId)
        {
            return await teams.GetMemberIdsAsync(teamId, cancellationToken);
        }

        if (message.Kind == nameof(MessageKind.Verification))
        {
            var stored = await messages.GetAsync(message.Id, cancellationToken);
            return stored?.CreatedById is { } author ? [author] : [];
        }

        return message.PeerNodeNum is { } nodeNum ? await ForConversationAsync(nodeNum, cancellationToken) : [];
    }

    /// <summary>The people who may read the conversation with this node.</summary>
    public async Task<IReadOnlyCollection<string>> ForConversationAsync(uint nodeNum, CancellationToken cancellationToken)
    {
        var people = new HashSet<string>(await messages.GetAuthorIdsToNodeAsync(nodeNum, cancellationToken), StringComparer.Ordinal);
        if (await registrations.GetActiveForNodeAsync(nodeNum, cancellationToken) is { Status: RegistrationStatus.Verified } registration)
        {
            people.Add(registration.UserId);
        }

        return people;
    }
}
