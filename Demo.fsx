// Live battle demo. Run with:  dotnet fsi Demo.fsx
//
// Two units (Boss, Minion) take a fixed sequence of damage events from
// a single attacker. The same battle runs twice:
//   Run 1: clean broker.
//   Run 2: broker that delivers every 2nd message twice.
// Final states are identical because the agents dedupe by MessageId.

#load "DamageRelay/Domain.fs"
#load "DamageRelay/Messages.fs"
#load "DamageRelay/Agent.fs"
#load "DamageRelay/MessageBroker.fs"

open DamageRelay.Domain
open DamageRelay.Messages
open DamageRelay.Agent
open DamageRelay.MessageBroker

// --- helpers -------------------------------------------------------------

let unwrap =
    function
    | Ok x -> x
    | Error e -> e |> ValidationError.toMessage |> failwith

let health n = Health.create n |> unwrap

let maxHealth n = MaxHealth.create n |> unwrap

let damage n = DamageAmount.create n |> unwrap

let alive cur max = Alive(health cur, maxHealth max)

let renderState id name state =
    let n = UnitId.value id

    match state with
    | Alive(h, m) -> $"%s{name} (#%s{n}): Alive %d{Health.value h}/%d{MaxHealth.value m}"
    | Defeated dmg ->
        let killed = dmg |> List.map DamageAmount.value

        $"%s{name} (#%s{n}): Defeated (killing blow(s): %A{killed})"

let snapshot label agentId agentName agent =
    let state = UnitAgent.getState agent

    printfn $"  %s{label} %s{renderState agentId agentName state}"
    state

let pause msg =
    printfn ""
    printfn $"    [press Enter to %s{msg}]"

    System.Console.ReadLine() |> ignore

    printfn ""

// --- the script ----------------------------------------------------------

let rng = System.Random.Shared

let pick (xs: _ list) = xs[rng.Next(xs.Length)]

/// 8 random lowercase letters so it looks kinda like a hash
let randomId () =
    System.String([| for _ in 1..8 -> char (int 'a' + rng.Next(26)) |])

let bossName = pick [ "Boss"; "Elite"; "Dragon" ]
let minionName = pick [ "Minion"; "Drone"; "Slime" ]

let bossId = randomId () |> UnitId.create

let minionId = randomId () |> UnitId.create

let bossHp = rng.Next(80, 432)
let minionHp = rng.Next(20, 51)

type AttackEvent =
    { Target: UnitId
      Name: string
      Amount: DamageAmount
      MessageId: MessageId }

type AttackGenState =
    { Index: int
      MinionRemaining: int
      Killed: bool }

/// A sequence of attacks for the demo. Once the minion is killed, remaining attacks go to the boss. Same list used by
/// both runs.
let scriptedAttacks =
    let maxAttacks = 20

    let step (state: AttackGenState) =
        if state.Index >= maxAttacks then
            None
        else
            let target, name =
                // while the minion is alive, half the attacks go to the minion
                if state.Killed || rng.Next(2) = 0 then
                    bossId, bossName
                else
                    minionId, minionName

            let amount = rng.Next(5, 21) // 5–20 damage

            let minionRemaining =
                if target = minionId then
                    state.MinionRemaining - amount
                else
                    state.MinionRemaining

            let event =
                { Target = target
                  Name = name
                  Amount = damage amount
                  MessageId = MessageId.newId () }

            let next =
                { Index = state.Index + 1
                  MinionRemaining = minionRemaining
                  Killed = state.Killed || minionRemaining <= 0 }

            Some(event, next)

    { Index = 0
      MinionRemaining = minionHp
      Killed = false }
    |> List.unfold step

let runBattle label duplicateEveryN =
    printfn ""
    printfn $"=== %s{label} ==="

    let boss = UnitAgent.create (alive bossHp bossHp)
    let minion = UnitAgent.create (alive minionHp minionHp)

    let broker = Broker.createInMemory duplicateEveryN
    let subscribe = Broker.subscribe broker
    let publish = Broker.publish broker

    // Receiver-side: count deliveries and notice duplicates.
    let mutable deliveries = 0
    let seen = System.Collections.Generic.HashSet<MessageId>()

    let countingPost name agent (DealDamage(_, _, msgId) as msg) =
        deliveries <- deliveries + 1

        if not (seen.Add msgId) then
            printfn $"        ↳ %s{name} notices a redelivery"

        UnitAgent.post agent msg

    use _ = subscribe bossId (countingPost bossName boss)
    use _ = subscribe minionId (countingPost minionName minion)

    snapshot "initial: " bossId bossName boss |> ignore

    snapshot "initial: " minionId minionName minion |> ignore

    printfn ""

    scriptedAttacks
    |> List.iteri (fun i attackEvent ->
        printfn
            $"  attack #%d{i + 1}: %d{DamageAmount.value attackEvent.Amount} damage -> %s{attackEvent.Name} (#%s{UnitId.value attackEvent.Target})"

        (attackEvent.Target, attackEvent.Amount, attackEvent.MessageId)
        |> DealDamage
        |> publish attackEvent.Target)

    printfn ""
    printfn $"  sent: %d{scriptedAttacks.Length}, delivered: %d{deliveries}"
    let h = snapshot "final:   " bossId bossName boss
    let g = snapshot "final:   " minionId minionName minion
    h, g

let cleanBoss, cleanMinion = runBattle "Run 1: reliable broker" 0

pause "rerun the same battle with an at-least-once broker"

let dupBoss, dupMinion = runBattle "Run 2: at-least-once broker" 2

printfn ""
printfn "=== comparison ==="
printfn $"  %-6s{bossName} identical across runs: %b{cleanBoss = dupBoss}"
printfn $"  %-6s{minionName} identical across runs: %b{cleanMinion = dupMinion}"
