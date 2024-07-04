module Todo.Api.Commands

open Db
open Domain
open Environment
open Giraffe
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http
open Sql
open System
open System.Threading.Tasks
open Validus

let private loadTodo (env: IQueryTodo) (todoId: TodoId) : ServiceResultAsync<Todo> = taskResult {
    let! result = env.GetTodoById todoId

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

    let write (connectionString: string) (Data cmd: CreateTodoCmd) = task {
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
        | PrimaryKeyConstraint "dbo.Todo" -> return Error(DomainError "A Todo with this ID already exists.")
    }

    let execute (env: ICommandTodo) (request: CreateTodoRequest) : ServiceResultAsync<_> = taskResult {
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

        let cmd = Todo.create title description todoId
        return! env.CreateTodo cmd
    }

    let handler env next (ctx: HttpContext) = task {
        let! request = ctx.BindJsonAsync<CreateTodoRequest>()
        let! result = execute env request
        return! Result.toHttpHandler (result, json) next ctx
    }

module CompleteTodo =
    let write (connectionString: string) (Data cmd: CompleteTodoCmd) = taskResult {
        let! rowsModified =
            Scripts.Commands.CompleteTodo
                .WithConnection(connectionString)
                .WithParameters(cmd.Date, cmd.Id)
                .ExecuteAsync()

        return! rowsModified |> ofRowsModified $"Todo {cmd.Id} not found"
    }

    let execute (env: #ICommandTodo & #IQueryTodo) (todoId: string) = taskResult {
        let! todoId = todoId |> TodoId.TryCreate |> Result.mapError InvalidRequest
        let! todo = loadTodo env todoId
        let! cmd = todo |> Todo.complete |> Result.mapError DomainError
        return! env.CompleteTodo cmd
    }

    let handler env todoId next (ctx: HttpContext) = task {
        let! result = execute env todoId
        return! Result.toHttpHandler result next ctx
    }

module DeleteTodo =
    let write (connectionString: string) (Data cmd: DeleteTodoCmd) = taskResult {
        // Using Facil's built-in CRUD script to delete a Todo.
        let! rowsModified =
            Scripts.Todo_Delete
                .WithConnection(connectionString)
                .WithParameters(cmd)
                .ExecuteAsync()

        return! rowsModified |> ofRowsModified $"Todo {cmd.Id} not found"
    }

    let execute (env: #IQueryTodo & #ICommandTodo) (todoId: string) = taskResult {
        let! todoId = todoId |> TodoId.TryCreate |> Result.mapError InvalidRequest
        let! todo = loadTodo env todoId
        let! cmd = todo |> Todo.delete |> Result.mapError DomainError
        return! env.DeleteTodo cmd
    }

    let handler env todoId next (ctx: HttpContext) = task {
        let! result = execute env todoId
        return! Result.toHttpHandler result next ctx
    }

module EditTodo =
    let write (connectionString: string) (Data cmd: EditTodoCmd) = taskResult {
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

    let execute (env: #IQueryTodo & #ICommandTodo) (todoId: string) title description : ServiceResultAsync<_> = taskResult {
        let! todoId, title, description =
            validate {
                let! todoId = TodoId.TryCreate todoId
                let! title = title |> String255.TryCreate "Title"

                and! description =
                    description
                    |> Option.ofObj
                    |> Option.toResultOption (String255.TryCreate "Description")

                return todoId, title, description
            }
            |> Result.mapError InvalidRequest

        let! todo = loadTodo env todoId
        let! cmd = todo |> Todo.edit title description |> Result.mapError DomainError
        return! env.EditTodo cmd
    }

    let handler env todoId next (ctx: HttpContext) = task {
        let! request = ctx.BindJsonAsync<{| Title: string; Description: string |}>()
        let! result = execute env todoId request.Title request.Description
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