module Todo.Service

open Dapper
open Db.Scripts
open Domain
open FsToolkit.ErrorHandling
open Microsoft.Data.SqlClient
open System
open System.Threading.Tasks
open Validus
open Validus.Operators

/// A sample implementation of a single DAL method using Dapper.
module Dapper =
    let saveUsingDapper connectionString todo =
        let conn = new SqlConnection(connectionString)

        let parameters = {|
            Id = todo.Id.Value
            Title = todo.Title.Value
            Description = todo.Description |> Option.map (fun r -> r.Value) |> Option.toObj
            CreatedDate = todo.CreatedDate
            CompletedDate = todo.CompletedDate |> Option.toNullable
        |}

        conn.ExecuteAsync(
            "INSERT INTO dbo.Todo (Id, Title, Description, CreatedDate, CompletedDate)
        VALUES (@Id, @Title, @Description, @CreatedDate, @CompletedDate)",
            parameters
        )

type CreateTodoRequest = { Title: string; Description: string }

type RawTodo = { Title: string; Description: string }

type EditTodoRequest = {
    Id: string
    Title: string
    Description: string
}

let createTodo (connectionString: string) (request: CreateTodoRequest) : Task<ServiceResult> = taskResult {
    let! (todo: Todo) =
        Todo.TryCreate(request.Title, request.Description)
        |> Result.mapError InvalidRequest

    do!
        Todo_Insert
            .WithConnection(connectionString)
            .WithParameters(
                todo.Id.Value,
                todo.Title.Value,
                todo.Description |> Option.map (fun r -> r.Value),
                todo.CreatedDate,
                todo.CompletedDate
            )
            .ExecuteAsync()
        :> Task
}

let getTodoById (connectionString: string) (todoId: string) : Task<ServiceResult<_>> = taskResult {
    let! todoId = TodoId.TryCreate("todoId", todoId) |> Result.mapError InvalidRequest

    let! result =
        Todo_ById
            .WithConnection(connectionString)
            .WithParameters(todoId.Value)
            .AsyncExecuteSingle()

    return! result |> Option.toResult (DataNotFound $"Unknown Todo {todoId.Value}")
}

let getAllTodos (connectionString: string) =
    DbQueries.GetAllItems.WithConnection(connectionString).ExecuteAsync()

let completeTodo (connectionString: string) (todoId: string) : Task<ServiceResult> = taskResult {
    // Using a dedicated domain type and associated build member for validation.
    let! todoId = TodoId.TryCreate("todoId", todoId) |> Result.mapError InvalidRequest

    let! rowsModified =
        DbCommands
            .CompleteTodo
            .WithConnection(connectionString)
            .WithParameters(DateTime.UtcNow, todoId.Value)
            .ExecuteAsync()

    return! rowsModified |> Result.ofRowsModified $"Unknown Todo {todoId.Value}"
}

let editTodo (connectionString: string) request : Task<ServiceResult> = taskResult {
    // An example of doing "inline" validation.
    let! todoDto =
        validate {
            let! title = (Check.String.notEmpty >=> String255.TryCreate) "Title" request.Title
            and! description = String255.TryCreate "Description" request.Description
            and! todoId = TodoId.TryCreate("Id", request.Id)

            return {|
                Id = todoId.Value
                Title = title.Value
                Description = description.Value
            |}
        }
        |> Result.mapError InvalidRequest

    let! rowsModified =
        DbCommands
            .EditTodo
            .WithConnection(connectionString)
            .WithParameters(todoDto)
            .ExecuteAsync()

    return! rowsModified |> Result.ofRowsModified $"Unknown Todo {request.Id}"
}

let getTodoStats (connectionString: string) = task {
    let! stats = DbQueries.GetTodoStats.WithConnection(connectionString).ExecuteAsync()

    let getStat status =
        stats
        |> Seq.tryPick (fun row -> if row.CompletionState = status then row.TodoItems else None)
        |> Option.defaultValue 0

    return {|
        Completed = getStat "Complete"
        Incomplete = getStat "Incomplete"
    |}
}
