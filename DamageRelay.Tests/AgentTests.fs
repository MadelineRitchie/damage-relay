module DamageRelay.Tests.AgentTests

open Xunit
open DamageRelay.Domain
open DamageRelay.Messages
open DamageRelay.Agent
open DamageRelay.Tests.Helpers

// Test helpers to cut construction noise out of the assertions below.

let health n =
    match Health.create n with
    | Ok h -> h
    | Error e -> failwith $"test setup: %s{ValidationError.toMessage e}"

let maxHealth n =
    match MaxHealth.create n with
    | Ok m -> m
    | Error e -> failwith $"test setup: %s{ValidationError.toMessage e}"

let alive cur max = Alive(health cur, maxHealth max)

let dealDamage (target: UnitId) (amount: DamageAmount) messageId = DealDamage(target, amount, messageId)

/// Post + read. The read drains the mailbox, so prior posts have been processed by the time it
/// returns.
let postAndWait agent msg =
    UnitAgent.post agent msg
    UnitAgent.getState agent


[<Fact>]
let ``newly created agent reports its initial state`` () =
    let expected = alive 100 100
    let agent = UnitAgent.create expected

    let actual = UnitAgent.getState agent

    Assert.Equal(expected, actual)

[<Fact>]
let ``damage subtracts from current health`` () =
    let expected = alive 70 100
    let unitId = UnitId.create "test-unit-1"
    let amount = damage 30
    let agent = UnitAgent.create (alive 100 100)

    let actual = dealFreshDamage unitId amount |> postAndWait agent

    Assert.Equal(expected, actual)

[<Fact>]
let ``damage exceeding remaining health transitions to Defeated`` () =
    let unitId = UnitId.create "test-unit-1"
    let amount = damage 25
    let agent = UnitAgent.create (alive 10 100)

    let actual = postAndWait agent (dealFreshDamage unitId amount)

    Assert.True actual.IsDefeated

[<Fact>]
let ``damage exactly equal to remaining health transitions to Defeated`` () =
    let unitId = UnitId.create "test-unit-1"
    let amount = damage 10
    let agent = UnitAgent.create (alive 10 100)

    let actual = postAndWait agent (dealFreshDamage unitId amount)

    Assert.True actual.IsDefeated

[<Fact>]
let ``duplicate MessageId is applied at most once`` () =
    let expected = alive 70 100
    let unitId = UnitId.create "test-unit-1"
    let amount = damage 30
    let agent = UnitAgent.create (alive 100 100)
    let id = MessageId.newId ()
    UnitAgent.post agent (dealDamage unitId amount id)

    let actual = postAndWait agent (dealDamage unitId amount id)

    Assert.Equal(expected, actual)

[<Fact>]
let ``distinct MessageIds with same payload both apply`` () =
    let expected = alive 40 100
    let unitId = UnitId.create "test-unit-1"
    let amount = damage 30
    let agent = UnitAgent.create (alive 100 100)
    UnitAgent.post agent (dealFreshDamage unitId amount)

    let actual = postAndWait agent (dealFreshDamage unitId amount)

    Assert.Equal(expected, actual)

[<Fact>]
let ``damage messages after Defeated are ignored but acknowledged`` () =
    let unitId = UnitId.create "test-unit-1"
    let killingBlow = damage 25
    let overkill = damage 100
    let agent = UnitAgent.create (alive 10 100)

    dealFreshDamage unitId killingBlow |> UnitAgent.post agent

    let actual = postAndWait agent (dealFreshDamage unitId overkill)

    match actual with
    | Defeated [ d ] -> Assert.Equal(killingBlow, d)
    | other -> Assert.Fail $"expected single-entry Defeated history, got %A{other}"
