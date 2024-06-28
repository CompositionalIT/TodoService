module Todo.Api.Queries

open Db.Scripts
open Domain
open Giraffe
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http

let getTodoRaw (connectionString: string) (todoId: string) = taskResult {
    let! todoId = todoId |> TodoId.TryCreate |> Result.mapError InvalidRequest

    // Using Facil's built-in CRUD script to get a Todo by ID.
    let! todoDto =
        Todo_ById
            .WithConnection(connectionString)
            .WithParameters(todoId.Value)
            .AsyncExecuteSingle()

    return! todoDto |> Result.requireSome (DataNotFound $"Unknown Todo {todoId.Value}")
}

let getTodo (todoId: string) next (ctx: HttpContext) = task {
    let! result = getTodoRaw ctx.TodoDbConnectionString todoId
    return! Result.toHttpHandler (result, json) next ctx
}

let getAllTodos next (ctx: HttpContext) = task {
    let! results = Queries.GetAllTodos.WithConnection(ctx.TodoDbConnectionString).ExecuteAsync()
    return! json results next ctx
}

let getTodoStats next (ctx: HttpContext) = task {
    let! stats = Queries.GetTodoStats.WithConnection(ctx.TodoDbConnectionString).ExecuteAsync()

    let getStat status =
        stats
        |> Seq.tryPick (fun row -> if row.CompletionState = status then row.TodoItems else None)
        |> Option.defaultValue 0

    return!
        json
            {|
                Completed = getStat "Complete"
                Incomplete = getStat "Incomplete"
            |}
            next
            ctx
}