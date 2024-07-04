namespace Environment

open Domain
open Db
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open System.Threading.Tasks
open System
open Validus

[<AutoOpen>]
module Extensions =
    type IConfiguration with

        /// The SQL connection string to the Todo database.
        member this.TodoDbConnectionString =
            match this.GetConnectionString "TodoDb" with
            | null -> failwith "Missing connection string"
            | v -> v

    type HttpContext with

        /// The SQL connection string to the Todo database.
        member this.TodoDbConnectionString =
            this.GetService<IConfiguration>().TodoDbConnectionString

type Result =
    /// Converts a Service Errors into an HTTPHandler response in a consistent way.
    static member toHttpHandler<'T>(result: 'T ServiceResult, ?onOk: 'T -> HttpHandler) : HttpHandler =
        match result with
        | Ok value ->
            let onSuccess = defaultArg onOk (fun _ next ctx -> next ctx)
            onSuccess value
        | Error(DataNotFound message) -> RequestErrors.NOT_FOUND message
        | Error(InvalidRequest messages) -> RequestErrors.badRequest (messages |> ValidationErrors.toMap |> json)
        | Error(DomainError message) -> RequestErrors.BAD_REQUEST message
        | Error(GenericError message) -> ServerErrors.INTERNAL_ERROR message

// Note: We use parens aroudn interface members to allow partial application.

// Query repository
type IQueryTodo =
    abstract GetTodoById: (TodoId -> ServiceResultAsync<TableDtos.dbo.Todo>)

    abstract GetStats:
        (unit
            -> Task<ResizeArray<{|
                CompletionState: string
                TodoItems: int option
            |}>>)

// Command repository
type ICommandTodo =
    abstract CompleteTodo: (CompleteTodoCmd -> ServiceResultAsync)
    abstract CreateTodo: (CreateTodoCmd -> ServiceResultAsync<Guid>)
    abstract DeleteTodo: (DeleteTodoCmd -> ServiceResultAsync)
    abstract EditTodo: (EditTodoCmd -> ServiceResultAsync)

// Environment - all dependencies etc. required by the app
type IEnv =
    inherit IQueryTodo
    inherit ICommandTodo