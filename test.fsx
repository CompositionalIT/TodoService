let x : Result<int, string> = Ok 1
let y : Result<int, string> = Ok 2
let z : Result<int, string> = Ok 3


let answer =
    match x with
    | Ok x ->
        match y with
        | Ok y ->
            match z with
            | Ok z ->
                Ok (float (x + y + z))
            | Error z ->
                Error z
        | Error y ->
            Error y
    | Error x ->
        Error x



let maybeANumber = Ok 0
let divide a b =
    if b = 0 then Error "Can't divide by zero"
    else Ok (a / b)

let theRealAnswer : Result<_, string> =
    maybeANumber
    |> Result.bind (divide 10)
    |> Result.map (fun x -> "The answer is: " + x.ToString())
    |> Result.map (fun x -> x.ToUpper())

#r "nuget:FsToolkit.ErrorHandling"

open FsToolkit.ErrorHandling

let theSmartAnswer = result {
    let! maybeANumber = maybeANumber
    printfn $"Unwrapped maybe a number {maybeANumber}"
    let! answer = maybeANumber |> divide 10
    printfn $"Divided: {answer}"
    let x = "The answer is: " + answer.ToString()
    return x.ToUpper()
}

// results
// pipe
// pipe
// pipe
// unwrap, handle errors

