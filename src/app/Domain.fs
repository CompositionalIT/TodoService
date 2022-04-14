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

type String255 =
    | String255 of string
    static member Create (s:string) =
        if isNull s then Error "No value provided"
        elif s.Length > 255 then Error "Too long"
        else Ok (String255 s)
    member this.Value = match this with String255 v -> v

type TodoId =
    | TodoId of Guid
    member this.Value = match this with TodoId v -> v
    static member Create () =
        TodoId (Guid.NewGuid())

type Todo =
    {
        Id : TodoId
        Title : String255
        Description : String255 option
        CreatedDate : DateTime
        CompletedDate : DateTime option
    }

    static member TryCreate (title, description) = result {
        let! title = String255.Create title
        let! description =
            description
            |> Option.ofObj
            |> Result.optionToResultNoneOk String255.Create
        return {
            Id = TodoId.Create ()
            Title = title
            Description = description
            CreatedDate = DateTime.UtcNow
            CompletedDate = None
        }
    }
