using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Samples;
using Shouldly;
using static Meshtrail.Core.UnitTests.Samples.SampleTestHelpers;

namespace Meshtrail.Core.UnitTests.Samples;

[TestClass]
public sealed class SampleTests
{
    [TestMethod]
    public void Create_ValidInput_TrimsAndSetsAuditFields()
    {
        // Act
        var sample = Sample.Create("  Name  ", "  Description  ", UserName, Now);

        // Assert
        sample.Id.ShouldNotBe(Guid.Empty);
        sample.Name.ShouldBe("Name");
        sample.Description.ShouldBe("Description");
        sample.CreatedAt.ShouldBe(Now);
        sample.CreatedBy.ShouldBe(UserName);
        sample.ModifiedAt.ShouldBeNull();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_BlankName_Throws(string name)
    {
        // Act + Assert
        Should.Throw<DomainException>(() => Sample.Create(name, null, UserName, Now));
    }

    [TestMethod]
    public void Create_NameTooLong_Throws()
    {
        // Arrange
        var name = new string('x', Sample.NameMaxLength + 1);

        // Act + Assert
        Should.Throw<DomainException>(() => Sample.Create(name, null, UserName, Now));
    }

    [TestMethod]
    public void Create_WhitespaceDescription_StoresNull()
    {
        // Act
        var sample = Sample.Create("Name", "   ", UserName, Now);

        // Assert
        sample.Description.ShouldBeNull();
    }

    [TestMethod]
    public void Update_ValidInput_ChangesDetailsAndModifiedFields()
    {
        // Arrange
        var sample = ExistingSample();
        var later = Now.AddHours(1);

        // Act
        sample.Update("New name", null, "editor", later);

        // Assert
        sample.Name.ShouldBe("New name");
        sample.Description.ShouldBeNull();
        sample.ModifiedAt.ShouldBe(later);
        sample.ModifiedBy.ShouldBe("editor");
        sample.CreatedBy.ShouldBe("creator");
    }

    [TestMethod]
    public void Update_BlankUser_Throws()
    {
        // Arrange
        var sample = ExistingSample();

        // Act + Assert
        Should.Throw<DomainException>(() => sample.Update("Name", null, " ", Now));
    }
}
