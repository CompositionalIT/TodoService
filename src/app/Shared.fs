[<AutoOpen>]
module Shared

open Microsoft.AspNetCore.Http

type ValidationError =
    { Field : string; Error : string }
    static member Create field message = { Field = field; Error = message }

/// Represents an error occurred while executing a service call.
type ServiceError =
    | DataNotFound of string
    | InvalidRequest of ValidationError list
    | GenericError of string
    static member ofValidationError error = error |> List.singleton |> InvalidRequest

type ServiceResult<'T> = Result<'T, ServiceError>
type ServiceResult = ServiceResult<unit>

module Result =
    open Giraffe
    let ofTryParse originalValue v =
        match v with
        | true, v -> Ok v
        | false, _ -> Error $"Parse failed: '{originalValue}'"

    /// Converts a Service Errors into an HTTPHandler response in a consistent way.
    let toHttpHandler next ctx onSuccess result =
        match result with
        | Ok value ->
            onSuccess value next ctx
        | Error error ->
            match error with
            | DataNotFound msg -> RequestErrors.NOT_FOUND msg next ctx
            | InvalidRequest msgs -> RequestErrors.BAD_REQUEST msgs next ctx
            | GenericError msg -> ServerErrors.INTERNAL_ERROR msg next ctx

    let ofRowsModified onNone rowsModified =
        match rowsModified with
        | 0 -> Error (DataNotFound onNone)
        | 1 -> Ok ()
        | _ -> Error (GenericError $"Too many rows modified ({rowsModified})")

module Option =
    /// If value is None, returns Ok None, otherwise runs the validator on Some value and wraps the result in Some.
    /// Use this if you want to handle the case for optional data, when you want to validate data *only if there is some*.
    let toResultOption validator value =
        value
        |> Option.map (validator >> Result.map Some)
        |> Option.defaultWith (fun () -> Ok None)

    // Maps None -> Error, Some x -> Ok x
    let toResult ifNone value =
        match value with
        | None -> Error ifNone
        | Some value -> Ok value