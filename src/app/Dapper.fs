module Dal

open Domain
open FsToolkit.ErrorHandling

/// A sample implementation of a single DAL method using Dapper.
module Dapper =
    open Dapper
    open Microsoft.Data.SqlClient

    let saveUsingDapper connectionString todo =
        let conn = new SqlConnection(connectionString)

        let parameters = {|
            Id = todo.Id.Value
            Title = todo.Title.Value
            Description = todo.Description |> Option.map _.Value |> Option.toObj
            CreatedDate = todo.CreatedDate
            CompletedDate = todo.CompletedDate |> Option.toNullable
        |}

        conn.ExecuteAsync(
            "INSERT INTO dbo.Todo (Id, Title, Description, CreatedDate, CompletedDate)
        VALUES (@Id, @Title, @Description, @CreatedDate, @CompletedDate)",
            parameters
        )