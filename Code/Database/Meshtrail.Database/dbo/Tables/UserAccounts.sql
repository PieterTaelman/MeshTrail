-- People who registered with Meshtrail. Passwords and mail tokens are only stored as hashes.
CREATE TABLE [dbo].[UserAccounts]
(
    [Id]                    UNIQUEIDENTIFIER  NOT NULL,
    -- Lower-case, so the same address always finds the same account.
    [Email]                 NVARCHAR(256)     NOT NULL,
    [FirstName]             NVARCHAR(100)     NOT NULL,
    [LastName]              NVARCHAR(100)     NOT NULL,
    -- ASP.NET Core Identity password hash format (PBKDF2, versioned).
    [PasswordHash]          NVARCHAR(400)     NOT NULL,
    -- NULL until the user clicked the link in the confirmation mail.
    [EmailConfirmedAt]      DATETIMEOFFSET(7) NULL,
    -- SHA-256 of the tokens in the confirmation and password-reset links.
    [ConfirmationTokenHash] VARBINARY(32)     NULL,
    [ConfirmationExpiresAt] DATETIMEOFFSET(7) NULL,
    [ResetTokenHash]        VARBINARY(32)     NULL,
    [ResetExpiresAt]        DATETIMEOFFSET(7) NULL,
    [FailedSignIns]         INT               NOT NULL,
    [LockedUntil]           DATETIMEOFFSET(7) NULL,
    [CreatedAt]             DATETIMEOFFSET(7) NOT NULL,
    [RowVersion]            ROWVERSION        NOT NULL,
    CONSTRAINT [PK_UserAccounts] PRIMARY KEY NONCLUSTERED ([Id])
);
GO

CREATE CLUSTERED INDEX [IX_UserAccounts_CreatedAt] ON [dbo].[UserAccounts] ([CreatedAt]);
GO

-- One account per email address.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_UserAccounts_Email] ON [dbo].[UserAccounts] ([Email]);
GO
