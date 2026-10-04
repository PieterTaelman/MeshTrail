using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

[TestClass]
public sealed class NodeRegistrationTests
{
    [TestMethod]
    public void Claim_ValidInput_IsClaimedWithCodeValidFor15Minutes()
    {
        // Act
        var registration = ClaimedRegistration();

        // Assert
        registration.Status.ShouldBe(RegistrationStatus.Claimed);
        registration.CodeExpiresAt.ShouldBe(Now.AddMinutes(15));
        registration.AttemptsLeft.ShouldBe(NodeRegistration.MaxAttempts);
        registration.CodeHash.ShouldNotBeNull();
    }

    [TestMethod]
    public void Claim_StoresOnlyAHashOfTheCode()
    {
        // Act
        var registration = ClaimedRegistration();

        // Assert
        registration.CodeHash!.Length.ShouldBe(32);
        System.Text.Encoding.UTF8.GetString(registration.CodeHash).ShouldNotContain("123456");
    }

    [TestMethod]
    public void Claim_LinkKeyDiffersFromBroadcastKey_Throws()
    {
        // Act + Assert
        Should.Throw<DomainException>(() => NodeRegistration.Claim(
            HikerNodeNum, UserName, UserName, "Hiker", "HKR", GatewayPublicKey, new byte[32], "123456", Now));
    }

    [TestMethod]
    public void Claim_NoPublicKey_Throws()
    {
        // Act + Assert
        Should.Throw<DomainException>(() => NodeRegistration.Claim(HikerNodeNum, UserName, UserName, "Hiker", "HKR", [], null, "123456", Now));
    }

    [TestMethod]
    [DataRow("12345")]
    [DataRow("12345a")]
    [DataRow("1234567")]
    public void Claim_CodeNotSixDigits_Throws(string code)
    {
        // Act + Assert
        Should.Throw<DomainException>(() => NodeRegistration.Claim(HikerNodeNum, UserName, UserName, "Hiker", "HKR", GatewayPublicKey, null, code, Now));
    }

    [TestMethod]
    public void Verify_RightCode_IsVerifiedAndCodeIsForgotten()
    {
        // Arrange
        var registration = ClaimedRegistration();

        // Act
        var result = registration.Verify(" 123456 ", Now.AddMinutes(5));

        // Assert
        result.ShouldBe(VerificationResult.Verified);
        registration.Status.ShouldBe(RegistrationStatus.Verified);
        registration.VerifiedAt.ShouldBe(Now.AddMinutes(5));
        registration.CodeHash.ShouldBeNull();
    }

    [TestMethod]
    public void Verify_WrongCode_CountsAttempt()
    {
        // Arrange
        var registration = ClaimedRegistration();

        // Act
        var result = registration.Verify("654321", Now);

        // Assert
        result.ShouldBe(VerificationResult.WrongCode);
        registration.Status.ShouldBe(RegistrationStatus.Claimed);
        registration.AttemptsLeft.ShouldBe(4);
    }

    [TestMethod]
    public void Verify_FifthWrongCode_LocksAndRevokes()
    {
        // Arrange
        var registration = ClaimedRegistration();
        for (var i = 0; i < 4; i++)
        {
            registration.Verify("000000", Now);
        }

        // Act
        var result = registration.Verify("000000", Now);

        // Assert
        result.ShouldBe(VerificationResult.Locked);
        registration.Status.ShouldBe(RegistrationStatus.Revoked);
        registration.RevokedReason.ShouldBe("Too many wrong codes.");
    }

    [TestMethod]
    public void Verify_RightCodeAfterLock_Throws()
    {
        // Arrange
        var registration = ClaimedRegistration();
        for (var i = 0; i < NodeRegistration.MaxAttempts; i++)
        {
            registration.Verify("000000", Now);
        }

        // Act + Assert
        Should.Throw<DomainException>(() => registration.Verify("123456", Now));
    }

    [TestMethod]
    public void Verify_AfterExpiry_Throws()
    {
        // Arrange
        var registration = ClaimedRegistration();

        // Act + Assert
        Should.Throw<DomainException>(() => registration.Verify("123456", Now.AddMinutes(16)));
    }

    [TestMethod]
    public void IsExpired_ClaimOlderThanCodeLifetime_IsTrue()
    {
        // Arrange
        var registration = ClaimedRegistration();

        // Act + Assert
        registration.IsExpired(Now.AddMinutes(14)).ShouldBeFalse();
        registration.IsExpired(Now.AddMinutes(16)).ShouldBeTrue();
    }

    [TestMethod]
    public void Revoke_Verified_IsRevokedAndNoLongerActive()
    {
        // Arrange
        var registration = ClaimedRegistration();
        registration.Verify("123456", Now);

        // Act
        registration.Revoke("Removed by the user.", Now);

        // Assert
        registration.Status.ShouldBe(RegistrationStatus.Revoked);
        registration.IsActive.ShouldBeFalse();
    }
}
