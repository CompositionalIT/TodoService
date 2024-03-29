module Todo.Api

open Giraffe
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Http
open Saturn

type HttpContext with

    /// The SQL connection string to the Todo database.
    member this.TodoDbConnectionString =
        match this.GetService<IConfiguration>().GetConnectionString "TodoDb" with
        | null -> failwith "Missing connection string"
        | v -> v

let createTodo next (ctx: HttpContext) = task {
    let! request = ctx.BindJsonAsync<Service.CreateTodoRequest>()
    let! result = Service.createTodo ctx.TodoDbConnectionString request
    return! Result.toHttpHandler (result, _.Value >> json) next ctx
}

let getTodo (todoId: string) next (ctx: HttpContext) = task {
    let! result = Service.getTodoById ctx.TodoDbConnectionString todoId
    return! Result.toHttpHandler (result, json) next ctx
}

let getAllTodos next (ctx: HttpContext) = task {
    let! results = Service.getAllTodos ctx.TodoDbConnectionString
    return! json results next ctx
}

let completeTodo (todoId: string) next (ctx: HttpContext) = task {
    let! result = Service.completeTodo ctx.TodoDbConnectionString todoId
    return! Result.toHttpHandler result next ctx
}

let editTodo todoId next (ctx: HttpContext) = task {
    let! request = ctx.BindJsonAsync<Service.RawTodo>()

    let! result =
        Service.editTodo ctx.TodoDbConnectionString {
            Title = request.Title
            Description = request.Description
            Id = todoId
        }

    return! Result.toHttpHandler result next ctx
}

let deleteTodo todoId next (ctx: HttpContext) = task {
    let! result = Service.deleteTodo ctx.TodoDbConnectionString todoId
    return! Result.toHttpHandler result next ctx
}

let clearAllTodos next (ctx: HttpContext) = task {
    do! Service.clearAllTodos ctx.TodoDbConnectionString
    return! Successful.OK "All todos deleted" next ctx
}

let getTodoStats next (ctx: HttpContext) = task {
    let! stats = Service.getTodoStats ctx.TodoDbConnectionString
    return! json stats next ctx
}

/// Giraffe's version of router
let giraffeRouter: HttpHandler =
    subRoute
        "/todo"
        (choose [
            GET >=> route "/" >=> getAllTodos
            GET >=> route "/stats" >=> getTodoStats

            POST >=> route "/" >=> createTodo
            DELETE >=> route "/" >=> clearAllTodos

            GET >=> routef "/%s" getTodo
            PUT >=> routef "/%s" editTodo
            PUT >=> routef "/%s/complete" completeTodo
            DELETE >=> routef "/%s" deleteTodo
        ])

/// Saturn's version of router
let saturnRouter: HttpHandler =
    subRoute
        "/todo"
        (router {
            get "/" getAllTodos
            get "/stats" getTodoStats

            post "/" createTodo
            post "/clear" clearAllTodos

            getf "/%s" getTodo
            putf "/%s" editTodo
            putf "/%s/complete" completeTodo
            deletef "/%s" deleteTodo
        })