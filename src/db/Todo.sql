CREATE TABLE dbo.Todo
(
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [Title] nvarchar(255) NOT NULL,
    [Description] nvarchar(255) NULL,
    [CreatedDate] datetime NOT NULL,
    [CompletedDate] datetime NULL
);