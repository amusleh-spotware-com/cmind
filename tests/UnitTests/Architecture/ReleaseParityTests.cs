using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace UnitTests.Architecture;

// Census gate (CLAUDE.md mandate #12) for the release pipeline: the set of shipped components and the
// single product version must stay in lockstep across the root Dockerfiles, the release workflow's image
// matrix, and the Helm chart. Add a service (a new root Dockerfile.<x>) or bump the version in one place
// and forget another, and this fails the build instead of shipping a half-wired release.
public sealed class ReleaseParityTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    // A component that is built as a release image but is not deployed by the chart. Keep empty; add with
    // a reason if a Dockerfile is ever image-only (e.g. a CI helper) so the ratchet stays honest.
    private static readonly Dictionary<string, string> HelmExcludedComponents =
        new(StringComparer.Ordinal);

    [Fact]
    public void Root_dockerfiles_release_matrix_and_helm_images_are_in_sync()
    {
        var dockerComponents = DockerfileComponents();
        var matrixComponents = ReleaseMatrixComponents();
        var helmComponents = HelmImageComponents();

        dockerComponents.Should().NotBeEmpty("expected root Dockerfile.<component> files to exist");

        matrixComponents.Should().BeEquivalentTo(dockerComponents,
            "every root Dockerfile.<component> must be built by the release workflow image matrix, and vice versa — see .github/workflows/release.yml");

        var expectedHelm = dockerComponents.Where(c => !HelmExcludedComponents.ContainsKey(c)).ToHashSet();
        helmComponents.Should().BeEquivalentTo(expectedHelm,
            "every deployed component image must map to a built Dockerfile.<component> (or be listed in HelmExcludedComponents) — see deploy/helm/cmind/templates");
    }

    [Fact]
    public void Chart_appVersion_tracks_the_product_version()
    {
        var versionPrefix = ReadFirst(
            Path.Combine(RepoRoot, "Directory.Build.props"),
            new Regex(@"<VersionPrefix>([^<]+)</VersionPrefix>"));
        var appVersion = ReadFirst(
            Path.Combine(RepoRoot, "deploy", "helm", "cmind", "Chart.yaml"),
            new Regex("""appVersion:\s*"?([^"\s]+)"?"""));

        appVersion.Should().Be(versionPrefix,
            "Helm Chart appVersion must equal Directory.Build.props VersionPrefix so a source-tree `helm install` pulls the matching image tag (the release workflow stamps both from the git tag)");
    }

    [Fact]
    public void Helm_image_repository_matches_the_release_registry_owner()
    {
        var repository = ReadFirst(
            Path.Combine(RepoRoot, "deploy", "helm", "cmind", "values.yaml"),
            new Regex(@"repository:\s*(\S+)"));

        repository.Should().Be("amusleh-spotware-com/cmind",
            "the chart's default image.repository must be the GHCR owner/repo the release workflow pushes to (ghcr.io/${{ github.repository }})");
    }

    private static HashSet<string> DockerfileComponents() =>
        Directory.EnumerateFiles(RepoRoot, "Dockerfile.*", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetFileName(f)["Dockerfile.".Length..])
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ReleaseMatrixComponents()
    {
        var workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "release.yml"));
        return Matches(workflow, new Regex(@"-\s*component:\s*([a-z0-9-]+)"));
    }

    private static HashSet<string> HelmImageComponents()
    {
        var templates = Path.Combine(RepoRoot, "deploy", "helm", "cmind", "templates");
        var pattern = new Regex("""cmind\.image"\s*\.\s*}}-([a-z0-9-]+):""");
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(templates, "*.yaml", SearchOption.AllDirectories))
        {
            foreach (var component in Matches(File.ReadAllText(file), pattern))
            {
                found.Add(component);
            }
        }

        return found;
    }

    private static HashSet<string> Matches(string text, Regex pattern) =>
        pattern.Matches(text).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    private static string ReadFirst(string path, Regex pattern)
    {
        var match = pattern.Match(File.ReadAllText(path));
        match.Success.Should().BeTrue("expected '{0}' to match in {1}", pattern, path);
        return match.Groups[1].Value.Trim();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "cmind.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate repo root (cmind.slnx) from test output.");
    }
}
