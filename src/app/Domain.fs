namespace Domain

open FsToolkit.ErrorHandling
open System
open Validus
open Validus.Operators

type String255 =
    private
    | String255 of string

    static member TryCreate field value : ValidationResult<String255> =
        (Check.String.notEmpty >=> Check.String.lessThanLen 255) field value
        |> Result.map String255

    member this.Value =
        match this with
        | String255 v -> v

type TodoId =
    private
    | TodoId of Guid

    member this.Value =
        match this with
        | TodoId v -> v

    static member Create() = Guid.NewGuid() |> TodoId

    static member TryCreate(todoId, ?field) : ValidationResult<TodoId> =
        let field = field |> Option.defaultValue "TodoId"
        todoId |> Check.Guid.notEmpty field |> Result.map TodoId

    static member TryCreate(guid: string, ?field) : ValidationResult<TodoId> = result {
        let field = field |> Option.defaultValue "TodoId"
        let! todoId = guid |> Result.ofParseResult field
        return! TodoId.TryCreate(todoId, field = field)
    }

type Todo = {
    Id: TodoId
    Title: String255
    Description: String255 option
    CreatedDate: DateTime
    CompletedDate: DateTime option
}

type Cmd<'T> =
    private
    | Data of 'T

    member this.Value =
        match this with
        | Data v -> v

[<AutoOpen>]
module Patterns =
    /// Unwraps the Value member of some data.
    let inline (|Data|) x = (^a: (member Value: _) x)

type CreateTodoCmd =
    Cmd<{|
        Id: Guid
        Title: string
        Description: string option
        CreatedDate: DateTime
        CompletedDate: DateTime option
    |}>

type CompleteTodoCmd = Cmd<{| Id: Guid; Date: DateTime |}>
type DeleteTodoCmd = Cmd<{| Id: Guid |}>

type EditTodoCmd =
    Cmd<{|
        Id: Guid
        Title: string
        Description: string
    |}>

[<RequireQualifiedAccess>]
module Todo =
    let private checkActive todo =
        if todo.CompletedDate.IsSome then
            Error "Todo is already completed."
        else
            Ok()

    /// Creates a new todo, with an optional todoId.
    let create (title: String255) (description: String255 option) (todoId: TodoId option) =
        Data {|
            Id =
                todoId
                |> Option.map _.Value
                |> Option.defaultWith (fun () -> TodoId.Create().Value)
            Title = title.Value
            Description = description |> Option.map _.Value
            CreatedDate = DateTime.UtcNow
            CompletedDate = None
        |}
        : CreateTodoCmd

    /// Completes the todo.
    let complete todo = result {
        do! checkActive todo

        return
            (Data {|
                Id = todo.Id.Value
                Date = DateTime.UtcNow
            |}
            : CompleteTodoCmd)
    }

    /// Checks if a todo can be deleted.
    let delete todo = result {
        do! checkActive todo
        return (Data {| Id = todo.Id.Value |}: DeleteTodoCmd)
    }

    /// Sets the title AND description of the Todo.
    let edit title description todo = result {
        do! checkActive todo

        if title = todo.Title && description = todo.Description then
            return! Error "No changes were made."

        return
            (Data {|
                Id = todo.Id.Value
                Title = title.Value
                Description = description |> Option.map _.Value |> Option.toObj
            |}
            : EditTodoCmd)
    }