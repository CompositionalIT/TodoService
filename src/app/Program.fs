open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Saturn
open System.Text.Json
open System.Text.Json.Serialization
open System.Collections.Generic
open Microsoft.Extensions.Logging


type CustomerDatabase() =
    let customers = Dictionary<int, {| Name: string |}>()

    member _.LoadCustomer(customerId: int) =
        if customers.ContainsKey customerId then
            customers.[customerId]
        else
            let fromDb = {| Name = "Isaac" |}
            customers[customerId] <- fromDb
            fromDb

type CustomerService(db: CustomerDatabase, logger: ILogger) =
    member _.TryLoadCustomer(customerId: int) =
        if customerId < 99 then
            failwith "Bad request"
        else
            db.LoadCustomer(customerId)


let configureServices (services: IServiceCollection) =
    // IOC Containers (Inversion of Control Container)

    services.AddTransient<CustomerDatabase>()
    |> ignore

    let service =
        services
            .BuildServiceProvider()
            .GetService<CustomerService>()

    let c = service.TryLoadCustomer 123

    services.AddSingleton<Json.ISerializer>(
        SystemTextJson.Serializer(
            let options =
                JsonSerializerOptions(
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Emit camel case JSON
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Allow unescaped JSON
                )
            // options.Converters.Add(JsonFSharpConverter(allowNullFields = true)) // Nice F# JSON serialization
            options
        )
    )


let app =
    application {
        use_router Todo.Api.giraffeRouter
        service_config configureServices
    }

run app
