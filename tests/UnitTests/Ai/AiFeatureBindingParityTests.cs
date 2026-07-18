using System.Threading.Tasks;
using Core.Ai;
using FluentAssertions;
using Xunit;

namespace UnitTests.Ai;

// Census gate: the AiFeature enum and IAiFeatureService must stay in lock-step so every AI operation is
// bindable to a model and nothing is silently un-routable. Each IAiFeatureService method (returning
// Task<AiResult>) must have a matching AiFeature value named after the method minus its "Async" suffix, and
// every AiFeature value must have a method. Adding a feature method without its enum value (or vice versa)
// fails the build here.
public sealed class AiFeatureBindingParityTests
{
    private static IReadOnlyList<string> FeatureMethodNames() =>
        typeof(IAiFeatureService).GetMethods()
            .Where(m => m.ReturnType == typeof(Task<AiResult>))
            .Select(m => m.Name.EndsWith("Async", System.StringComparison.Ordinal) ? m.Name[..^"Async".Length] : m.Name)
            .ToList();

    [Fact]
    public void Every_feature_service_method_has_a_matching_AiFeature_value()
    {
        foreach (var name in FeatureMethodNames())
            Enum.IsDefined(typeof(AiFeature), name).Should()
                .BeTrue($"IAiFeatureService.{name}Async needs an AiFeature.{name} value");
    }

    [Fact]
    public void Every_AiFeature_value_has_a_matching_feature_service_method()
    {
        var methods = FeatureMethodNames();
        foreach (var feature in Enum.GetValues<AiFeature>())
            methods.Should().Contain(feature.ToString(),
                $"AiFeature.{feature} needs an IAiFeatureService.{feature}Async method");
    }
}
