using Mediator;
using Meshtrail.Core.Contracts.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts.Queries.GetMyProfile;

public sealed record GetMyProfileQuery : IQuery<ProfileDto>;
