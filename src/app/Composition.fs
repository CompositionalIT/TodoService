namespace Composition

open Environment
open Microsoft.Extensions.Configuration
open Todo.Api

type RealEnvironment(config: IConfiguration) =
    let connectionString = config.TodoDbConnectionString

    interface IEnv with
        member _.GetTodoById = Queries.GetTodo.read connectionString
        member _.CreateTodo = Commands.CreateTodo.write connectionString
        member _.DeleteTodo = Commands.DeleteTodo.write connectionString
        member _.EditTodo = Commands.EditTodo.write connectionString
        member _.CompleteTodo = Commands.CompleteTodo.write connectionString
        member _.GetStats = fun () -> Queries.GetTodoStats.read connectionString