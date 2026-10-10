-- Links a mesh node to the user who owns it. Claimed until the user enters the code we sent to the node.
CREATE TABLE [dbo].[NodeRegistrations]
(
    [Id]                  UNIQUEIDENTIFIER  NOT NULL,
    [NodeNum]             BIGINT            NOT NULL,
    [UserId]              NVARCHAR(256)     NOT NULL,
    [UserName]            NVARCHAR(256)     NOT NULL,
    -- Claimed | Verified | Revoked
    [Status]              NVARCHAR(20)      NOT NULL,
    -- Public key from the contact link (32 bytes); given to TCP gateways once verified. NULL for a node registered
    -- through its gateway login before we heard its key.
    [PublicKey]           VARBINARY(32)     NULL,
    [LongName]            NVARCHAR(40)      NOT NULL,
    [ShortName]           NVARCHAR(10)      NOT NULL,
    -- SHA-256 of the 6-digit code (never the code itself).
    [CodeHash]            VARBINARY(32)     NULL,
    [CodeExpiresAt]       DATETIMEOFFSET(7) NULL,
    [FailedAttempts]      INT               NOT NULL,
    [VerificationMessageId] UNIQUEIDENTIFIER NULL,
    [ClaimedAt]           DATETIMEOFFSET(7) NOT NULL,
    [VerifiedAt]          DATETIMEOFFSET(7) NULL,
    [RevokedAt]           DATETIMEOFFSET(7) NULL,
    [RevokedReason]       NVARCHAR(200)     NULL,
    [RowVersion]          ROWVERSION        NOT NULL,
    CONSTRAINT [PK_NodeRegistrations] PRIMARY KEY NONCLUSTERED ([Id]),
    CONSTRAINT [FK_NodeRegistrations_MeshNodes] FOREIGN KEY ([NodeNum]) REFERENCES [dbo].[MeshNodes] ([NodeNum])
);
GO

CREATE CLUSTERED INDEX [IX_NodeRegistrations_ClaimedAt] ON [dbo].[NodeRegistrations] ([ClaimedAt]);
GO

-- Rule backed by the database: a node has at most one registration in progress or active.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_NodeRegistrations_NodeNum_Active]
    ON [dbo].[NodeRegistrations] ([NodeNum])
    WHERE [Status] IN (N'Claimed', N'Verified');
GO

CREATE NONCLUSTERED INDEX [IX_NodeRegistrations_UserId] ON [dbo].[NodeRegistrations] ([UserId]);
GO
