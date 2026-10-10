-- Nodes that connect the mesh around them to Meshtrail. MQTT gateways are added by their owner (who gets a login);
-- TCP and simulated gateways come from the server configuration.
CREATE TABLE [dbo].[MeshGateways]
(
    [Id]              UNIQUEIDENTIFIER  NOT NULL,
    -- The gateway's own node number (uint32, so BIGINT), chosen when the gateway is added; its login only works
    -- for this node.
    [NodeNum]         BIGINT            NULL,
    -- Mqtt | Tcp | Simulated
    [Transport]       NVARCHAR(20)      NOT NULL,
    [OwnerUserId]     NVARCHAR(256)     NULL,
    [OwnerName]       NVARCHAR(256)     NULL,
    -- MQTT login (not secret) and SHA-256 of "login:password" (the password itself is never stored).
    [MqttUserName]    NVARCHAR(50)      NULL,
    [CredentialHash]  VARBINARY(32)     NULL,
    -- Broker (region) the gateway connects to, and the root topic it uses; downlinks go back the same way.
    [Broker]          NVARCHAR(50)      NULL,
    [MqttRoot]        NVARCHAR(100)     NULL,
    -- Channel name we send direct messages on (never a channel key).
    [DownlinkChannel] NVARCHAR(30)      NULL,
    -- Pending | Online | Offline | Revoked
    [Status]          NVARCHAR(20)      NOT NULL,
    [StatusChangedAt] DATETIMEOFFSET(7) NOT NULL,
    [LastUplinkAt]    DATETIMEOFFSET(7) NULL,
    [LastError]       NVARCHAR(500)     NULL,
    [FirmwareVersion] NVARCHAR(50)      NULL,
    [CreatedAt]       DATETIMEOFFSET(7) NOT NULL,
    [RevokedAt]       DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_MeshGateways] PRIMARY KEY NONCLUSTERED ([Id])
);
GO

CREATE CLUSTERED INDEX [IX_MeshGateways_CreatedAt] ON [dbo].[MeshGateways] ([CreatedAt]);
GO

-- Rule backed by the database: a node is at most one gateway (pending included). Remove it first to add it again.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_MeshGateways_NodeNum_Active]
    ON [dbo].[MeshGateways] ([NodeNum])
    WHERE [NodeNum] IS NOT NULL AND [Status] <> N'Revoked';
GO

-- Logins are never reused, not even after a revoke.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_MeshGateways_MqttUserName]
    ON [dbo].[MeshGateways] ([MqttUserName])
    WHERE [MqttUserName] IS NOT NULL;
GO

-- "My gateways".
CREATE NONCLUSTERED INDEX [IX_MeshGateways_OwnerUserId] ON [dbo].[MeshGateways] ([OwnerUserId]) WHERE [OwnerUserId] IS NOT NULL;
GO
