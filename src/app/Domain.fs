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

    static member TryCreate(field, todoId) : ValidationResult<TodoId> =
        Check.Guid.notEmpty field todoId |> Result.map TodoId

    static member TryCreate(field, guid: string) : ValidationResult<TodoId> =
        guid
        |> Guid.TryParse
        |> Result.ofParseResult field guid
        |> Result.bind (fun todoId -> TodoId.TryCreate(field, todoId))

type Todo = {
    Id: TodoId
    Title: String255
    Description: String255 option
    CreatedDate: DateTime
    CompletedDate: DateTime option
} with

    static member TryCreate(title, description, todoId) : ValidationResult<Todo> = validate {
        let! title = title |> String255.TryCreate "Title"

        and! todoId =
            match todoId with
            | Some(todoId: Guid) -> TodoId.TryCreate("TodoId", todoId)
            | None -> TodoId.Create() |> Ok

        and! description =
            description
            |> Option.ofObj
            |> Option.toResultOption (String255.TryCreate "Description")

        return {
            Id = todoId
            Title = title
            Description = description
            CreatedDate = DateTime.UtcNow
            CompletedDate = None
        }
    }