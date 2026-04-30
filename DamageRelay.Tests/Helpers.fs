module DamageRelay.Tests.Helpers

open DamageRelay.Domain
open DamageRelay.Messages

let damage n =
    match DamageAmount.create n with
    | Ok d -> d
    | Error e -> failwith $"test setup: %s{ValidationError.toMessage e}"

let dealFreshDamage (target: UnitId) (amount: DamageAmount) =
    DealDamage(target, amount, MessageId.newId ())
