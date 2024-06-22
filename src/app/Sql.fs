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

module Regexes =
    [<Literal>]
    let uniqueIndex =
        @"Cannot insert duplicate key row in object '([^']+)' with unique index '([^']+)'"

    [<Literal>]
    let constraintViolation =
        @"Violation of ([A-Z\s]+) constraint '([^']+)'. Cannot insert duplicate key in object '([^']+)'"

    [<Literal>]
    let nullColumn =
        @"Cannot insert the value NULL into column '([^']+)', table '([^']+)'[^']*column does not allow nulls."

/// A simple Regex active pattern.
let (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)

    if m.Success then
        Some(m.Groups |> Seq.map _.Value |> Seq.toList)
    else
        None

let (|PrimaryAggregationException|_|) (ex: exn) =
    match ex with
    | :? AggregateException as ex -> Some(PrimaryAggregationException ex.InnerExceptions[0])
    | _ -> None

/// An active pattern which matches a SQLException and extracts both the HelpLink.EvtID and exception Message.
let (|SqlException|_|) (ex: exn) =
    match ex with
    | :? SqlException as ex -> Some(ex.Data["HelpLink.EvtID"] :?> string |> int, ex.Message)
    | _ -> None

/// A SQLException active pattern which matches a unique index constraint violation (2601) and extracts the object and index.
let (|UniqueIndexConstraint|_|) =
    function
    | SqlException(2601, Regex Regexes.uniqueIndex [ _; object; index ]) -> Some(UniqueIndexConstraint(object, index))
    | _ -> None

/// A SQLException active pattern which matches a generic constraint violation (2627) and extracts the constraint type, name, and table.
let (|Constraint|_|) =
    function
    | SqlException(2627, Regex Regexes.constraintViolation [ _; constraintType; constraintName; table ]) ->
        Some(Constraint(constraintType, constraintName, table))
    | _ -> None

/// A Constraint active pattern which matches a unique key constraint and extracts the constraint name and table.
let (|UniqueConstraint|_|) =
    function
    | Constraint("UNIQUE KEY", constraintName, table) -> Some(UniqueConstraint(constraintName, table))
    | _ -> None

/// A Constraint active pattern which matches a primary key constraint and extracts the constraint name and table.
let (|PrimaryKeyConstraint|_|) =
    function
    | Constraint("PRIMARY KEY", _, table) -> Some(PrimaryKeyConstraint table)
    | _ -> None

/// A SQLException active pattern which matches a null column insertion violation (515) and extracts the column and table.
let (|NullColumnInsertion|_|) =
    function
    | SqlException(515, Regex Regexes.nullColumn [ _; column; table ]) -> Some(NullColumnInsertion(table, column))
    | _ -> None