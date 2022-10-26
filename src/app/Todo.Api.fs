module Todo.Api

open Giraffe
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Http
open Saturn

type HttpContext with

    member this.TodoDbConnectionString =
        this.GetService<IConfiguration>().GetConnectionString "TodoDb"

let createTodo next (ctx: HttpContext) = task {
    let! request = ctx.BindJsonAsync<Service.CreateTodoRequest>()
    let! result = Service.createTodo ctx.TodoDbConnectionString request
    return! result |> Result.toHttpHandler next ctx (fun _ next ctx -> next ctx)
}

let getTodo (todoId: string) next (ctx: HttpContext) = task {
    let! result = Service.getTodoById ctx.TodoDbConnectionString todoId
    return! result |> Result.toHttpHandler next ctx json
}

let getAllTodos next (ctx: HttpContext) = task {
    let! results = Service.getAllTodos ctx.TodoDbConnectionString
    return! json results next ctx
}

let completeTodo (todoId: string) next (ctx: HttpContext) = task {
    let! result = Service.completeTodo ctx.TodoDbConnectionString todoId
    return! result |> Result.toHttpHandler next ctx (fun _ next ctx -> next ctx)
}

// Create a function to edit the todo's title and description
let editTodo next (ctx: HttpContext) = task {
    let! request = ctx.BindJsonAsync<Service.EditTodoRequest>()
    let! result = Service.editTodo ctx.TodoDbConnectionString request
    return! result |> Result.toHttpHandler next ctx (fun _ next ctx -> next ctx)
}

let getTodoStats next (ctx: HttpContext) = task {
    let! stats = Service.getTodoStats ctx.TodoDbConnectionString
    return! json stats next ctx
}

/// Giraffe version of router
let giraffeRouter: HttpHandler =
    choose [
        GET >=> route "/todo/" >=> getAllTodos
        GET >=> routef "/todo/%s" getTodo
        POST >=> route "/todo/" >=> createTodo
        PUT >=> route "/todo/" >=> editTodo
        
        GET >=> route "/todo/stats" >=> getTodoStats
        PUT >=> routef "/todo/%s/complete" completeTodo
    ]

/// Saturn's version of router
let saturnRouter = router {
    get "/todo/" getAllTodos
    getf "/todo/%s" getTodo
    put "/todo/" editTodo
    post "/todo/" createTodo

    get "/todo/stats" getTodoStats
    putf "/todo/%s/complete" completeTodo
}
