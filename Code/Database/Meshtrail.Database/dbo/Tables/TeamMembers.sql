-- Who is in which team. Owner can renew the join code.
CREATE TABLE [dbo].[TeamMembers]
(
    [TeamId]   UNIQUEIDENTIFIER  NOT NULL,
    [UserId]   NVARCHAR(256)     NOT NULL,
    [UserName] NVARCHAR(256)     NOT NULL,
    -- Owner | Member
    [Role]     NVARCHAR(20)      NOT NULL,
    [JoinedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_TeamMembers] PRIMARY KEY CLUSTERED ([TeamId], [UserId]),
    CONSTRAINT [FK_TeamMembers_Teams] FOREIGN KEY ([TeamId]) REFERENCES [dbo].[Teams] ([Id]) ON DELETE CASCADE
);
GO

-- "My teams".
CREATE NONCLUSTERED INDEX [IX_TeamMembers_UserId] ON [dbo].[TeamMembers] ([UserId]);
GO
