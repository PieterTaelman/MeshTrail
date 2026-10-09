-- Channel names each gateway uplinks (from its MQTT topics). Used to send team messages through the gateways that
-- carry the team's channel. Only names: channel keys are never stored.
CREATE TABLE [dbo].[GatewayChannels]
(
    [GatewayId]   UNIQUEIDENTIFIER  NOT NULL,
    [ChannelName] NVARCHAR(30)      NOT NULL,
    [FirstSeenAt] DATETIMEOFFSET(7) NOT NULL,
    [LastSeenAt]  DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_GatewayChannels] PRIMARY KEY CLUSTERED ([GatewayId], [ChannelName]),
    CONSTRAINT [FK_GatewayChannels_MeshGateways] FOREIGN KEY ([GatewayId]) REFERENCES [dbo].[MeshGateways] ([Id])
);
GO
