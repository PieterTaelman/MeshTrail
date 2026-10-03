using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodeById;

public sealed class GetNodeByIdValidator : AbstractValidator<GetNodeByIdQuery>
{
    public GetNodeByIdValidator()
    {
        RuleFor(query => query.NodeNum).NotEqual(0u);
    }
}
