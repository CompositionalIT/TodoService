module Todo.Api

open Db.TableDtos
open Giraffe
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Http
open Saturn
open System.Threading.Tasks

type HttpContext with
    member this.TodoDbConnectionString =
        this
            .GetService<IConfiguration>()
            .GetConnectionString "TodoDb"

let createTodo next (ctx: HttpContext) =
    task {
        let! request = ctx.BindJsonAsync<Service.CreateTodoRequest>()
        let! result = Service.createTodo ctx.TodoDbConnectionString request

        return!
            result
            |> Result.toHttpHandler next ctx (fun _ next ctx -> next ctx)
    }

let getTodo (todoId: string) next (ctx: HttpContext) =
    task {
        let! result = Service.getTodoById ctx.TodoDbConnectionString todoId
        return! result |> Result.toHttpHandler next ctx json
    }

let getAllTodos next (ctx: HttpContext) =
    task {
        let! results = Service.getAllTodos ctx.TodoDbConnectionString
        return! json results next ctx
    }

let completeTodo (todoId: string) next (ctx: HttpContext) =
    task {
        let! result = Service.completeTodo ctx.TodoDbConnectionString todoId

        return!
            result
            |> Result.toHttpHandler next ctx (fun _ next ctx -> next ctx)
    }

// Create a function to edit the todo's title and description
let editTodo next (ctx: HttpContext) =
    task {
        let! request = ctx.BindJsonAsync<Service.EditTodoRequest>()
        let! result = Service.editTodo ctx.TodoDbConnectionString request

        return!
            result
            |> Result.toHttpHandler next ctx (fun _ next ctx -> next ctx)
    }

let getTodoStats next (ctx: HttpContext) =
    task {
        let! stats = Service.getTodoStats ctx.TodoDbConnectionString
        return! json stats next ctx
    }

/// Giraffe version of router
let giraffeRouter: HttpHandler =
    choose
        [
            GET >=> route "/todo/" >=> getAllTodos
            POST >=> route "/todo/" >=> createTodo
            GET >=> route "/todo/stats" >=> getTodoStats
            GET >=> routef "/todo/%s" getTodo
            PUT >=> route "/todo/" >=> editTodo
            PUT >=> routef "/todo/%s/complete" completeTodo
        ]

/// Saturn's version of router
let saturnRouter =
    router {
        get "/todo/" getAllTodos
        post "/todo/" createTodo
        get "/todo/stats" getTodoStats
        getf "/todo/%s" getTodo
        put "/todo/" editTodo
        putf "/todo/%s/complete" completeTodo
    }
