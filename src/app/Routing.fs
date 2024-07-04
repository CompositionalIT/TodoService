module Todo.Routing

open Environment
open Giraffe
open Microsoft.AspNetCore.Http
open Saturn

/// Giraffe's version of router
let giraffeRouter next (ctx: HttpContext) = task {
    let env = ctx.GetService<IEnv>()

    return!
        subRoute
            "/todo"
            (choose [
                GET >=> route "/" >=> Api.Queries.getAllTodos
                GET >=> route "/stats" >=> Api.Queries.GetTodoStats.handler env

                POST >=> route "/" >=> Api.Commands.CreateTodo.handler env
                DELETE >=> route "/" >=> Api.Commands.clearAllTodos

                GET >=> routef "/%s" (Api.Queries.GetTodo.handler env)
                PUT >=> routef "/%s" (Api.Commands.EditTodo.handler env)
                PUT >=> routef "/%s/complete" (Api.Commands.CompleteTodo.handler env)
                DELETE >=> routef "/%s" (Api.Commands.DeleteTodo.handler env)
            ])
            next
            ctx
}

/// Saturn's version of router
let saturnRouter next (ctx: HttpContext) = task {
    let env = ctx.GetService<IEnv>()

    let todoRoutes = router {
        get "/" Api.Queries.getAllTodos
        get "/stats" (Api.Queries.GetTodoStats.handler env)

        post "/" (Api.Commands.CreateTodo.handler env)
        post "/clear" Api.Commands.clearAllTodos

        getf "/%s" (Api.Queries.GetTodo.handler env)
        putf "/%s" (Api.Commands.EditTodo.handler env)
        putf "/%s/complete" (Api.Commands.CompleteTodo.handler env)
        deletef "/%s" (Api.Commands.DeleteTodo.handler env)
    }

    let appRouter = router { forward "/todo" todoRoutes }

    return! appRouter next ctx
}