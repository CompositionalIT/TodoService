module Todo.Service

open Db.Scripts
open Domain
open FsToolkit.ErrorHandling
open System
open System.Threading.Tasks
open Validus

type CreateTodoRequest = {
    TodoId: Guid Nullable
    Title: string
    Description: string
}

type EditTodoRequest = {
    Id: string
    Title: string
    Description: string
}

module Queries =
    let getAllTodos (connectionString: string) =
        DbQueries.GetAllItems.WithConnection(connectionString).ExecuteAsync()

    let getTodoStats (connectionString: string) = task {
        let! stats = DbQueries.GetTodoStats.WithConnection(connectionString).ExecuteAsync()

        let getStat status =
            stats
            |> Seq.tryPick (fun row -> if row.CompletionState = status then row.TodoItems else None)
            |> Option.defaultValue 0

        return {|
            Completed = getStat "Complete"
            Incomplete = getStat "Incomplete"
        |}
    }

    let getTodoById (connectionString: string) (todoId: string) : _ ServiceResultAsync = taskResult {
        let! todoId = todoId |> TodoId.TryCreate |> Result.mapError InvalidRequest

        // Using Facil's built-in CRUD script to get a Todo by ID.
        let! todo =
            Todo_ById
                .WithConnection(connectionString)
                .WithParameters(todoId.Value)
                .AsyncExecuteSingle()

        return! todo |> Result.requireSome (DataNotFound $"Unknown Todo {todoId.Value}")
    }

    let loadTodo connectionString todoId : ServiceResultAsync<Todo> = taskResult {
        let! result = getTodoById connectionString todoId

        return!
            validate {
                let! todoId = TodoId.TryCreate(result.Id, "Id")
                let! title = result.Title |> String255.TryCreate "Title"
                let! description = result.Description |> Option.toResultOption (String255.TryCreate "Description")

                return {
                    Id = todoId
                    Title = title
                    Description = description
                    CreatedDate = result.CreatedDate
                    CompletedDate = result.CompletedDate
                }
            }
            |> Result.mapError InvalidRequest
    }

module Commands =
    let createTodo connectionString (request: CreateTodoRequest) : ServiceResultAsync<_> = taskResult {
        let! title, todoId, description =
            validate {
                let! title = request.Title |> String255.TryCreate "Title"
                and! todoId = request.TodoId |> Option.ofNullable |> Option.toResultOption TodoId.TryCreate

                and! description =
                    request.Description
                    |> Option.ofObj
                    |> Option.toResultOption (String255.TryCreate "Description")

                return title, todoId, description
            }
            |> Result.mapError InvalidRequest

        let command = Todo.create title description todoId

        return! command |> Dal.createTodo connectionString
    }

    let completeTodo connectionString todoId : ServiceResultAsync<_> = taskResult {
        let! command = taskResult {
            let! todo = Queries.loadTodo connectionString todoId
            return! todo |> Todo.complete |> Result.mapError DomainError
        }

        return! command |> Dal.completeTodo connectionString
    }

    let deleteTodo connectionString todoId : ServiceResultAsync = taskResult {
        let! command = taskResult {
            let! todo = Queries.loadTodo connectionString todoId
            return! todo |> Todo.delete |> Result.mapError DomainError
        }

        return! command |> Dal.deleteTodo connectionString
    }

    let editTodo connectionString request : ServiceResultAsync = taskResult {
        let! title, description =
            validate {
                let! title = request.Title |> String255.TryCreate "Title"

                and! description =
                    request.Description
                    |> Option.ofObj
                    |> Option.toResultOption (String255.TryCreate "Description")

                return title, description
            }
            |> Result.mapError InvalidRequest

        let! command = taskResult {
            let! todo = Queries.loadTodo connectionString request.Id
            return! todo |> Todo.edit title description |> Result.mapError DomainError
        }

        return! command |> Dal.updateTodo connectionString
    }

    let clearAllTodos (connectionString: string) =
        DbCommands.ClearAllTodos.WithConnection(connectionString).ExecuteAsync()
        |> Task.map ignore