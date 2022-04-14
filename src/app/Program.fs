open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open System
open FsToolkit.ErrorHandling

// HttpHandlers

(*

    HttpContext => (HttpContext optionally) Task

text
    HttpContext.Response.ContentType <- "text/plain"
    HttpContext.Response.Body <- "Hello, world"

GET
    if HttpContext.Request.Method = GET then hand off to the next pipeline stage
    OTHERWISE None
*)

type CreateTodoRequest =
    {
        Title : string
        Description : string
    }

type String255 =
    | String255 of string
    static member Create (s:string) =
        if isNull s then Error "No value provided"
        elif s.Length > 255 then Error "Too long"
        else Ok (String255 s)
    member this.Value = match this with String255 v -> v

module Result =
    let optionToResultNoneOk validator v =
        match v with
        | None -> Ok None
        | Some v ->
            match validator v with
            | Ok v -> Ok (Some v)
            | Error r -> Error r

type TodoId = TodoId of Guid member this.Value = match this with TodoId v -> v

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
            Id = TodoId (Guid.NewGuid())
            Title = title
            Description = description
            CreatedDate = DateTime.UtcNow
            CompletedDate = None
        }
    }

let connectionString = "Data Source=localhost,1434;Database=Todo;User=sa;Password=yourStrong(!)Password; TrustServerCertificate=True"

open Microsoft.Data.SqlClient
open Dapper

let saveUsingDapper todo =
    let conn = new SqlConnection(connectionString)
    let parameters =
        {|
            Id = todo.Id.Value
            Title = todo.Title.Value
            Description = todo.Description |> Option.map (fun r -> r.Value) |> Option.toObj
            CreatedDate = todo.CreatedDate
            CompletedDate = todo.CompletedDate |> Option.toNullable
        |}
    conn.ExecuteAsync("INSERT INTO dbo.Todo (Id, Title, Description, CreatedDate, CompletedDate)
    VALUES (@Id, @Title, @Description, @CreatedDate, @CompletedDate)", parameters)

let createTodo next (ctx:HttpContext) = task {
    let! request = ctx.BindJsonAsync<CreateTodoRequest>()
    let todo = Todo.TryCreate (request.Title, request.Description)
    match todo with
    | Ok todo ->
        let! response =
            TodoDb.Scripts.Todo_Insert
                .WithConnection(connectionString)
                .WithParameters(
                    todo.Id.Value,
                    todo.Title.Value,
                    todo.Description |> Option.map (fun r -> r.Value),
                    todo.CreatedDate,
                    todo.CompletedDate)
                .ExecuteAsync()
        return! next ctx
    | Error err ->
        return! Response.badRequest ctx err
}

let getTodoById (todoId:string) next (ctx:HttpContext) = task {
    match Guid.TryParse todoId with
    | true, value ->
        let! result = TodoDb.Scripts.Todo_ById.WithConnection(connectionString).WithParameters(value).AsyncExecuteSingle()
        match result with
        | None -> return! Response.notFound ctx "Unknown todo id"
        | Some todo -> return! json todo next ctx
    | false, _ ->
        return! Response.badRequest ctx "Invalid todo id"
}

let getAllTodos next ctx = task {
    let! results = TodoDb.Scripts.Queries.GetAllItems.WithConnection(connectionString).AsyncExecute()
    return! json results next ctx
}

(*

POST http://localhost:5000/

{
    "Title": "Another Todo",
    "Description": "This is a second test"
}

http://localhost:5000/todo/f453dd17-d3f7-4bb1-9dd4-f707ea202f83
http://localhost:5000/todo


*)

let myRoutes =
    choose [
        POST >=> createTodo
        GET >=> route "/todo" >=> getAllTodos
        GET >=> routef "/todo/%s" getTodoById

        // create
        // get by id
        // update
        // custom SQL
    ]

open Microsoft.Extensions.DependencyInjection
open System.Text.Json
open Microsoft.Extensions.Configuration

type DbRepo (config:IConfiguration) =
    member _.SaveToDb() = ()
    member _.AddTwoNumbers(a,b) = a + b

let app = application {
    use_router myRoutes
    service_config (fun svc ->
        svc.AddScoped<DbRepo>() |> ignore
        svc.AddSingleton<Json.ISerializer>(
            SystemTextJson.Serializer(JsonSerializerOptions())))
}

run app

(*
    Saturn F#
    Giraffe F#
    ASP .NET Core (request / response pipeline) C# / F# / VB .NET
    ASP .NET Core - Kestrel C# / F# / VB .NET
*)