using FluentValidation;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMessages;

public sealed class GetMessagesValidator : AbstractValidator<GetMessagesQuery>
{
    public const int MaxPageSize = 200;

    public GetMessagesValidator()
    {
        RuleFor(query => query.Request.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.Request.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(query => query.Request.Channel).InclusiveBetween(0, MeshMessage.MaxChannelIndex);
        RuleFor(query => query.Request)
            .Must(request => request.Channel is null != request.Node is null)
            .WithMessage("Give exactly one of 'channel' or 'node'.");
    }
}
