UPDATE dbo.Todo
SET Title = @Title,
    [Description] = @Description
WHERE Id = @Id