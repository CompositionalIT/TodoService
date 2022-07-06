namespace Domain

open FsToolkit.ErrorHandling
open System

type SafeString =
    static member TryCreate (value : string, builder, ?minLength, ?maxLength) =
        match value, minLength, maxLength with
        | null, _, _ -> Error "No value provided."
        | value, Some min, _ when value.Length < min -> Error $"Field is too short (min is {min}, but was {value.Length})."
        | value, _, Some max when value.Length > max -> Error $"Field is too long (max is {max}, but was {value.Length})."
        | value, _, _ -> Ok (builder value)

type String255 =
    private | String255 of string
    static member TryCreate value = SafeString.TryCreate (value, String255, maxLength = 255)
    member this.Value = match this with String255 v -> v

type TodoId =
    private | TodoId of Guid
    member this.Value = match this with TodoId v -> v
    static member Create () = TodoId (Guid.NewGuid())
    static member TryCreate todoId =
        if todoId = Guid.Empty then Error "This is the empty GUID."
        else Ok (TodoId todoId)
    static member TryCreate (guid:string) =
        guid
        |> Guid.TryParse
        |> Result.ofTryParse guid
        |> Result.map TodoId

type Todo =
    {
        Id : TodoId
        Title : String255
        Description : String255 option
        CreatedDate : DateTime
        CompletedDate : DateTime option
    }

    static member TryCreate (title, description) = validation {
        let! title = title |> Result.tryCreate "Title"
        and! description =
            description
            |> Option.ofObj
            |> Option.toResultOption (Result.tryCreate "Description")
        return {
            Id = TodoId.Create ()
            Title = title
            Description = description
            CreatedDate = DateTime.UtcNow
            CompletedDate = None
        }
    }