-- Traceroutes we asked for and their result (the list of relays between the gateway and the node).
CREATE TABLE [dbo].[NodeTraceroutes]
(
    [Id]           UNIQUEIDENTIFIER  NOT NULL,
    [NodeNum]      BIGINT            NOT NULL,
    -- Id of the packet we sent; the answer carries it as request_id.
    [PacketId]     BIGINT            NOT NULL,
    [Status]       NVARCHAR(20)      NOT NULL,
    [RequestedAt]  DATETIMEOFFSET(7) NOT NULL,
    [RequestedBy]  NVARCHAR(256)     NOT NULL,
    [CompletedAt]  DATETIMEOFFSET(7) NULL,
    -- Comma-separated node numbers / SNR values (dB, empty = unknown), in hop order.
    [RouteTowards] NVARCHAR(400)     NULL,
    [SnrTowards]   NVARCHAR(400)     NULL,
    [RouteBack]    NVARCHAR(400)     NULL,
    [SnrBack]      NVARCHAR(400)     NULL,
    CONSTRAINT [PK_NodeTraceroutes] PRIMARY KEY NONCLUSTERED ([Id]),
    CONSTRAINT [FK_NodeTraceroutes_MeshNodes] FOREIGN KEY ([NodeNum]) REFERENCES [dbo].[MeshNodes] ([NodeNum])
);
GO

CREATE CLUSTERED INDEX [IX_NodeTraceroutes_NodeNum_RequestedAt] ON [dbo].[NodeTraceroutes] ([NodeNum], [RequestedAt]);
GO

-- Matching an incoming answer to its request.
CREATE NONCLUSTERED INDEX [IX_NodeTraceroutes_PacketId] ON [dbo].[NodeTraceroutes] ([PacketId]);
GO
