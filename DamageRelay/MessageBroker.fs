// In-process router. Not a real distributed queue but interesting for exploring real message bus
// scenarios.
module DamageRelay.MessageBroker

open System
open System.Collections.Generic
open DamageRelay.Domain
open DamageRelay.Messages

/// Operations carried. Just data.
type BrokerOp = DamageUnit of target: UnitId * message: DamageMessage

/// Internal mailbox envelope. Both ops carry reply channels so callers know when the broker has
/// finished dispatching.
type private BrokerCommand =
    | Op of BrokerOp * AsyncReplyChannel<unit>
    | Subscribe of UnitId * (DamageMessage -> unit) * AsyncReplyChannel<IDisposable>

/// Opaque handle. Forces callers through the module functions.
type Broker = private Broker of MailboxProcessor<BrokerCommand>

module Broker =

    /// `duplicateEveryN`: every Nth `DamageUnit` is delivered twice.
    let createInMemory (duplicateEveryN: int) : Broker =
        let subscribers = Dictionary<UnitId, DamageMessage -> unit>()
        let mutable deliveryCount = 0

        let shouldDuplicate () =
            duplicateEveryN > 0 && deliveryCount % duplicateEveryN = 0

        let deliver target message =
            match subscribers.TryGetValue target with
            | true, callback -> callback message
            | false, _ -> () // Unknown target -> drop. A real queue would treat dead letters appropriately.

        let rec loop (inbox: MailboxProcessor<BrokerCommand>) =
            async {
                let! cmd = inbox.Receive()

                match cmd with
                | Op(DamageUnit(target, message), reply) ->
                    deliveryCount <- deliveryCount + 1

                    deliver target message

                    if shouldDuplicate () then
                        deliver target message

                    reply.Reply()
                | Subscribe(target, callback, reply) ->
                    subscribers[target] <- callback

                    let unsubscribe =
                        { new IDisposable with
                            member _.Dispose() = subscribers.Remove(target) |> ignore }

                    reply.Reply(unsubscribe)

                return! loop inbox
            }

        MailboxProcessor.Start loop |> Broker

    /// Every `BrokerOp` flows through here. Blocks until the op has been dispatched (so callbacks have run).
    let send (Broker mb) (op: BrokerOp) : unit =
        mb.PostAndReply(fun reply -> Op(op, reply))

    /// Convenience for the most common case.
    let publish broker target message = send broker (DamageUnit(target, message))

    /// Register a callback for messages to `target`
    let subscribe (Broker mb) (target: UnitId) (callback: DamageMessage -> unit) : IDisposable =
        // Would use async here in a real system, but not until a real async consumer was involved
        mb.PostAndReply(fun reply -> Subscribe(target, callback, reply))
