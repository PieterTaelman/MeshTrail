using Mediator;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.RevokeGateway;

/// <summary>The owner removes a gateway: its login stops working and it is no longer used for sending.</summary>
public sealed record RevokeGatewayCommand(Guid Id) : ICommand;
