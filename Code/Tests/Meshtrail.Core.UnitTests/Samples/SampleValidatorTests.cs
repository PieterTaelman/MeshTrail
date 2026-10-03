using Meshtrail.Core.Application.UseCases.Samples.Commands.CreateSample;
using Meshtrail.Core.Application.UseCases.Samples.Commands.UpdateSample;
using Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleGrid;
using Meshtrail.Core.Contracts.Samples;
using Meshtrail.Core.Domain.Samples;
using Shouldly;

namespace Meshtrail.Core.UnitTests.Samples;

[TestClass]
public sealed class SampleValidatorTests
{
    [TestMethod]
    public void CreateValidator_ValidCommand_HasNoErrors()
    {
        // Act
        var result = new CreateSampleValidator().Validate(new CreateSampleCommand("Name", null));

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [TestMethod]
    public void CreateValidator_EmptyAndTooLong_ReportsBothFields()
    {
        // Arrange
        var command = new CreateSampleCommand(string.Empty, new string('x', Sample.DescriptionMaxLength + 1));

        // Act
        var result = new CreateSampleValidator().Validate(command);

        // Assert
        result.Errors.Select(error => error.PropertyName).ShouldBe(["Name", "Description"], ignoreOrder: true);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not base64!")]
    public void UpdateValidator_BadRowVersion_ReportsRowVersion(string rowVersion)
    {
        // Act
        var result = new UpdateSampleValidator().Validate(new UpdateSampleCommand(Guid.NewGuid(), "Name", null, rowVersion));

        // Assert
        result.Errors.ShouldContain(error => error.PropertyName == "RowVersion");
    }

    [TestMethod]
    public void UpdateValidator_EmptyId_ReportsId()
    {
        // Act
        var result = new UpdateSampleValidator().Validate(new UpdateSampleCommand(Guid.Empty, "Name", null, "AQ=="));

        // Assert
        result.Errors.ShouldContain(error => error.PropertyName == "Id");
    }

    [TestMethod]
    public void GridValidator_PageSizeTooLarge_ReportsPageSize()
    {
        // Arrange
        var query = new GetSampleGridQuery(new SampleGridRequest { PageSize = GetSampleGridValidator.MaxPageSize + 1 });

        // Act
        var result = new GetSampleGridValidator().Validate(query);

        // Assert
        result.Errors.ShouldContain(error => error.PropertyName == "Request.PageSize");
    }
}
