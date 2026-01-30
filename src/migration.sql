IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [Email] nvarchar(255) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE TABLE [Notifications] (
    [Id] int NOT NULL IDENTITY,
    [Message] nvarchar(500) NOT NULL,
    [Type] int NOT NULL,
    [IsRead] bit NOT NULL,
    [RelatedTaskId] int NULL,
    [UserId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Tasks] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(2000) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Priority] int NOT NULL,
    [DueDate] datetime2 NOT NULL,
    [CreatedByUserId] int NOT NULL,
    [AssignedToUserId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Tasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Tasks_Users_AssignedToUserId] FOREIGN KEY ([AssignedToUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Tasks_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Comments] (
    [Id] int NOT NULL IDENTITY,
    [Content] nvarchar(1000) NOT NULL,
    [TaskId] int NOT NULL,
    [UserId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Comments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Comments_Tasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [Tasks] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Comments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Comments_TaskId] ON [Comments] ([TaskId]);

CREATE INDEX [IX_Comments_UserId] ON [Comments] ([UserId]);

CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);

CREATE INDEX [IX_Tasks_AssignedToUserId] ON [Tasks] ([AssignedToUserId]);

CREATE INDEX [IX_Tasks_CreatedByUserId] ON [Tasks] ([CreatedByUserId]);

CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260125203723_InitialCreate', N'10.0.2');

COMMIT;
GO

