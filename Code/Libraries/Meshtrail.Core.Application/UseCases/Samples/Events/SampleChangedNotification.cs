using Mediator;

namespace Meshtrail.Core.Application.UseCases.Samples.Events;

public enum SampleChangeKind
{
    Created,
    Updated,
    Deleted,
}

/// <summary>Published by the sample handlers AFTER a successful save, so listeners never react to a rolled-back change.</summary>
public sealed record SampleChangedNotification(Guid SampleId, SampleChangeKind Kind) : INotification;
