module Todo.Service

open Dapper
open Domain
open FsToolkit.ErrorHandling
open Microsoft.Data.SqlClient
open System
open System.Threading.Tasks

/// Represents an error occurred while executing a service call.
type ValidationError<'T> =
    | NotFound of string
    | ValidationErrors of {| Field : string; Error : string |} list
    | GenericError of 'T

module ValidationError =
    let create field message = {| Field = field; Error = message |}
    let createOne field message = create field message |> List.singleton |> ValidationErrors

module Result =
    let ofRowsModified onNone rowsModified =
        match rowsModified with
        | 0 -> Error (NotFound onNone)
        | 1 -> Ok ()
        | _ -> Error (GenericError $"Too many rows modified ({rowsModified})")

/// A sample implementation of a single DAL method using Dapper.
module Dapper =
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

let createTodo (connectionString:string) (request:CreateTodoRequest) = taskResult {
    let! todo =
        Todo.TryCreate (request.Title, request.Description)
        |> Result.mapError ValidationErrors

    do! Db.Scripts
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
}

let getTodoById (connectionString:string) (todoId:string) = taskResult {
    let! todoId =
        TodoId.TryParse todoId
        |> Result.mapError (ValidationError.createOne "TodoId")

    let! result =
        Db.Scripts.Todo_ById
            .WithConnection(connectionString)
            .WithParameters(todoId.Value)
            .AsyncExecuteSingle()

    return!
        result
        |> Option.toResult (NotFound $"Unknown Todo {todoId.Value}")
}

let getAllTodos (connectionString:string) =
    Db.Scripts.Queries.GetAllItems
            .WithConnection(connectionString)
            .AsyncExecute()

// Create a function to complete a todo
let completeTodo (connectionString:string) (todoId:string) = taskResult {
    let! todoId =
        TodoId.TryParse todoId
        |> Result.mapError (ValidationError.createOne "TodoId")

    let! rowsModified =
        Db.Scripts.Commands.CompleteTodo
            .WithConnection(connectionString)
            .WithParameters(DateTime.UtcNow, todoId.Value)
            .ExecuteAsync()

    return!
        rowsModified
        |> Result.ofRowsModified $"Unknown Todo {todoId.Value}"
}

let editTodo (connectionString:string) (request:EditTodoRequest) = taskResult {
    let! todoDto =
        validation {
            let! title = String255.TryCreate "Title" request.Title
            and! description = String255.TryCreate "Description" request.Description
            and! todoId = TodoId.TryCreate request.Id |> Result.mapError (ValidationError.create "Id")
            return
                {|
                    Id = todoId.Value
                    Title = title.Value
                    Description = description.Value
                |}
        }
        |> Result.mapError ValidationErrors

    let! rowsModified =
        Db.Scripts.Commands.EditTodo
            .WithConnection(connectionString)
            .WithParameters(todoDto)
            .ExecuteAsync()

    return!
       rowsModified
       |> Result.ofRowsModified $"Unknown Todo {request.Id}"
}

let getTodoStats (connectionString:string) = task {
    let! stats =
        Db.Scripts.Queries.GetTodoStats
            .WithConnection(connectionString)
            .AsyncExecute()
    let getStat =
        let stats =
            stats
            |> Seq.map (fun r -> r.CompletionState, r.TodoItems)
            |> Map
        fun status ->
            stats
            |> Map.tryFind status
            |> Option.bind id
            |> Option.defaultValue 0

    return
        {|
            Completed = getStat "Complete"
            Incomplete = getStat "Incomplete"
        |}
}