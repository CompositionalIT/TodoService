open Giraffe
open Microsoft.Data.SqlClient
open Microsoft.Extensions.DependencyInjection
open Saturn
open System.Text.Encodings.Web
open System.Text.Json

module Infrastructure =
    /// A STJ serializer with some specific options set.
    let jsonSerializer =
        let options =
            JsonSerializerOptions(
                // PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Require camel case JSON
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Allow unescaped JSON
            )
        // options.Converters.Add(JsonFSharpConverter(allowNullFields = true)) // Nice F# JSON serialization
        SystemTextJson.Serializer options

    /// Give back a simpler error message if something goes wrong on SQL.
    let errorHandler (ex: exn) logger =
        match ex with
        | :? SqlException ->
            clearResponse
            >=> ServerErrors.INTERNAL_ERROR "An error occurred connecting to the database."
        | _ -> clearResponse >=> ServerErrors.INTERNAL_ERROR ex.Message

    /// Optional - only return text or JSON, not XML (which never works anyway).
    let customNegotiation =
        { new INegotiationConfig with
            member _.UnacceptableHandler =
                let d: INegotiationConfig = DefaultNegotiationConfig()
                d.UnacceptableHandler

            member _.Rules = dict [ "text/plain", string >> text; "*/*", json ]
        }

let app = application {
    use_router Todo.Api.giraffeRouter
    error_handler Infrastructure.errorHandler
    service_config (fun svc -> svc.AddSingleton Infrastructure.customNegotiation)
    use_json_serializer Infrastructure.jsonSerializer
}

run app