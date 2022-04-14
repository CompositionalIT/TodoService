module TodoService

open Dapper
open Domain
open Microsoft.Data.SqlClient
open System

module Result =
    let ofTry v =
        match v with
        | true, v -> Ok v
        | false, _ -> Error $"Parse failed: {v}"

let saveUsingDapper connectionString todo =
    let conn = new SqlConnection(connectionString)
    let parameters =
        {|
            Id = todo.Id.Value
            Title = todo.Title.Value
            Description = todo.Description |> Option.map (fun r -> r.Value) |> Option.toObj
            CreatedDate = todo.CreatedDate
            CompletedDate = todo.CompletedDate |> Option.toNullable
        |}
    conn.ExecuteAsync("INSERT INTO dbo.Todo (Id, Title, Description, CreatedDate, CompletedDate)
    VALUES (@Id, @Title, @Description, @CreatedDate, @CompletedDate)", parameters)

type CreateTodoRequest =
    {
        Title : string
        Description : string
    }

let createTodo (connectionString:string) (request:CreateTodoRequest) = task {
    match Todo.TryCreate (request.Title, request.Description) with
    | Ok todo ->
        let! rowsModified =
            TodoDb.Scripts
                .Todo_Insert
                .WithConnection(connectionString)
                .WithParameters(
                    todo.Id.Value,
                    todo.Title.Value,
                    todo.Description |> Option.map (fun r -> r.Value),
                    todo.CreatedDate,
                    todo.CompletedDate)
                .ExecuteAsync()
        return Ok rowsModified
    | Error err ->
        return Error err
}

let getTodoById (connectionString:string) (todoId:string) = task {
    match Guid.TryParse todoId |> Result.ofTry with
    | Ok value ->
        let! result =
            TodoDb.Scripts
                    .Todo_ById
                    .WithConnection(connectionString)
                    .WithParameters(value)
                    .AsyncExecuteSingle()
        return Ok result
    | Error message ->
        return Error message
}

let getAllTodos (connectionString:string) =
    TodoDb.Scripts
            .Queries
            .GetAllItems
            .WithConnection(connectionString)
            .AsyncExecute()