BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015194759_RTK_AuditLog'
)
BEGIN
    CREATE TABLE [RemedialTrackAuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [TrackId] int NOT NULL,
        [ActionType] nvarchar(60) NOT NULL,
        [Description] nvarchar(600) NOT NULL,
        [ActorUserId] nvarchar(450) NOT NULL,
        [ActorName] nvarchar(200) NULL,
        [ActorProfileName] nvarchar(200) NULL,
        [ActorRoles] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RemedialTrackAuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RemedialTrackAuditLogs_RemedialTracks_TrackId] FOREIGN KEY ([TrackId]) REFERENCES [RemedialTracks] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015194759_RTK_AuditLog'
)
BEGIN
    CREATE INDEX [IX_RemedialTrackAuditLogs_Track_CreatedAt] ON [RemedialTrackAuditLogs] ([TrackId], [CreatedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015194759_RTK_AuditLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015194759_RTK_AuditLog', N'8.0.17');
END;
GO

COMMIT;
GO

