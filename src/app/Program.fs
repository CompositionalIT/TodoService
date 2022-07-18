open Giraffe
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Hosting
open Saturn
open System.Collections.Generic
open System.Text.Encodings.Web
open System.Text.Json

let serializer =
    let options =
        JsonSerializerOptions(
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Emit camel case JSON
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Allow unescaped JSON
        )
    // options.Converters.Add(JsonFSharpConverter(allowNullFields = true)) // Nice F# JSON serialization
    SystemTextJson.Serializer options

let app =
    application {
        use_router Todo.Api.giraffeRouter
        use_json_serializer serializer
    }

run app
