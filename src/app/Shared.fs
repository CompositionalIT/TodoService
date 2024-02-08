[<AutoOpen>]
module Shared

open Giraffe
open Microsoft.AspNetCore.Http
open Validus

/// Represents an error occurred while executing a service call.
type ServiceError =
    | DataNotFound of string
    | InvalidRequest of ValidationErrors
    | GenericError of string

    static member createInvalidRequest field error =
        error |> List.singleton |> ValidationErrors.create field |> InvalidRequest

/// Represents a Result of some successfully value, or a Service Error.
type ServiceResult<'T> = Result<'T, ServiceError>
/// Represents a Result of some success with no value, or a Service Error.
type ServiceResult = ServiceResult<unit>

type Result =

    /// Converts a Service Errors into an HTTPHandler response in a consistent way.
    static member toHttpHandler<'T>(result: 'T ServiceResult, ?onSuccess: 'T -> HttpHandler) : HttpHandler =
        match result with
        | Ok value ->
            let onSuccess = defaultArg onSuccess (fun _ next ctx -> next ctx)
            onSuccess value
        | Error(DataNotFound msg) -> RequestErrors.NOT_FOUND msg
        | Error(InvalidRequest msgs) -> RequestErrors.badRequest (msgs |> ValidationErrors.toMap |> json)
        | Error(GenericError msg) -> ServerErrors.INTERNAL_ERROR msg

module Result =
    let ofParseResult field originalValue v : _ ValidationResult =
        match v with
        | true, v -> Ok v
        | false, _ -> Error(ValidationErrors.create field [ $"Unable to parse '{originalValue}'" ])

    let ofRowsModified onNone rowsModified =
        match rowsModified with
        | 0 -> Error(DataNotFound onNone)
        | 1 -> Ok()
        | _ -> Error(GenericError $"Too many rows modified ({rowsModified})")

module Option =
    /// If value is None, returns Ok None, otherwise runs the validator on Some value and wraps the result in Some.
    /// Use this if you want to handle the case for optional data, when you want to validate data *only if there is some*.
    let toResultOption validator value =
        value
        |> Option.map (validator >> Result.map Some)
        |> Option.defaultWith (fun () -> Ok None)

    /// Maps None -> Error, Some x -> Ok x
    let toResult ifNone value =
        match value with
        | None -> Error ifNone
        | Some value -> Ok value