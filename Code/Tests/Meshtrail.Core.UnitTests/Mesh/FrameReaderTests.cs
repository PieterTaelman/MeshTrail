using Meshtrail.Mesh.Framing;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

[TestClass]
public sealed class FrameReaderTests
{
    [TestMethod]
    public void Push_CompleteFrame_ReturnsPayload()
    {
        // Arrange
        var reader = new FrameReader();

        // Act
        var payloads = reader.Push(Frame(42));

        // Assert
        payloads.Count.ShouldBe(1);
        ConfigCompleteId(payloads[0]).ShouldBe(42u);
    }

    [TestMethod]
    public void Push_FrameSplitOverReads_ReturnsPayloadOnlyWhenComplete()
    {
        // Arrange
        var reader = new FrameReader();
        var frame = Frame(7);

        // Act
        var first = reader.Push(frame.AsSpan(0, 1));
        var second = reader.Push(frame.AsSpan(1, 3));
        var third = reader.Push(frame.AsSpan(4));

        // Assert
        first.ShouldBeEmpty();
        second.ShouldBeEmpty();
        third.Count.ShouldBe(1);
        ConfigCompleteId(third[0]).ShouldBe(7u);
    }

    [TestMethod]
    public void Push_TwoFramesInOneRead_ReturnsBothInOrder()
    {
        // Arrange
        var reader = new FrameReader();

        // Act
        var payloads = reader.Push([.. Frame(1), .. Frame(2)]);

        // Assert
        payloads.Select(ConfigCompleteId).ShouldBe([1u, 2u]);
    }

    [TestMethod]
    public void Push_GarbageBeforeStartMarker_SkipsGarbage()
    {
        // Arrange
        var reader = new FrameReader();
        byte[] garbage = [.. "DEBUG | boot ok\r\n"u8, MeshFrame.Start1, 0x00];

        // Act
        var payloads = reader.Push([.. garbage, .. Frame(9)]);

        // Assert
        payloads.Count.ShouldBe(1);
        ConfigCompleteId(payloads[0]).ShouldBe(9u);
        reader.DiscardedBytes.ShouldBe(garbage.Length);
    }

    [TestMethod]
    public void Push_StartMarkerSplitOverReads_StillFindsFrame()
    {
        // Arrange
        var reader = new FrameReader();
        var frame = Frame(5);

        // Act
        var first = reader.Push([0x41, .. frame.AsSpan(0, 1)]);
        var second = reader.Push(frame.AsSpan(1));

        // Assert
        first.ShouldBeEmpty();
        second.Count.ShouldBe(1);
        ConfigCompleteId(second[0]).ShouldBe(5u);
    }

    [TestMethod]
    public void Push_OversizedLength_ResyncsOnNextFrame()
    {
        // Arrange
        var reader = new FrameReader();
        var tooLong = MeshFrame.MaxPayloadLength + 1;
        byte[] bogusHeader = [MeshFrame.Start1, MeshFrame.Start2, (byte)(tooLong >> 8), (byte)tooLong];

        // Act
        var payloads = reader.Push([.. bogusHeader, .. Frame(3)]);

        // Assert
        payloads.Count.ShouldBe(1);
        ConfigCompleteId(payloads[0]).ShouldBe(3u);
    }

    [TestMethod]
    public void Push_PayloadEndingWithStartByte_DoesNotReadItAsNewFrame()
    {
        // Arrange
        var reader = new FrameReader();
        byte[] frame = [MeshFrame.Start1, MeshFrame.Start2, 0x00, 0x01, MeshFrame.Start1];

        // Act
        var payloads = reader.Push(frame);
        var next = reader.Push(Frame(11));

        // Assert
        payloads.Single().ShouldBe([MeshFrame.Start1]);
        next.Select(ConfigCompleteId).ShouldBe([11u]);
    }
}
