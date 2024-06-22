module Dal

open Db.Scripts
open Domain
open FsToolkit.ErrorHandling
open Sql
open System.Threading.Tasks

/// Unwraps the Value member of some data.
let inline (|Data|) x = (^a: (member Value: _) x)

let createTodo (connectionString: string) (Data cmd: CreateTodoCmd) = task {
    try
        do!
            Todo_Insert
                .WithConnection(connectionString)
                .WithParameters(
                    cmd.CreatedId.Value,
                    cmd.Title.Value,
                    cmd.Description |> Option.map _.Value,
                    cmd.CreatedDate,
                    None
                )
                .ExecuteAsync()
            :> Task

        return Ok cmd.CreatedId
    with
    | UniqueConstraint("UC_Todo_Title", "dbo.Todo") ->
        return Error(DomainError "A Todo with this title already exists.")
    | PrimaryKeyConstraint "dbo.Todo" -> return Error(DomainError $"A Todo with this ID already exists.")
}

let completeTodo (connectionString: string) (Data cmd: CompleteTodoCmd) = taskResult {
    let! rowsModified =
        DbCommands.CompleteTodo
            .WithConnection(connectionString)
            .WithParameters(cmd.Date, cmd.CompletedId.Value)
            .ExecuteAsync()

    return! rowsModified |> ofRowsModified $"Todo {cmd.CompletedId.Value} not found"
}

let deleteTodo (connectionString: string) (Data cmd: DeleteTodoCmd) = taskResult {
    // Using Facil's built-in CRUD script to delete a Todo.
    let! rowsModified =
        Todo_Delete
            .WithConnection(connectionString)
            .WithParameters(cmd.DeletedId.Value)
            .ExecuteAsync()

    return! rowsModified |> ofRowsModified $"Todo {cmd.DeletedId.Value} not found"
}

let updateTodo (connectionString: string) (Data cmd: EditTodoCmd) = taskResult {
    try
        let! rowsModified =
            DbCommands.EditTodo
                .WithConnection(connectionString)
                .WithParameters(
                    Id = cmd.EditedId.Value,
                    Title = cmd.Title.Value,
                    Description = (cmd.Description |> Option.map _.Value |> Option.toObj)
                )
                .ExecuteAsync()

        do! rowsModified |> ofRowsModified $"Unknown Todo {cmd.EditedId.Value}"
    with UniqueConstraint("dbo.Todo", "UC_Todo_Title") ->
        return! Error(DomainError "A Todo with this title already exists.")
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