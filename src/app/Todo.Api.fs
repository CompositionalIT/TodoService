module Todo.Api

open Giraffe
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Http
open Saturn

type HttpContext with

    member this.TodoDbConnectionString =
        match this.GetService<IConfiguration>().GetConnectionString "TodoDb" with
        | null -> failwith "Missing connection string"
        | v -> v

let createTodo next (ctx: HttpContext) = task {
    let! request = ctx.BindJsonAsync<Service.CreateTodoRequest>()
    let! result = Service.createTodo ctx.TodoDbConnectionString request
    return! Result.toHttpHandler result next ctx
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

// Create a function to edit the todo's title and description
let editTodo todoId next (ctx: HttpContext) = task {
    let! request = ctx.BindJsonAsync<Service.RawTodo>()

    let! result =
        Service.editTodo
            ctx.TodoDbConnectionString
            {
                Title = request.Title
                Description = request.Description
                Id = todoId
            }

    return! Result.toHttpHandler result next ctx
}

let getTodoStats next (ctx: HttpContext) = task {
    let! stats = Service.getTodoStats ctx.TodoDbConnectionString
    return! json stats next ctx
}

/// Giraffe version of router
let giraffeRouter: HttpHandler =
    choose [
        GET >=> route "/todo/" >=> getAllTodos
        GET >=> route "/todo/stats" >=> getTodoStats

        POST >=> route "/todo/" >=> createTodo

        GET >=> routef "/todo/%s" getTodo
        PUT >=> routef "/todo/%s" editTodo
        PUT >=> routef "/todo/%s/complete" completeTodo
    ]

/// Saturn's version of router
let saturnRouter = router {
    get "/todo/" getAllTodos
    getf "/todo/%s" getTodo
    putf "/todo/%s" editTodo
    post "/todo/" createTodo

    get "/todo/stats" getTodoStats
    putf "/todo/%s/complete" completeTodo
}
