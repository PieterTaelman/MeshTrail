using System.Globalization;
using System.Security.Cryptography;
using Meshtrail.Core.Application.Abstractions;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>6-digit codes from the cryptographic random generator (never System.Random for secrets).</summary>
internal sealed class RandomVerificationCodeGenerator : IVerificationCodeGenerator
{
    public string NewCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
}
