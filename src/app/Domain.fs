namespace Domain

open FsToolkit.ErrorHandling
open System

module Result =
    let optionToResultOption validator onNone v =
        match v with
        | None ->
            onNone
        | Some v ->
            match validator v with
            | Ok v ->
                Ok (Some v)
            | Error r ->
                Error r
    let ofParseResult v =
        match v with
        | true, v -> Ok v
        | false, _ -> Error $"Parse failed: {v}"


type String255 =
    | String255 of string
    static member TryCreate name (s:string) =
        if isNull s then Error {| Field = name; Error = $"No value provided." |}
        elif s.Length > 255 then Error {| Field = name; Error = $"Field is too long." |}
        else Ok (String255 s)
    member this.Value = match this with String255 v -> v

type TodoId =
    private | TodoId of Guid
    member this.Value = match this with TodoId v -> v
    static member Create () = TodoId (Guid.NewGuid())
    static member TryCreate todoId =
        if todoId = Guid.Empty then Error "This is the empty GUID."
        else Ok (TodoId todoId)
    static member TryParse (guid:string) =
        guid
        |> Guid.TryParse
        |> Result.ofParseResult
        |> Result.map TodoId
        |> Result.mapError (fun _ -> "Not a valid GUID")

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
            |> Result.optionToResultOption (String255.TryCreate "Description") (Ok None)
        return {
            Id = TodoId.Create ()
            Title = title
            Description = description
            CreatedDate = DateTime.UtcNow
            CompletedDate = None
        }
    }
