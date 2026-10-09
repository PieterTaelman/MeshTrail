using Mediator;

namespace Meshtrail.Core.Application.UseCases.Gateways.Queries.AuthenticateGateway;

/// <summary>The broker asks: is this an active gateway login with the right password?</summary>
public sealed record AuthenticateGatewayQuery(string? UserName, string? Password) : IQuery<bool>;
