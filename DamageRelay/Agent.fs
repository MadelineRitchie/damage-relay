// MailboxProcessor agent with idempotent message handling.
module DamageRelay.Agent

open DamageRelay.Domain
open DamageRelay.Messages

/// Mailbox envelope. Queries and damage share one queue so reads see consistent state. Private to
/// keep `DamageMessage` query-free.
type private AgentCommand =
    | Damage of DamageMessage
    | Query of AsyncReplyChannel<UnitState>

/// Opaque handle. Forces callers through the module functions instead of the raw mailbox.
type UnitAgent = private UnitAgent of MailboxProcessor<AgentCommand>

module UnitAgent =

    /// Pure, but not directly tested. Tested through the mailbox plumbing. Also, the Error case is just for show.
    let private applyDamage (amount: DamageAmount) (state: UnitState) : UnitState =
        match state with
        | Alive(health, maxHealth) ->
            let remaining = Health.value health - DamageAmount.value amount

            if remaining <= 0 then
                // Killing blow seeds the history (newest-first).
                Defeated [ amount ]
            else
                // `Health.create` cannot fail here: `remaining > 0`.
                match Health.create remaining with
                | Ok h -> Alive(h, maxHealth)
                | Error _ -> state // unreachable
        | Defeated _ -> state // If we wanted to record overkill damage, we'd prepend the incoming damage here.

    let create (initialState: UnitState) : UnitAgent =
        let rec loop (inbox: MailboxProcessor<AgentCommand>) (state: UnitState) (seen: Set<MessageId>) =
            async {
                let! cmd = inbox.Receive()

                match cmd with
                | Query reply ->
                    reply.Reply(state)
                    return! loop inbox state seen
                | Damage(DealDamage(_target, amount, id)) ->
                    if Set.contains id seen then
                        // Duplicate; ignore.
                        return! loop inbox state seen
                    else
                        let state' = applyDamage amount state
                        return! loop inbox state' (Set.add id seen)
            }

        // I could try collapsing this lambda with partial application.
        MailboxProcessor.Start(fun inbox -> loop inbox initialState Set.empty)
        |> UnitAgent

    let post (UnitAgent mb) (msg: DamageMessage) : unit = mb.Post(Damage msg)

    let getState (UnitAgent mb) : UnitState = mb.PostAndReply(Query)
