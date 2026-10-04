namespace Meshtrail.Core.Application.Abstractions;

/// <summary>Creates registration codes. An interface so tests can use a known code.</summary>
public interface IVerificationCodeGenerator
{
    /// <summary>6 random digits from a cryptographic random generator.</summary>
    string NewCode();
}
