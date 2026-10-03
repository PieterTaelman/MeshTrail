-- Connection state of each Meshtastic gateway node. One row today ("primary"); the key leaves room for more gateways.
CREATE TABLE [dbo].[MeshGateways]
(
    [GatewayKey]      NVARCHAR(50)      NOT NULL,
    [Mode]            NVARCHAR(20)      NOT NULL,
    [Status]          NVARCHAR(20)      NOT NULL,
    [StatusChangedAt] DATETIMEOFFSET(7) NOT NULL,
    [LastConnectedAt] DATETIMEOFFSET(7) NULL,
    [LastError]       NVARCHAR(500)     NULL,
    -- Meshtastic node numbers are uint32, which does not fit in INT, so BIGINT.
    [NodeNum]         BIGINT            NULL,
    [FirmwareVersion] NVARCHAR(50)      NULL,
    CONSTRAINT [PK_MeshGateways] PRIMARY KEY CLUSTERED ([GatewayKey])
);
GO
