namespace Domain

open FsToolkit.ErrorHandling
open System

module Result =
    let optionToResultNoneOk validator v =
        match v with
        | None ->
            Ok None
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
        if isNull s then Error $"No value provided for field '{name}'."
        elif s.Length > 255 then Error "Too long"
        else Ok (String255 s)
    member this.Value = match this with String255 v -> v

type TodoId =
    private | TodoId of Guid
    member this.Value = match this with TodoId v -> v
    static member Create () = TodoId (Guid.NewGuid())
    static member Create todoId = TodoId todoId
    static member TryParse (guid:string) =
        guid
        |> Guid.TryParse
        |> Result.ofParseResult
        |> Result.map TodoId

type Todo =
    {
        Id : TodoId
        Title : String255
        Description : String255 option
        CreatedDate : DateTime
        CompletedDate : DateTime option
    }

    static member TryCreate (title, description) = result {
        let! title = String255.TryCreate "Title" title
        let! description =
            description
            |> Option.ofObj
            |> Result.optionToResultNoneOk (String255.TryCreate "Description")
        return {
            Id = TodoId.Create ()
            Title = title
            Description = description
            CreatedDate = DateTime.UtcNow
            CompletedDate = None
        }
    }
