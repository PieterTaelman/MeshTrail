-- Reference table for the Samples module: the worked example every new table copies.
CREATE TABLE [dbo].[Samples]
(
    [Id]          UNIQUEIDENTIFIER  NOT NULL,
    [Name]        NVARCHAR(200)     NOT NULL,
    [Description] NVARCHAR(2000)    NULL,
    [CreatedAt]   DATETIMEOFFSET(7) NOT NULL,
    [CreatedBy]   NVARCHAR(256)     NOT NULL,
    [ModifiedAt]  DATETIMEOFFSET(7) NULL,
    [ModifiedBy]  NVARCHAR(256)     NULL,
    -- SQL Server bumps this on every update; EF uses it to detect concurrent edits (HTTP 409).
    [RowVersion]  ROWVERSION        NOT NULL,
    CONSTRAINT [PK_Samples] PRIMARY KEY NONCLUSTERED ([Id])
);
GO

-- Clustered on creation time so new rows append at the end instead of random GUID inserts.
CREATE CLUSTERED INDEX [IX_Samples_CreatedAt] ON [dbo].[Samples] ([CreatedAt]);
GO

CREATE NONCLUSTERED INDEX [IX_Samples_Name] ON [dbo].[Samples] ([Name]);
GO
