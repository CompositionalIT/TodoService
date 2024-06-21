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
        let! todoId = guid |> Guid.TryParse |> Result.ofParseResult field guid
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

type CreateTodoCmd =
    Cmd<{|
        CreatedId: TodoId
        Title: String255
        Description: String255 option
        CreatedDate: DateTime
    |}>

type CompleteTodoCmd =
    Cmd<{|
        CompletedId: TodoId
        Date: DateTime
    |}>

type DeleteTodoCmd = Cmd<{| DeletedId: TodoId |}>

type EditTodoCmd =
    Cmd<{|
        EditedId: TodoId
        Title: String255
        Description: String255 option
    |}>

[<RequireQualifiedAccess>]
module Todo =
    let checkActive todo =
        if todo.CompletedDate.IsSome then
            Error "Todo is already completed."
        else
            Ok()

    let create title description todoId =
        Data {|
            CreatedId = todoId |> Option.defaultWith (fun () -> TodoId.Create())
            Title = title
            Description = description
            CreatedDate = DateTime.UtcNow
        |}
        : CreateTodoCmd

    /// Completes the todo.
    let complete todo = result {
        do! checkActive todo

        return
            (Data {|
                CompletedId = todo.Id
                Date = DateTime.UtcNow
            |}
            : CompleteTodoCmd)
    }

    /// Checks if a todo can be deleted.
    let delete todo = result {
        do! checkActive todo
        return (Data {| DeletedId = todo.Id |}: DeleteTodoCmd)
    }

    /// Sets the title AND description of the Todo.
    let edit title description todo : Result<EditTodoCmd, _> = result {
        do! checkActive todo

        if title = todo.Title && description = todo.Description then
            return! Error "No changes were made."

        return
            Data {|
                EditedId = todo.Id
                Title = title
                Description = description
            |}
    }