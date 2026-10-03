using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Samples.Events;

namespace Meshtrail.Core.Application.UseCases.Samples.Commands.DeleteSample;

public sealed class DeleteSampleHandler(ISampleRepository repository, IPublisher publisher) : ICommandHandler<DeleteSampleCommand>
{
    public async ValueTask<Unit> Handle(DeleteSampleCommand command, CancellationToken cancellationToken)
    {
        var sample = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sample {command.Id} does not exist.");

        await repository.DeleteAsync(sample, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await publisher.Publish(new SampleChangedNotification(sample.Id, SampleChangeKind.Deleted), cancellationToken);
        return Unit.Value;
    }
}
