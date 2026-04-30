/// Multi-case DUs for protocol and state.
module DamageRelay.Messages

open DamageRelay.Domain

/// Broker wire protocol.
type DamageMessage =
    //| FireDamage of something // for example
    //| LightningDamage of something // for example
    | DealDamage of target: UnitId * amount: DamageAmount * id: MessageId

/// Never mutating, it represents a moment in a unit's existence.
type UnitState =
    | Alive of health: Health * maxHealth: MaxHealth
    | Defeated of damageHistory: DamageAmount list
