using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMessages;

public sealed class GetMessagesValidator : AbstractValidator<GetMessagesQuery>
{
    public const int MaxPageSize = 200;

    public GetMessagesValidator()
    {
        RuleFor(query => query.Request.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.Request.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(query => query.Request.Node).NotEqual(0u).NotEqual(uint.MaxValue).When(query => query.Request.Node is not null);
        RuleFor(query => query.Request)
            .Must(request => request.Node is null != request.Team is null)
            .WithMessage("Give exactly one of 'node' (a conversation) or 'team' (team chat).");
    }
}
