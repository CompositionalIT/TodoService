namespace Domain

open FsToolkit.ErrorHandling
open System
open Validus

type String255 =
    private
    | String255 of string

    static member TryCreate field value =
        Check.String.lessThanLen 255 field value |> Result.map String255

    member this.Value =
        match this with
        | String255 v -> v

type TodoId =
    private
    | TodoId of Guid

    member this.Value =
        match this with
        | TodoId v -> v

    static member Create() = TodoId(Guid.NewGuid())

    static member TryCreate(field, todoId) : _ ValidationResult =
        Check.Guid.notEmpty field todoId |> Result.map TodoId

    static member TryCreate(field, guid: string) =
        guid
        |> Guid.TryParse
        |> Result.ofParseResult field guid
        |> Result.bind (fun todoId -> TodoId.TryCreate(field, todoId))

type Todo =
    {
        Id: TodoId
        Title: String255
        Description: String255 option
        CreatedDate: DateTime
        CompletedDate: DateTime option
    }

    static member TryCreate(title, description) = validate {
        let! title = title |> String255.TryCreate "Title"

        and! description =
            description
            |> Option.ofObj
            |> Option.toResultOption (String255.TryCreate "Description")

        return {
            Id = TodoId.Create()
            Title = title
            Description = description
            CreatedDate = DateTime.UtcNow
            CompletedDate = None
        }
    }
