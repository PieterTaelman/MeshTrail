using System.Security.Cryptography;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Domain.Teams;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>8 random capitals and digits without look-alikes (0/O, 1/I), easy to read out over the phone.</summary>
public sealed class TeamJoinCodeGenerator : ITeamJoinCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string NewCode() => RandomNumberGenerator.GetString(Alphabet, Team.JoinCodeLength);
}
