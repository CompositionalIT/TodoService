open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Saturn
open System.Text.Json

let configureServices (services:IServiceCollection) =
    services.AddSingleton<Json.ISerializer>(
        SystemTextJson.Serializer(
            JsonSerializerOptions(
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Emit camel case JSON
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Allow unescaped JSON
            )
        )
    )

let app = application {
    use_router Todo.Api.giraffeRouter
    service_config configureServices
}

run app