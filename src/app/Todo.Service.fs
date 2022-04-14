module Todo.Service

open Dapper
open Domain
open Microsoft.Data.SqlClient
open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling

/// Represents an error occurred while executing a service call.
type ServiceError<'T> =
    | NotFound of string
    | ValidationError of 'T
    | GenericError of 'T

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

type EditTodoRequest =
    {
        Id : Guid
        Title : string
        Description : string
    }

let createTodo (connectionString:string) (request:CreateTodoRequest) = task {
    match Todo.TryCreate (request.Title, request.Description) with
    | Ok todo ->
        do! TodoDb.Scripts
                .Todo_Insert
                .WithConnection(connectionString)
                .WithParameters(
                    todo.Id.Value,
                    todo.Title.Value,
                    todo.Description |> Option.map (fun r -> r.Value),
                    todo.CreatedDate,
                    todo.CompletedDate)
                .ExecuteAsync()
                :> Task
        return Ok ()
    | Error err ->
        return Error (ValidationError err)
}

let getTodoById (connectionString:string) (todoId:string) = task {
    match TodoId.TryParse todoId with
    | Ok todoId ->
        let! result =
            TodoDb.Scripts
                .Todo_ById
                .WithConnection(connectionString)
                .WithParameters(todoId.Value)
                .AsyncExecuteSingle()
        return
            match result with
            | None -> Error (NotFound $"Unknown Todo {todoId}")
            | Some result -> Ok result
    | Error message ->
        return Error (ValidationError message)
}

let getAllTodos (connectionString:string) =
    TodoDb.Scripts
            .Queries
            .GetAllItems
            .WithConnection(connectionString)
            .AsyncExecute()

// Create a function to complete a todo
let completeTodo (connectionString:string) (todoId:string) = task {
    match TodoId.TryParse todoId with
    | Ok todoId ->
        let! rowsModified =
            TodoDb.Scripts
                .Commands
                .CompleteTodo
                .WithConnection(connectionString)
                .WithParameters(DateTime.UtcNow, todoId.Value)
                .ExecuteAsync()
        return
            match rowsModified with
            | 0 -> Error (NotFound $"Unknown Todo {todoId}")
            | 1 -> Ok ()
            | _ -> Error (GenericError $"Too many rows modified ({rowsModified})")
    | Error message ->
        return Error (ValidationError message)
}

let editTodo (connectionString:string) (request:EditTodoRequest) = task {
    let todoDto = result {
        let! title = String255.TryCreate "Title" request.Title
        let! description = String255.TryCreate "Description" request.Description
        return
            {| request with
                Title = title.Value
                Description = description.Value
            |}
    }
    match todoDto with
    | Ok todoDto ->
        let! rowsModified =
            TodoDb.Scripts
                .Commands
                .EditTodo
                .WithConnection(connectionString)
                .WithParameters(todoDto)
                .ExecuteAsync()
        return
            match rowsModified with
            | 0 -> Error (NotFound $"Unknown Todo {request.Id}")
            | 1 -> Ok ()
            | _ -> Error (GenericError $"Too many rows modified ({rowsModified})")
    | Error error ->
        return Error (ValidationError error)
}