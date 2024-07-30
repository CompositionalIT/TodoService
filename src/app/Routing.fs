module Todo.Routing

open Giraffe
open Saturn

/// Giraffe's version of router
let giraffeRouter: HttpHandler =
    subRoute
        "/todo"
        (choose [
            GET >=> route "/" >=> Api.Queries.getAllTodos
            GET >=> route "/stats" >=> Api.Queries.getTodoStats

            POST >=> route "/" >=> Api.Commands.CreateTodo.handler
            DELETE >=> route "/" >=> Api.Commands.clearAllTodos

            GET >=> routef "/%s" Api.Queries.getTodo
            PUT >=> routef "/%s" Api.Commands.EditTodo.handler
            PUT >=> routef "/%s/complete" Api.Commands.CompleteTodo.handler
            DELETE >=> routef "/%s" Api.Commands.DeleteTodo.handler
        ])

/// Saturn's version of router
let saturnRouter: HttpHandler =
    let z : String.String = null
    subRoute
        "/todo"
        (router {
            get "/" Api.Queries.getAllTodos
            get "/stats" Api.Queries.getTodoStats

            post "/" Api.Commands.CreateTodo.handler
            post "/clear" Api.Commands.clearAllTodos

            getf "/%s" Api.Queries.getTodo
            putf "/%s" Api.Commands.EditTodo.handler
            putf "/%s/complete" Api.Commands.CompleteTodo.handler
            deletef "/%s" Api.Commands.DeleteTodo.handler
        })