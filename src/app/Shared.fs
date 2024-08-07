[<AutoOpen>]
module Shared

open Giraffe
open Validus
open System.Threading.Tasks
open FsToolkit.ErrorHandling

/// Represents an error that occurred while executing a service call.
type ServiceError =
    /// Used when the requested data was not found.
    | DataNotFound of string
    /// Used when the request was invalid, typically with validation errors.
    | InvalidRequest of ValidationErrors
    /// Used when a domain error occurred.
    | DomainError of string
    /// Used when a generic error occurred.
    | GenericError of string

    static member createInvalidRequest field error =
        error |> List.singleton |> ValidationErrors.create field |> InvalidRequest

/// Represents a Result of either an Ok with some payload or a Service Error.
type ServiceResult<'T> = Result<'T, ServiceError>
/// Represents a Result of either an OK with no payload or a Service Error.
type ServiceResult = ServiceResult<unit>
/// Represents an asynchronous service result.
type ServiceResultAsync<'T> = Task<ServiceResult<'T>>
type ServiceResultAsync = Task<ServiceResult>

type Result =
    /// Converts a Service Errors into an HTTPHandler response in a consistent way.
    static member toHttpHandler<'T>(result: 'T ServiceResult, ?onOk: 'T -> HttpHandler) : HttpHandler =
        match result with
        | Ok value ->
            let onSuccess = defaultArg onOk (fun _ next ctx -> next ctx)
            onSuccess value
        | Error(DataNotFound message) -> RequestErrors.NOT_FOUND message
        | Error(InvalidRequest messages) -> RequestErrors.badRequest (messages |> ValidationErrors.toMap |> json)
        | Error(DomainError message) -> RequestErrors.BAD_REQUEST message
        | Error(GenericError message) -> ServerErrors.INTERNAL_ERROR message

module Result =
    let inline ofParseResult field unparsedValue : _ ValidationResult =
        unparsedValue
        |> Option.tryParse
        |> Result.requireSome (ValidationErrors.create field [ $"Unable to parse '{unparsedValue}'" ])

module Option =
    /// If value is None, returns Ok None, otherwise runs the validator on Some value and wraps the result in Some.
    /// Use this if you want to handle the case for optional data, when you want to validate data *only if there is some*.
    let toResultOption validator value =
        value
        |> Option.map (validator >> Result.map Some)
        |> Option.defaultWith (fun () -> Ok None)

open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Http

type HttpContext with

    /// The SQL connection string to the Todo database.
    member this.TodoDbConnectionString =
        match this.GetService<IConfiguration>().GetConnectionString "TodoDb" with
        | null -> failwith "Missing connection string"
        | v -> v