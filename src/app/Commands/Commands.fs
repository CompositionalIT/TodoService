module Todo.Api.Commands

open Db
open Domain
open Giraffe
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http
open Sql
open System
open System.Threading.Tasks
open Validus

let private loadTodo (connectionString: string) (todoId: string) : ServiceResultAsync<Todo> = taskResult {
    let! result = Queries.getTodoRaw connectionString todoId

    return!
        validate {
            let! todoId = TodoId.TryCreate(result.Id, "Id")
            and! title = result.Title |> String255.TryCreate "Title"
            and! description = result.Description |> Option.toResultOption (String255.TryCreate "Description")

            return {
                Id = todoId
                Title = title
                Description = description
                CreatedDate = result.CreatedDate
                CompletedDate = result.CompletedDate
            }
        }
        |> Result.mapError InvalidRequest
}

module CreateTodo =
    type CreateTodoRequest = {
        Id: Guid Nullable
        Title: string
        Description: string
    }

    let writeToDb (connectionString: string) (Data cmd: CreateTodoCmd) = task {
        try
            do!
                Scripts.Todo_Insert
                    .WithConnection(connectionString)
                    .WithParameters(cmd)
                    .ExecuteAsync()
                :> Task

            return Ok cmd.Id
        with
        | UniqueConstraint("UC_Todo_Title", "dbo.Todo") ->
            return Error(DomainError "A Todo with this title already exists.")
        | PrimaryKeyConstraint "dbo.Todo" -> return Error(DomainError $"A Todo with this ID already exists.")
    }

    let execute (request: CreateTodoRequest) : ServiceResult<_> = result {
        let! title, todoId, description =
            validate {
                let! title = request.Title |> String255.TryCreate "Title"
                and! todoId = request.Id |> Option.ofNullable |> Option.toResultOption TodoId.TryCreate

                and! description =
                    request.Description
                    |> Option.ofObj
                    |> Option.toResultOption (String255.TryCreate "Description")

                return title, todoId, description
            }
            |> Result.mapError InvalidRequest

        return Todo.create title description todoId
    }

    let handler next (ctx: HttpContext) = task {
        let! result = taskResult {
            let! request = ctx.BindJsonAsync<CreateTodoRequest>()
            let! cmd = execute request
            return! cmd |> writeToDb ctx.TodoDbConnectionString
        }

        return! Result.toHttpHandler (result, json) next ctx
    }

module CompleteTodo =
    let writeToDb (connectionString: string) (Data cmd: CompleteTodoCmd) = taskResult {
        let! rowsModified =
            Scripts.Commands.CompleteTodo
                .WithConnection(connectionString)
                .WithParameters(cmd.Date, cmd.Id)
                .ExecuteAsync()

        return! rowsModified |> ofRowsModified $"Todo {cmd.Id} not found"
    }

    let handler (todoId: string) next (ctx: HttpContext) = task {
        let! result = taskResult {
            let! todo = loadTodo ctx.TodoDbConnectionString todoId
            let! cmd = todo |> Todo.complete |> Result.mapError DomainError
            return! cmd |> writeToDb ctx.TodoDbConnectionString
        }

        return! Result.toHttpHandler result next ctx
    }

module DeleteTodo =
    let writeToDb (connectionString: string) (Data cmd: DeleteTodoCmd) = taskResult {
        // Using Facil's built-in CRUD script to delete a Todo.
        let! rowsModified =
            Scripts.Todo_Delete
                .WithConnection(connectionString)
                .WithParameters(cmd)
                .ExecuteAsync()

        return! rowsModified |> ofRowsModified $"Todo {cmd.Id} not found"
    }

    let handler todoId next (ctx: HttpContext) = task {
        let! result = taskResult {
            let! todo = loadTodo ctx.TodoDbConnectionString todoId
            let! cmd = todo |> Todo.delete |> Result.mapError DomainError
            return! cmd |> writeToDb ctx.TodoDbConnectionString
        }

        return! Result.toHttpHandler result next ctx
    }

module EditTodo =
    type EditTodoRequest = {
        Todo: Todo
        Title: string
        Description: string
    }

    let writeToDb (connectionString: string) (Data cmd: EditTodoCmd) = taskResult {
        try
            let! rowsModified =
                Scripts.Commands.EditTodo
                    .WithConnection(connectionString)
                    .WithParameters(cmd)
                    .ExecuteAsync()

            do! rowsModified |> ofRowsModified $"Unknown Todo {cmd.Id}"
        with UniqueConstraint("dbo.Todo", "UC_Todo_Title") ->
            return! Error(DomainError "A Todo with this title already exists.")
    }

    let execute request : ServiceResult<_> = result {
        let! title, description =
            validate {
                let! title = request.Title |> String255.TryCreate "Title"

                and! description =
                    request.Description
                    |> Option.ofObj
                    |> Option.toResultOption (String255.TryCreate "Description")

                return title, description
            }
            |> Result.mapError InvalidRequest

        return! request.Todo |> Todo.edit title description |> Result.mapError DomainError
    }

    let handler todoId next (ctx: HttpContext) = task {
        let! request = ctx.BindJsonAsync<{| Title: string; Description: string |}>()

        let! result = taskResult {
            let! todo = loadTodo ctx.TodoDbConnectionString todoId

            let! cmd =
                execute {
                    Todo = todo
                    Title = request.Title
                    Description = request.Description
                }

            return! cmd |> writeToDb ctx.TodoDbConnectionString
        }

        return! Result.toHttpHandler result next ctx
    }

let clearAllTodos next (ctx: HttpContext) = task {
    do!
        Scripts.Commands.ClearAllTodos
            .WithConnection(ctx.TodoDbConnectionString)
            .ExecuteAsync()
        |> Task.map ignore

    return! Successful.OK "All todos deleted" next ctx
}