BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Courses_ProjectId' AND object_id = OBJECT_ID(N'[Courses]'))
        DROP INDEX [IX_Courses_ProjectId] ON [Courses];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [Projects] ADD [PublicDescription] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [Projects] ADD [RegisterAccentColor] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [Projects] ADD [RegisterDisplayOrder] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [Projects] ADD [RegisterIcon] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [Projects] ADD [ShowOnRegisterPage] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [AdminNotes] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [ApplicantType] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [City] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [ContactedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [ContactedByUserId] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [Gender] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [LastUpdatedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [Notes] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [ParentName] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [ParentPhone] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [SchoolStage] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [SourceIp] nvarchar(45) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [FrontendLeads] ADD [Status] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [Courses] ADD [PublicDescription] nvarchar(300) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [Courses] ADD [RegisterDisplayOrder] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    ALTER TABLE [Courses] ADD [ShowOnRegisterPage] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    CREATE TABLE [FrontendLeadCourses] (
        [Id] int NOT NULL IDENTITY,
        [FrontendLeadId] int NOT NULL,
        [CourseId] int NULL,
        [ProjectId] int NULL,
        [CourseNameSnapshot] nvarchar(200) NOT NULL,
        [ProjectNameSnapshot] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_FrontendLeadCourses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FrontendLeadCourses_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_FrontendLeadCourses_FrontendLeads_FrontendLeadId] FOREIGN KEY ([FrontendLeadId]) REFERENCES [FrontendLeads] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    CREATE INDEX [IX_Projects_ShowOnRegisterPage_IsActive] ON [Projects] ([ShowOnRegisterPage], [IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    CREATE INDEX [IX_FrontendLeads_Status_CreatedAt] ON [FrontendLeads] ([Status], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    CREATE INDEX [IX_Courses_ProjectId_ShowOnRegisterPage_IsActive] ON [Courses] ([ProjectId], [ShowOnRegisterPage], [IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    CREATE INDEX [IX_FrontendLeadCourses_CourseId] ON [FrontendLeadCourses] ([CourseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    CREATE INDEX [IX_FrontendLeadCourses_FrontendLeadId] ON [FrontendLeadCourses] ([FrontendLeadId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN

    UPDATE FrontendLeads
    SET Status = CASE WHEN IsContacted = 1 THEN 2 ELSE 1 END
    WHERE Status = 0 OR Status IS NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925064606_RL_RegisterCatalogAndLeadCourses'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260925064606_RL_RegisterCatalogAndLeadCourses', N'8.0.17');
END;
GO

COMMIT;
GO

