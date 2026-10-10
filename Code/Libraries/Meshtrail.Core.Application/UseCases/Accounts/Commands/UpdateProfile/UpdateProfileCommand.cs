using Mediator;
using Meshtrail.Core.Contracts.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.UpdateProfile;

/// <summary>The signed-in user changes their name.</summary>
public sealed record UpdateProfileCommand(string FirstName, string LastName) : ICommand<ProfileDto>;
