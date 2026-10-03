-- Idempotent seed data for local development. Runs on EVERY AppHost start, so only insert missing rows.
SET NOCOUNT ON;

MERGE [dbo].[Samples] AS target
USING (VALUES
    ('3F2504E0-4F89-11D3-9A0C-0305E82C3301', N'First sample',  N'Seeded so the grid is not empty on first run.'),
    ('3F2504E0-4F89-11D3-9A0C-0305E82C3302', N'Second sample', N'Edit me to try optimistic concurrency.'),
    ('3F2504E0-4F89-11D3-9A0C-0305E82C3303', N'Third sample',  NULL)
) AS source ([Id], [Name], [Description])
ON target.[Id] = source.[Id]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [Name], [Description], [CreatedAt], [CreatedBy])
    VALUES (source.[Id], source.[Name], source.[Description], SYSDATETIMEOFFSET(), N'seed');
GO
