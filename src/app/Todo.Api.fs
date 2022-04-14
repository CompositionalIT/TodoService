module Todo.Api

open Giraffe
open Microsoft.AspNetCore.Http
open Saturn

/// Contains combinators to go from a ServiceError to an HttpHandler error.
module Result =
    let errorToHttpHandler next ctx error =
        match error with
        | Service.NotFound msg -> RequestErrors.NOT_FOUND msg next ctx
        | Service.ValidationErrors msgs -> RequestErrors.BAD_REQUEST msgs next ctx
        | Service.GenericError msg -> RequestErrors.BAD_REQUEST msg next ctx
    let toHttpHandler next ctx onSuccess result =
        match result with
        | Ok value -> onSuccess value
        | Error error -> errorToHttpHandler next ctx error

let connectionString = "Data Source=localhost,1434;Database=Todo;User=sa;Password=yourStrong(!)Password; TrustServerCertificate=True"

let createTodo next (ctx:HttpContext) = task {
    let! request = ctx.BindJsonAsync<Service.CreateTodoRequest>()
    let! result = Service.createTodo connectionString request
    return! result |> Result.toHttpHandler next ctx (fun () -> next ctx)
}

let getTodo (todoId:string) next (ctx:HttpContext) = task {
    let! result = Service.getTodoById connectionString todoId
    return! result |> Result.toHttpHandler next ctx (fun todo -> json todo next ctx)
}

let getAllTodos next ctx = task {
    let! results = Service.getAllTodos connectionString
    return! json results next ctx
}

let completeTodo (todoId:string) next (ctx:HttpContext) = task {
    let! result = Service.completeTodo connectionString todoId
    return! result |> Result.toHttpHandler next ctx (fun () -> next ctx)
}

// Create a function to edit the todo's title and description
let editTodo next (ctx:HttpContext) = task {
    let! request = ctx.BindJsonAsync<Service.EditTodoRequest>()
    let! result = Service.editTodo connectionString request
    return! result |> Result.toHttpHandler next ctx (fun () -> next ctx)
}

/// Giraffe version of router
let giraffeRouter : HttpHandler =
    choose [
        GET >=> route "/todo/" >=> getAllTodos
        POST >=> route "/todo/" >=> createTodo
        GET >=> routef "/todo/%s" getTodo
        PUT >=> route "/todo/" >=> editTodo
        PUT >=> routef "/todo/%s/complete" completeTodo
    ]

/// Saturn's version of router
let saturnRouter = router {
    get "/todo/" getAllTodos
    post "/todo/" createTodo
    getf "/todo/%s" getTodo
    put "/todo/" editTodo
    putf "/todo/%s/complete" completeTodo
}
