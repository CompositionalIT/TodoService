module Todo.Api.Queries

open Db
open Domain
open Environment
open FsToolkit.ErrorHandling
open Giraffe
open Microsoft.AspNetCore.Http

module GetTodo =
    let read (connectionString: string) (todoId: TodoId) = task {
        // Using Facil's built-in CRUD script to get a Todo by ID.
        let! todoDto =
            Scripts.Todo_ById
                .WithConnection(connectionString)
                .WithParameters(todoId.Value)
                .AsyncExecuteSingle()

        return todoDto |> Result.requireSome (DataNotFound $"Unknown Todo {todoId.Value}")
    }

    let execute (query: IQueryTodo) (todoId: string) = taskResult {
        let! todoId = todoId |> TodoId.TryCreate |> Result.mapError InvalidRequest
        return! query.GetTodoById todoId
    }

    let handler env (todoId: string) next (ctx: HttpContext) : HttpFuncResult = task {
        let! result = execute env todoId
        return! Result.toHttpHandler (result, json) next ctx
    }

let getAllTodos next (ctx: HttpContext) = task {
    let! results =
        Scripts.Queries.GetAllTodos
            .WithConnection(ctx.TodoDbConnectionString)
            .ExecuteAsync()

    return! json results next ctx
}

module GetTodoStats =
    let read (connectionString: string) =
        Scripts.Queries.GetTodoStats.WithConnection(connectionString).ExecuteAsync()

    let execute (env: IQueryTodo) = task {
        let! stats = env.GetStats()

        let getStat status =
            stats
            |> Seq.tryPick (fun row -> if row.CompletionState = status then row.TodoItems else None)
            |> Option.defaultValue 0

        return {|
            Completed = getStat "Complete"
            Incomplete = getStat "Incomplete"
        |}
    }

    let handler env next (ctx: HttpContext) = task {
        let! results = execute env
        return! json results next ctx
    }