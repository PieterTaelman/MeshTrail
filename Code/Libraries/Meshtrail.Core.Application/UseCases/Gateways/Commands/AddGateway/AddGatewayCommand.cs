using Mediator;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.AddGateway;

/// <summary>Creates MQTT credentials for a new gateway of the current user. The password is returned this one time.</summary>
public sealed record AddGatewayCommand : ICommand<GatewayCredentialsDto>;
