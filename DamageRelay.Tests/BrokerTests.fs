module DamageRelay.Tests.BrokerTests

open System
open System.Collections.Concurrent
open Xunit
open DamageRelay.Domain
open DamageRelay.Messages
open DamageRelay.MessageBroker
open DamageRelay.Tests.Helpers

/// Fixture for the common case
type BrokerTests() =
    [<Literal>]
    let DUPLICATE_EVERY = 0

    let broker = Broker.createInMemory DUPLICATE_EVERY
    let target = UnitId.create "test-unit-1"
    let received = ConcurrentQueue<DamageMessage>()
    let sub = Broker.subscribe broker target received.Enqueue

    interface IDisposable with
        member _.Dispose() = sub.Dispose()

    [<Fact>]
    member _.``publish to a subscribed target delivers the message``() =
        let msg = dealFreshDamage target (damage 30)
        let expected = [| msg |]
        Broker.publish broker target msg

        let actual = received.ToArray()
        Assert.Equal<DamageMessage[]>(expected, actual)

    [<Fact>]
    member _.``publish to an unknown target does not throw``() =
        let unknown = UnitId.create "test-unit-999"
        Broker.publish broker unknown (dealFreshDamage unknown (damage 5))

    [<Fact>]
    member _.``unsubscribe stops further delivery``() =
        let expected = 1
        Broker.publish broker target (dealFreshDamage target (damage 10))
        sub.Dispose()
        Broker.publish broker target (dealFreshDamage target (damage 20))

        Assert.Equal(expected, received.Count)

    [<Fact>]
    member _.``subscribing again for the same UnitId replaces the prior callback``() =
        let secondReceived = ConcurrentQueue<DamageMessage>()
        use _ = Broker.subscribe broker target secondReceived.Enqueue

        Broker.publish broker target (dealFreshDamage target (damage 10))

        let expected = [| 0; 1 |]
        let actual = [| received.Count; secondReceived.Count |]
        Assert.Equal<int array>(expected, actual)

    [<Fact>]
    member _.``duplicateEveryN of 0 disables duplicate delivery``() =
        let expected = 5

        for i in 1..5 do
            Broker.publish broker target (dealFreshDamage target (damage i))

        let actual = received.Count
        Assert.Equal(expected, actual)

/// Fixture for the duplicating-broker case (different `duplicateEveryN`).
type BrokerDuplicationTests() =
    [<Literal>]
    let DUPLICATE_EVERY = 3

    let broker = Broker.createInMemory DUPLICATE_EVERY
    let target = UnitId.create "test-unit-2"
    let received = ConcurrentQueue<DamageMessage>()
    let sub = Broker.subscribe broker target received.Enqueue

    interface IDisposable with
        member _.Dispose() = sub.Dispose()

    [<Fact>]
    member _.``every Nth publish is delivered twice when duplicateEveryN is set``() =
        // Duplicate every 3rd message: counts 3 and 6 deliver twice -> 8 total.
        let expected = 8

        for i in 1..6 do
            Broker.publish broker target (dealFreshDamage target (damage i))

        let actual = received.Count
        Assert.Equal(expected, actual)
