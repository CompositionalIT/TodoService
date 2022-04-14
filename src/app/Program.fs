open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Saturn
open System.Text.Json

let connectionString = "Data Source=localhost,1434;Database=Todo;User=sa;Password=yourStrong(!)Password; TrustServerCertificate=True"

let createTodo next (ctx:HttpContext) = task {
    let! request = ctx.BindJsonAsync<TodoService.CreateTodoRequest>()
    let! result = TodoService.createTodo connectionString request
    return!
        match result with
        | Ok _ -> next ctx
        | Error err -> Response.badRequest ctx err
}

let getTodoById (todoId:string) next (ctx:HttpContext) = task {
    let! result = TodoService.getTodoById connectionString todoId
    return!
        match result with
        | Ok (Some todo) -> json todo next ctx
        | Ok None -> Response.notFound ctx $"No such todo {todoId}"
        | Error err -> Response.badRequest ctx err
}

let getAllTodos next ctx = task {
    let! results = TodoService.getAllTodos connectionString
    return! json results next ctx
}

/// Saturn's version of router
let saturnRouter = router {
    post "/" createTodo
    get "/todo" getAllTodos
    getf "/todo/%s" getTodoById
}

/// Giraffe version of router
let giraffeRouter =
    choose [
        POST >=> createTodo
        GET >=> route "/todo" >=> getAllTodos
        GET >=> routef "/todo/%s" getTodoById
    ]

let app = application {
    use_router giraffeRouter
    service_config (fun svc ->
        svc.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(JsonSerializerOptions())))
}

run app


(*

POST http://localhost:5000/

{
    "Title": "Another Todo",
    "Description": "This is a second test"
}

http://localhost:5000/todo/f453dd17-d3f7-4bb1-9dd4-f707ea202f83
http://localhost:5000/todo

*)
