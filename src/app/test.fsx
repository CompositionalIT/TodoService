let customerIds = [ 2..5..100 ]

let loadCustomer (cId: int) : string option =
    match cId with
    | 2
    | 7
    | 12 -> Some "Customer!"
    | _ -> None

let customers =
    customerIds
    |> List.choose (fun customerId -> loadCustomer customerId)

(*

HTTP JSON -> F# -> SQL

DTO 1 -> Domain -> SQL -> Domain -> DTO 2

// Mapping from e.g. SQL to F#
// Too much mapping!
// Mapping into entire domain model but only needs a small part of it?

Command Query Reponsibility Segregation


*)

open System

type CreateOrderRequest = // JSON 1:1
    {
        CustomerId: string // ABC-123
        ProductNumber: int
        Quantity: int
        PromotionCode: Nullable<int>
    } // optional for domain

// 1. ID Pattern
// 2. Quantity = must be at least 1. less than 10.
// 3. ProductNumber = ?? SQL Only?

type CustomerDivision = CustomerDivision of string
type CustomerNumber = CustomerNumber of string

// ABC-123
type CustomerIdValidationError =
    | CustomerIdTooShort
    | MissingCustomerDivision
    | InvalidCustomerNumber

type CustomerId =
    private
    | CustomerId of string
    static member Create(customerId: string) =
        if customerId.Length <> 7 then
            Error CustomerIdTooShort
        else
            Ok(CustomerId customerId)

    member this.CustomerDivision =
        let (CustomerId v) = this
        CustomerDivision v.[0..2]

    member this.CustomerNumber =
        let (CustomerId v) = this
        CustomerNumber v.[4..6]

    member this.Value =
        let (CustomerId v) = this
        v

let c = CustomerId.Create "ABC-1253232"
type ProductNumber = ProductNumber of int

type QuantityValidationError =
    | QuantityTooSmall of int
    | QuantityTooLarge of int

type Quantity =
    | Quantity of int
    static member Create(qty: int) =
        if qty < 1 then
            Error(QuantityTooSmall qty)
        elif qty > 10 then
            Error(QuantityTooLarge qty)
        else
            Ok(Quantity qty)

type PromoCode = PromoCode of int

type OrderDomainObjectThing =
    {
        CustomerId: CustomerId // ABC-123
        ProductNumber: ProductNumber
        Quantity: Quantity
        PromotionCode: PromoCode option
        OrderDate: DateTime
    }

type OrderValidator = CreateOrderRequest -> OrderDomainObjectThing

type CreateOrderValidationError =
    | CustomerIdValidationError of CustomerIdValidationError
    | QuantityValidationError of QuantityValidationError

#r "nuget:FSToolkit.ErrorHandling"

open FsToolkit.ErrorHandling

let validator (request: CreateOrderRequest) =
    result {
        let! customerId =
            CustomerId.Create request.CustomerId
            |> Result.mapError CustomerIdValidationError

        let! quantity =
            Quantity.Create request.Quantity
            |> Result.mapError QuantityValidationError

        return
            {
                CustomerId = customerId
                ProductNumber = ProductNumber request.ProductNumber
                Quantity = quantity
                PromotionCode =
                    request.PromotionCode
                    |> Option.ofNullable
                    |> Option.map PromoCode
                OrderDate = DateTime.UtcNow
            }
    }

let saveToDatabase (r: CreateOrderRequest) = ()
