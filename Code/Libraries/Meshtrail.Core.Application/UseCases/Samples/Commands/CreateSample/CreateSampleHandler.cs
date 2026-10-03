using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Samples.Events;
using Meshtrail.Core.Contracts.Samples;
using Meshtrail.Core.Domain.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Commands.CreateSample;

public sealed class CreateSampleHandler(
    ISampleRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<CreateSampleCommand, SampleDto>
{
    public async ValueTask<SampleDto> Handle(CreateSampleCommand command, CancellationToken cancellationToken)
    {
        var sample = Sample.Create(command.Name, command.Description, currentUser.Name, timeProvider.GetUtcNow());

        await repository.AddAsync(sample, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await publisher.Publish(new SampleChangedNotification(sample.Id, SampleChangeKind.Created), cancellationToken);
        return sample.ToDto();
    }
}
