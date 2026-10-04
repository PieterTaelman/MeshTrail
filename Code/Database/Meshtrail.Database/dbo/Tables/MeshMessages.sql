-- Text messages sent and received over the mesh (channel broadcasts and direct messages).
CREATE TABLE [dbo].[MeshMessages]
(
    [Id]            UNIQUEIDENTIFIER  NOT NULL,
    -- Inbound | Outbound
    [Direction]     NVARCHAR(10)      NOT NULL,
    -- Text | Verification (verification codes are never shown in the chat)
    [Kind]          NVARCHAR(20)      NOT NULL,
    [ChannelIndex]  INT               NOT NULL,
    -- Sender; NULL for our own messages before the gateway number is known.
    [FromNodeNum]   BIGINT            NULL,
    -- NULL = broadcast to the channel; otherwise a direct message to this node.
    [ToNodeNum]     BIGINT            NULL,
    -- Radio text is limited to ~233 bytes, so 256 characters always fit.
    [Text]          NVARCHAR(256)     NOT NULL,
    [PacketId]      BIGINT            NOT NULL,
    -- Queued | Sent | Acked | Failed (outbound) or Received (inbound)
    [Status]        NVARCHAR(20)      NOT NULL,
    [FailureReason] NVARCHAR(100)     NULL,
    [Snr]           FLOAT             NULL,
    [Rssi]          INT               NULL,
    [HopsAway]      INT               NULL,
    [CreatedAt]     DATETIMEOFFSET(7) NOT NULL,
    [CreatedBy]     NVARCHAR(256)     NULL,
    [SentAt]        DATETIMEOFFSET(7) NULL,
    [AckedAt]       DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_MeshMessages] PRIMARY KEY NONCLUSTERED ([Id])
);
GO

CREATE CLUSTERED INDEX [IX_MeshMessages_CreatedAt] ON [dbo].[MeshMessages] ([CreatedAt]);
GO

-- Channel tab: broadcasts on one channel, newest first.
CREATE NONCLUSTERED INDEX [IX_MeshMessages_Channel_CreatedAt] ON [dbo].[MeshMessages] ([ChannelIndex], [CreatedAt])
    WHERE [ToNodeNum] IS NULL;
GO

-- Direct-message tab: conversation with one node (we look up both directions).
CREATE NONCLUSTERED INDEX [IX_MeshMessages_From_CreatedAt] ON [dbo].[MeshMessages] ([FromNodeNum], [CreatedAt]);
GO
CREATE NONCLUSTERED INDEX [IX_MeshMessages_To_CreatedAt] ON [dbo].[MeshMessages] ([ToNodeNum], [CreatedAt]);
GO

-- Matching delivery reports to our packets, and the dispatcher's look-ups by status.
CREATE NONCLUSTERED INDEX [IX_MeshMessages_PacketId] ON [dbo].[MeshMessages] ([PacketId]);
GO
CREATE NONCLUSTERED INDEX [IX_MeshMessages_Status] ON [dbo].[MeshMessages] ([Status]) WHERE [Direction] = N'Outbound';
GO
