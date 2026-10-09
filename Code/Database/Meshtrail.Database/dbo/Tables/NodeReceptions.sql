-- "Gateway G heard node N": latest time, signal and hops per node and gateway. Tells us which gateway can reach a node.
CREATE TABLE [dbo].[NodeReceptions]
(
    [NodeNum]        BIGINT            NOT NULL,
    -- Node number of the gateway (no FK: the gateway row may be revoked, the reception stays useful history).
    [GatewayNodeNum] BIGINT            NOT NULL,
    [LastHeardAt]    DATETIMEOFFSET(7) NOT NULL,
    [Snr]            FLOAT             NULL,
    [Rssi]           INT               NULL,
    [HopsAway]       INT               NULL,
    -- Clustered per node: "who heard this node" is one range scan. Also serves the FK.
    CONSTRAINT [PK_NodeReceptions] PRIMARY KEY CLUSTERED ([NodeNum], [GatewayNodeNum]),
    CONSTRAINT [FK_NodeReceptions_MeshNodes] FOREIGN KEY ([NodeNum]) REFERENCES [dbo].[MeshNodes] ([NodeNum])
);
GO

-- "Which nodes does this gateway hear".
CREATE NONCLUSTERED INDEX [IX_NodeReceptions_GatewayNodeNum] ON [dbo].[NodeReceptions] ([GatewayNodeNum], [LastHeardAt]);
GO
