CREATE TABLE dbo.Todo
(
    Id int NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Title nvarchar(255) NOT NULL,
    Description nvarchar(255) NOT NULL,
    CreatedDate datetime NOT NULL,
    CompletedDate datetime NULL
);