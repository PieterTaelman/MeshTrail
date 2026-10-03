-- Every Meshtastic node the gateway has ever heard of ("discovered"), with its latest state.
CREATE TABLE [dbo].[MeshNodes]
(
    -- uint32 node number from the radio; BIGINT because it does not fit in INT.
    [NodeNum]           BIGINT            NOT NULL,
    -- Display form of NodeNum: "!" + 8 hex digits, e.g. !f115aaec.
    [NodeId]            NVARCHAR(9)       NOT NULL,
    [LongName]          NVARCHAR(40)      NOT NULL,
    [ShortName]         NVARCHAR(10)      NOT NULL,
    [HardwareModel]     NVARCHAR(50)      NULL,
    [Role]              NVARCHAR(30)      NULL,
    -- 32-byte Curve25519 public key (public, safe to store); used for direct-message encryption.
    [PublicKey]         VARBINARY(32)     NULL,
    -- Where the node came from; "mesh" today, room for other sources later.
    [Source]            NVARCHAR(20)      NOT NULL,
    [FirstSeenAt]       DATETIMEOFFSET(7) NOT NULL,
    [LastHeardAt]       DATETIMEOFFSET(7) NULL,
    [Snr]               FLOAT             NULL,
    [Rssi]              INT               NULL,
    [HopsAway]          INT               NULL,
    -- 0-100 %, or 101 = running on external power.
    [BatteryLevel]      INT               NULL,
    [Voltage]           FLOAT             NULL,
    [Latitude]          FLOAT             NULL,
    [Longitude]         FLOAT             NULL,
    [Altitude]          INT               NULL,
    [PositionTime]      DATETIMEOFFSET(7) NULL,
    [PositionPrecision] INT               NULL,
    [UpdatedAt]         DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_MeshNodes] PRIMARY KEY CLUSTERED ([NodeNum])
);
GO

-- "Who was heard recently" (online filter, node list order).
CREATE NONCLUSTERED INDEX [IX_MeshNodes_LastHeardAt] ON [dbo].[MeshNodes] ([LastHeardAt]);
GO
