using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Authoring;

// Invariants for the CBotSourceProject language hierarchy: create (name guard), file storage, build
// recording, and the language/extension identity. (WS-1 Core backfill.)
public class CBotSourceProjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CSharp_project_reports_language_and_extension()
    {
        var project = CSharpProject.Create(UserId.New(), "MyBot");

        project.Name.Should().Be("MyBot");
        project.LanguageName.Should().Be("CSharp");
        project.FileExtension.Should().Be(".cs");
        project.EncryptedProjectFiles.Should().BeEmpty();
    }

    [Fact]
    public void Python_project_reports_language_and_extension()
    {
        var project = PythonProject.Create(UserId.New(), "MyBot");

        project.LanguageName.Should().Be("Python");
        project.FileExtension.Should().Be(".py");
    }

    [Fact]
    public void Create_rejects_a_blank_name()
    {
        var act = () => CSharpProject.Create(UserId.New(), "  ");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.NameRequired);
    }

    [Fact]
    public void Set_files_and_record_build_persist_state()
    {
        var project = PythonProject.Create(UserId.New(), "MyBot");
        byte[] files = [1, 2, 3];

        project.SetFiles(files);
        project.EncryptedProjectFiles.Should().BeEquivalentTo(files);

        project.RecordBuild("build ok", succeeded: true, Now);
        project.LastBuildLog.Should().Be("build ok");
        project.LastBuildSucceeded.Should().BeTrue();
        project.LastBuildAt.Should().Be(Now);
    }
}
