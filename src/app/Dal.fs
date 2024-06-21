module Dal

open Db.Scripts
open Domain
open FsToolkit.ErrorHandling

let createTodo (connectionString: string) todo =
    Todo_Insert
        .WithConnection(connectionString)
        .WithParameters(
            todo.Id.Value,
            todo.Title.Value,
            todo.Description |> Option.map _.Value,
            todo.CreatedDate,
            todo.CompletedDate
        )
        .ExecuteAsync()
    |> Task.map (fun _ -> todo.Id)

let completeTodo (connectionString: string) (data: CompletedTodo) = taskResult {
    let! rowsModified =
        DbCommands.CompleteTodo
            .WithConnection(connectionString)
            .WithParameters(data.Value.Date, data.Value.CompletedId.Value)
            .ExecuteAsync()

    return!
        rowsModified
        |> Result.ofRowsModified $"Todo {data.Value.CompletedId.Value} not found"
}

let deleteTodo (connectionString: string) (data: DeletedTodo) = taskResult {
    // Using Facil's built-in CRUD script to delete a Todo.
    let! rowsModified =
        Todo_Delete
            .WithConnection(connectionString)
            .WithParameters(data.Value.DeletedId.Value)
            .ExecuteAsync()

    return!
        rowsModified
        |> Result.ofRowsModified $"Todo {data.Value.DeletedId.Value} not found"
}

let updateTodo (connectionString: string) (data: EditedTodo) = taskResult {
    let! rowsModified =
        DbCommands.EditTodo
            .WithConnection(connectionString)
            .WithParameters(
                Id = data.Value.EditedId.Value,
                Title = data.Value.Title.Value,
                Description = (data.Value.Description |> Option.map _.Value |> Option.toObj)
            )
            .ExecuteAsync()

    return!
        rowsModified
        |> Result.ofRowsModified $"Unknown Todo {data.Value.EditedId.Value}"
}


/// A sample implementation of a single DAL method using Dapper.
module Dapper =
    open Dapper
    open Microsoft.Data.SqlClient

    let saveUsingDapper connectionString todo =
        let conn = new SqlConnection(connectionString)

        let parameters = {|
            Id = todo.Id.Value
            Title = todo.Title.Value
            Description = todo.Description |> Option.map _.Value |> Option.toObj
            CreatedDate = todo.CreatedDate
            CompletedDate = todo.CompletedDate |> Option.toNullable
        |}

        conn.ExecuteAsync(
            "INSERT INTO dbo.Todo (Id, Title, Description, CreatedDate, CompletedDate)
        VALUES (@Id, @Title, @Description, @CreatedDate, @CompletedDate)",
            parameters
        )