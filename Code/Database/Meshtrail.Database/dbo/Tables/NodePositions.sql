-- Position history per node (for trails later). Old rows are removed by the NodePositionRetention job.
CREATE TABLE [dbo].[NodePositions]
(
    [Id]           UNIQUEIDENTIFIER  NOT NULL,
    [NodeNum]      BIGINT            NOT NULL,
    [Latitude]     FLOAT             NOT NULL,
    [Longitude]    FLOAT             NOT NULL,
    [Altitude]     INT               NULL,
    -- When the node took the fix (if it said so); ReceivedAt = when the gateway got it.
    [PositionTime] DATETIMEOFFSET(7) NULL,
    [Precision]    INT               NOT NULL,
    [ReceivedAt]   DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_NodePositions] PRIMARY KEY NONCLUSTERED ([Id]),
    CONSTRAINT [FK_NodePositions_MeshNodes] FOREIGN KEY ([NodeNum]) REFERENCES [dbo].[MeshNodes] ([NodeNum])
);
GO

-- Clustered per node and time: a trail is one range scan. Also serves the FK.
CREATE CLUSTERED INDEX [IX_NodePositions_NodeNum_ReceivedAt] ON [dbo].[NodePositions] ([NodeNum], [ReceivedAt]);
GO

-- The retention job deletes by age across all nodes.
CREATE NONCLUSTERED INDEX [IX_NodePositions_ReceivedAt] ON [dbo].[NodePositions] ([ReceivedAt]);
GO
