/// Contains helpful active patterns for handling SQL exceptions.
module Sql

open Microsoft.Data.SqlClient
open System
open System.Text.RegularExpressions

/// An active pattern which checks how many rows were affected by a DB operation.
let (|NoRowsAffected|SingleRowAffected|MultipleRowsAffected|) =
    function
    | 0 -> NoRowsAffected
    | 1 -> SingleRowAffected
    | n -> MultipleRowsAffected n

let ofRowsModified onNone rowsModified =
    match rowsModified with
    | NoRowsAffected -> Error(DataNotFound onNone)
    | SingleRowAffected -> Ok()
    | MultipleRowsAffected _ -> Error(GenericError $"Too many rows modified ({rowsModified})")

let (|PrimaryAggregationException|_|) (ex: exn) =
    match ex with
    | :? AggregateException as ex -> Some(PrimaryAggregationException ex.InnerExceptions[0])
    | _ -> None

let (|SqlException|_|) (ex: exn) =
    match ex with
    | :? SqlException as ex -> Some(ex.Data["HelpLink.EvtID"] :?> string |> int, ex.Message)
    | _ -> None

let (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)

    if m.Success then
        Some(m.Groups |> Seq.map _.Value |> Seq.toList)
    else
        None

let (|UniqueConstraint|_|) =
    function
    | SqlException(2627, Regex @"constraint '([\w\.]+)'.*object '([\w\.]+)" [ _; uniqueConstraint; table ]) ->
        Some(UniqueConstraint(table, uniqueConstraint))
    | _ -> None

let (|UniqueIndexConstraint|_|) =
    function
    | SqlException(2601, msg) ->
        let words = msg.Split([| " "; "\r\n" |], StringSplitOptions.RemoveEmptyEntries)
        let constraintName = words[11][1 .. (words[11].Length - 3)]
        Some(UniqueIndexConstraint constraintName)
    | _ -> None

let (|PrimaryKeyViolation|_|) =
    function
    | SqlException(2627, msg) when msg.StartsWith "Violation of PRIMARY KEY constraint" ->
        let table = (msg.Split ' ')[12]
        Some(PrimaryKeyViolation table[1 .. table.Length - 3])
    | _ -> None

let (|NullColumnInsertion|_|) =
    function
    | SqlException(515, msg) ->
        let column = msg.Split(' ')[7] |> fun column -> column[1 .. (column.Length - 3)]

        let table =
            msg.Split(' ').[9].Split('.')
            |> Seq.last
            |> fun table -> table[.. (table.Length - 3)]

        Some(NullColumnInsertion(table, column))
    | _ -> None