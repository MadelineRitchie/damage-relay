/// Some types for the toy. I'm going all the way with "make invalid states unrepresentable" as an exercise.
module DamageRelay.Domain

open System

/// Capturing all the different types of validation errors as DU cases centralizes and standardizes errors themselves.
type ValidationError =
    //| SomethingElseIsWrongWithSomething of richValue1: aType * richValue2: bType
    //| ASecretThirdTypeOfValidationError
    | MustBeNonNegative of field: string * actual: int

module ValidationError =
    let toMessage =
        function
        | MustBeNonNegative(field, actual) -> $"%s{field} must be non-negative; got %d{actual}"

/// Unit identifier. Wrapped so it can't be mixed with other string IDs.
type UnitId = private UnitId of string

module UnitId =
    let create value = UnitId value
    let value (UnitId v) = v

/// Current health remaining. Non-negative.
type Health = private Health of int

module Health =
    let create value =
        if value >= 0 then
            Ok(Health value)
        else
            Error(MustBeNonNegative("Health", value))

    let value (Health v) = v

/// Distinct from Health so they can't be swapped.
type MaxHealth = private MaxHealth of int

module MaxHealth =
    let create value =
        if value >= 0 then
            Ok(MaxHealth value)
        else
            Error(MustBeNonNegative("MaxHealth", value))

    let value (MaxHealth v) = v

/// Damage from one event. Zero allowed (e.g. fully resisted).
type DamageAmount = private DamageAmount of int

module DamageAmount =
    let create value =
        if value >= 0 then
            Ok(DamageAmount value)
        else
            Error(MustBeNonNegative("DamageAmount", value))

    let value (DamageAmount v) = v

/// Per-message ID. Consumers use it to skip duplicates.
type MessageId = private MessageId of Guid

module MessageId =
    let newId () = Guid.NewGuid() |> MessageId

    let value (MessageId g) = g
