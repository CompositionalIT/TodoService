namespace Domain

open FsToolkit.ErrorHandling
open System

type String255 =
    | String255 of string
    static member TryCreate name (s:string) =
        if isNull s then ValidationError.Create name "No value provided." |> Error
        elif s.Length > 255 then ValidationError.Create name "Field is too long." |> Error
        else Ok (String255 s)
    member this.Value = match this with String255 v -> v

type TodoId =
    private | TodoId of Guid
    member this.Value = match this with TodoId v -> v
    static member Create () = TodoId (Guid.NewGuid())
    static member TryCreate name todoId =
        if todoId = Guid.Empty then Error (ValidationError.Create name "This is the empty GUID.")
        else Ok (TodoId todoId)
    static member TryParse field (guid:string) =
        guid
        |> Guid.TryParse
        |> Result.ofTryParse guid
        |> Result.map TodoId
        |> Result.mapError (ValidationError.Create field)

type Todo =
    {
        Id : TodoId
        Title : String255
        Description : String255 option
        CreatedDate : DateTime
        CompletedDate : DateTime option
    }

    static member TryCreate (title, description) = validation {
        let! title = String255.TryCreate "Title" title
        and! description =
            description
            |> Option.ofObj
            |> Option.toResultOption (String255.TryCreate "Description")
        return {
            Id = TodoId.Create ()
            Title = title
            Description = description
            CreatedDate = DateTime.UtcNow
            CompletedDate = None
        }
    }
