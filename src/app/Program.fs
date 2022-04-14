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
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            )
        )
    )

let app = application {
    use_router Todo.Api.giraffeRouter
    service_config configureServices
}

run app

(*

-- CREATE TODO
POST http://localhost:5000/todo/

{
    "Title": "Another Todo",
    "description": "This is a second test"
}

-- GET TODO
http://localhost:5000/todo/f453dd17-d3f7-4bb1-9dd4-f707ea202f83

-- GET ALL TODOS
http://localhost:5000/todo/

-- COMPLETE TODO
PUT http://localhost:5000/todo/f453dd17-d3f7-4bb1-9dd4-f707ea202f82/complete

-- EDIT TODO
PUT http://localhost:5000/todo/

{
    "Id": "f453dd17-d3f7-4bb1-9dd4-f707ea202f83",
    "Title": "Edited todo",
    "Description": "Updated description"
}

*)