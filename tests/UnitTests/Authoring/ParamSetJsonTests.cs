using Core;
using FluentAssertions;
using Xunit;

namespace UnitTests.Authoring;

// The ParamSet JSON schema guard: a parameter set must be a flat { "name": scalar } map — exactly what the
// cTrader `.cbotset` is built from. Nested objects, arrays, null values, a non-object root, or malformed
// JSON are rejected; an empty object (no overrides) is accepted.
public class ParamSetJsonTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Period\": 14}")]
    [InlineData("{\"Label\": \"trend\", \"Enabled\": true, \"Risk\": 1.5}")]
    public void Accepts_a_flat_scalar_object(string json) =>
        ParamSetJson.IsValidSchema(json).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("42")]
    [InlineData("\"just a string\"")]
    [InlineData("{\"Parameters\": {\"Period\": 14}}")]
    [InlineData("{\"tags\": [1, 2]}")]
    [InlineData("{\"Period\": null}")]
    public void Rejects_anything_that_is_not_a_flat_scalar_object(string? json) =>
        ParamSetJson.IsValidSchema(json).Should().BeFalse();
}
