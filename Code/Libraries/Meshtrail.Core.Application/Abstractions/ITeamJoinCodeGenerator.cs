namespace Meshtrail.Core.Application.Abstractions;

/// <summary>Creates team join codes (8 characters people can type). An interface so tests can use a known code.</summary>
public interface ITeamJoinCodeGenerator
{
    string NewCode();
}
