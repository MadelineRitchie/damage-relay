# damage-relay

F# message-passing toy. Game units are `MailboxProcessor` agents; an
in-memory broker routes damage events between them. Agents dedupe on
`MessageId` so at-least-once delivery is safe.

```sh
dotnet test DamageRelay.slnx
dotnet fsi Demo.fsx
```

The demo runs the same battle twice — second pass through a broker
that duplicates every other message. Final states match.

.NET 10, F# 10, xUnit.
