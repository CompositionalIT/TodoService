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

type Wrapped<'T> =
    private
    | Data of 'T

    member this.Value =
        match this with
        | Data v -> v

type CompletedTodo =
    Wrapped<{|
        CompletedId: TodoId
        Date: DateTime
    |}>

type DeletedTodo = Wrapped<{| DeletedId: TodoId |}>

type EditedTodo =
    Wrapped<{|
        EditedId: TodoId
        Title: String255
        Description: String255 option
    |}>

[<RequireQualifiedAccess>]
module Todo =
    let ifActive mapper todo =
        if todo.CompletedDate.IsSome then
            Error "Todo is already completed."
        else
            mapper todo |> Ok

    let create title description todoId = {
        Id = todoId |> Option.defaultWith (fun () -> TodoId.Create())
        Title = title
        Description = description
        CreatedDate = DateTime.UtcNow
        CompletedDate = None
    }

    /// Completes the todo.
    let complete todo =
        todo
        |> ifActive (fun _ ->
            let date = DateTime.UtcNow
            Data {| CompletedId = todo.Id; Date = date |}: CompletedTodo)

    /// Checks if a todo can be deleted.
    let delete todo =
        todo |> ifActive (fun _ -> Data {| DeletedId = todo.Id |}: DeletedTodo)

    /// Sets the title AND description of the Todo.
    let edit title description todo =
        todo
        |> ifActive (fun this ->
            Data {|
                EditedId = todo.Id
                Title = title
                Description = description
            |}
            : EditedTodo)