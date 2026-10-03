using Mediator;
using Meshtrail.Core.Application.UseCases.Samples.Commands.CreateSample;
using Meshtrail.Core.Application.UseCases.Samples.Commands.DeleteSample;
using Meshtrail.Core.Application.UseCases.Samples.Commands.UpdateSample;
using Meshtrail.Core.Application.UseCases.Samples.Events;
using Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleById;
using Meshtrail.Core.Domain.Samples;
using Moq;
using Shouldly;
using static Meshtrail.Core.UnitTests.Samples.SampleTestHelpers;

namespace Meshtrail.Core.UnitTests.Samples;

[TestClass]
public sealed class SampleHandlerTests
{
    [TestMethod]
    public async Task Create_ValidCommand_AddsSavesPublishesAndReturnsDto()
    {
        // Arrange
        var repository = RepositoryReturning(null);
        var publisher = Publisher();
        var handler = new CreateSampleHandler(repository.Object, CurrentUser().Object, FixedTime(), publisher.Object);

        // Act
        var result = await handler.Handle(new CreateSampleCommand("Name", "Description"), CancellationToken.None);

        // Assert
        result.Name.ShouldBe("Name");
        result.CreatedBy.ShouldBe(UserName);
        result.CreatedAt.ShouldBe(Now);
        repository.Verify(repo => repo.AddAsync(It.Is<Sample>(s => s.Id == result.Id), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(pub => pub.Publish(new SampleChangedNotification(result.Id, SampleChangeKind.Created), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Update_ExistingSample_PassesClientRowVersionToRepository()
    {
        // Arrange
        var sample = ExistingSample();
        var repository = RepositoryReturning(sample);
        var handler = new UpdateSampleHandler(repository.Object, CurrentUser().Object, FixedTime(), Publisher().Object);
        byte[] clientRowVersion = [9, 9, 9, 9, 9, 9, 9, 9];

        // Act
        var result = await handler.Handle(
            new UpdateSampleCommand(sample.Id, "Renamed", null, Convert.ToBase64String(clientRowVersion)),
            CancellationToken.None);

        // Assert
        result.Name.ShouldBe("Renamed");
        result.ModifiedBy.ShouldBe(UserName);
        repository.Verify(repo => repo.UpdateAsync(sample, clientRowVersion, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Update_UnknownSample_ThrowsKeyNotFoundAndDoesNotSave()
    {
        // Arrange
        var repository = RepositoryReturning(null);
        var handler = new UpdateSampleHandler(repository.Object, CurrentUser().Object, FixedTime(), Publisher().Object);

        // Act + Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await handler.Handle(new UpdateSampleCommand(Guid.NewGuid(), "Name", null, "AQ=="), CancellationToken.None));
        repository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Delete_ExistingSample_DeletesSavesAndPublishes()
    {
        // Arrange
        var sample = ExistingSample();
        var repository = RepositoryReturning(sample);
        var publisher = Publisher();
        var handler = new DeleteSampleHandler(repository.Object, publisher.Object);

        // Act
        var result = await handler.Handle(new DeleteSampleCommand(sample.Id), CancellationToken.None);

        // Assert
        result.ShouldBe(Unit.Value);
        repository.Verify(repo => repo.DeleteAsync(sample, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(pub => pub.Publish(new SampleChangedNotification(sample.Id, SampleChangeKind.Deleted), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetById_UnknownSample_ThrowsKeyNotFound()
    {
        // Arrange
        var handler = new GetSampleByIdHandler(RepositoryReturning(null).Object);

        // Act + Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await handler.Handle(new GetSampleByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [TestMethod]
    public async Task GetById_ExistingSample_ReturnsBase64RowVersion()
    {
        // Arrange
        var sample = ExistingSample();
        var handler = new GetSampleByIdHandler(RepositoryReturning(sample).Object);

        // Act
        var result = await handler.Handle(new GetSampleByIdQuery(sample.Id), CancellationToken.None);

        // Assert
        result.Id.ShouldBe(sample.Id);
        result.RowVersion.ShouldBe(Convert.ToBase64String(StoredRowVersion));
    }
}
