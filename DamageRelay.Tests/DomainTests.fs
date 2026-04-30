module DamageRelay.Tests.DomainTests

open Xunit
open DamageRelay.Domain

[<Fact>]
let ``UnitId wraps any string without validation`` () =
    let expected = ""

    let actual = UnitId.create expected |> UnitId.value

    Assert.Equal(expected, actual)

[<Fact>]
let ``Health.create returns Ok for non-negative input`` () =
    let expected = 100

    match Health.create expected with
    | Ok h ->
        let actual = Health.value h
        Assert.Equal(expected, actual)
    | Error e -> Assert.Fail(ValidationError.toMessage e)

[<Fact>]
let ``Health.create returns MustBeNonNegative for negative input`` () =
    let expected = MustBeNonNegative("Health", -7)

    match Health.create -7 with
    | Ok _ -> Assert.Fail("expected Error")
    | Error actual -> Assert.Equal(expected, actual)

[<Fact>]
let ``MaxHealth.create returns Ok for non-negative input`` () =
    let expected = 100

    match MaxHealth.create expected with
    | Ok m ->
        let actual = MaxHealth.value m
        Assert.Equal(expected, actual)
    | Error e -> Assert.Fail(ValidationError.toMessage e)

[<Fact>]
let ``MaxHealth.create returns MustBeNonNegative for negative input`` () =
    let expected = MustBeNonNegative("MaxHealth", -1)

    match MaxHealth.create -1 with
    | Ok _ -> Assert.Fail("expected Error")
    | Error actual -> Assert.Equal(expected, actual)

[<Fact>]
let ``DamageAmount.create allows zero`` () =
    let expected = 0

    match DamageAmount.create expected with
    | Ok d ->
        let actual = DamageAmount.value d
        Assert.Equal(expected, actual)
    | Error e -> Assert.Fail(ValidationError.toMessage e)

[<Fact>]
let ``DamageAmount.create returns MustBeNonNegative for negative input`` () =
    let expected = MustBeNonNegative("DamageAmount", -5)

    match DamageAmount.create -5 with
    | Ok _ -> Assert.Fail("expected Error")
    | Error actual -> Assert.Equal(expected, actual)
