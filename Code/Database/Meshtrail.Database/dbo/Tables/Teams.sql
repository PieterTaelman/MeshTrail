-- Groups of people who chat on their own Meshtastic channel. Only the channel name is stored, never its key.
CREATE TABLE [dbo].[Teams]
(
    [Id]          UNIQUEIDENTIFIER  NOT NULL,
    [Name]        NVARCHAR(50)      NOT NULL,
    -- The Meshtastic channel name (max 11 characters in the firmware), as gateways report it in their MQTT topics.
    [ChannelName] NVARCHAR(11)      NOT NULL,
    -- Shared with people who may join; the owner can renew it.
    [JoinCode]    NVARCHAR(8)       NOT NULL,
    [CreatedAt]   DATETIMEOFFSET(7) NOT NULL,
    [CreatedBy]   NVARCHAR(256)     NOT NULL,
    CONSTRAINT [PK_Teams] PRIMARY KEY NONCLUSTERED ([Id])
);
GO

CREATE CLUSTERED INDEX [IX_Teams_CreatedAt] ON [dbo].[Teams] ([CreatedAt]);
GO

-- One team per channel name, or messages of two teams would mix.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Teams_ChannelName] ON [dbo].[Teams] ([ChannelName]);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_Teams_JoinCode] ON [dbo].[Teams] ([JoinCode]);
GO
