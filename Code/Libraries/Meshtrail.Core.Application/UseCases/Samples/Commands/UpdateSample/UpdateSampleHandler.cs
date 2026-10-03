using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Samples.Events;
using Meshtrail.Core.Contracts.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Commands.UpdateSample;

/// <summary>Load → check it exists → let the domain apply the change → save with the client's version → map.</summary>
public sealed class UpdateSampleHandler(
    ISampleRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<UpdateSampleCommand, SampleDto>
{
    public async ValueTask<SampleDto> Handle(UpdateSampleCommand command, CancellationToken cancellationToken)
    {
        var sample = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sample {command.Id} does not exist.");

        sample.Update(command.Name, command.Description, currentUser.Name, timeProvider.GetUtcNow());

        // Passing the client's version makes the save fail (409) if someone else changed the row meanwhile.
        await repository.UpdateAsync(sample, Convert.FromBase64String(command.RowVersion), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await publisher.Publish(new SampleChangedNotification(sample.Id, SampleChangeKind.Updated), cancellationToken);
        return sample.ToDto();
    }
}
