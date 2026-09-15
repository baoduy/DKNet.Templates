using System.Xml.Linq;
using Minimal.App.Tests.Architecture.TemplateRepo.Guards;

namespace Minimal.App.Tests.Architecture.TemplateRepo;

/// <summary>DRK-1271 R1/R2: synthetic csproj/sln scenarios for <see cref="CiSolutionGuard"/>.</summary>
public class CiSolutionGuardTests
{
    private static XDocument CsprojWithPackages(params string[] packageIncludes) => XDocument.Parse($"""
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            {string.Join("\n", packageIncludes.Select(p => $"""<PackageReference Include="{p}" />"""))}
          </ItemGroup>
        </Project>
        """);

    [Fact]
    public void CsprojReferencingTestSdk_IsATestProject()
    {
        var csproj = CsprojWithPackages("Microsoft.NET.Test.Sdk", "xunit");

        CiSolutionGuard.IsTestProject(csproj).ShouldBeTrue();
    }

    [Fact]
    public void CsprojWithNoTestSdkReference_IsNotATestProject()
    {
        // Shaped like Minimal.App.TestSupport: test-support helpers, no Microsoft.NET.Test.Sdk.
        var csproj = CsprojWithPackages("Microsoft.AspNetCore.Mvc.Testing", "Microsoft.EntityFrameworkCore.InMemory");

        CiSolutionGuard.IsTestProject(csproj).ShouldBeFalse();
    }

    [Fact]
    public void TestProjectsMissingFromSolution_ReportsOnlyThePathAbsentFromTheSolutionText()
    {
        const string solutionContent = """
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Minimal.App.Tests", "src/ApiEndpoints/Minimal.App.Tests/Minimal.App.Tests.csproj", "{GUID}"
            EndProject
            """;

        var offenders = CiSolutionGuard.TestProjectsMissingFromSolution(
            [
                "src/ApiEndpoints/Minimal.App.Tests/Minimal.App.Tests.csproj",
                "tests/DKNet.Templates.ScaffoldTests/DKNet.Templates.ScaffoldTests.csproj",
            ],
            solutionContent);

        offenders.ShouldBe(["tests/DKNet.Templates.ScaffoldTests/DKNet.Templates.ScaffoldTests.csproj"]);
    }

    [Fact]
    public void TestProjectsMissingFromSolution_IsEmptyWhenEveryPathIsPresent()
    {
        const string solutionContent = """
            "src/ApiEndpoints/Minimal.App.Tests/Minimal.App.Tests.csproj"
            "tests/DKNet.Templates.ScaffoldTests/DKNet.Templates.ScaffoldTests.csproj"
            """;

        var offenders = CiSolutionGuard.TestProjectsMissingFromSolution(
            [
                "src/ApiEndpoints/Minimal.App.Tests/Minimal.App.Tests.csproj",
                "tests/DKNet.Templates.ScaffoldTests/DKNet.Templates.ScaffoldTests.csproj",
            ],
            solutionContent);

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void ProjectPathsEscapingSolutionDirectory_FindsATraversingProjectPath()
    {
        const string slnContent = """
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Minimal.Api", "ApiEndpoints\Minimal.Api\Minimal.Api.csproj", "{GUID1}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ScaffoldTests", "..\tests\DKNet.Templates.ScaffoldTests\DKNet.Templates.ScaffoldTests.csproj", "{GUID2}"
            EndProject
            """;

        var offenders = CiSolutionGuard.ProjectPathsEscapingSolutionDirectory(slnContent);

        offenders.ShouldBe(["..\\tests\\DKNet.Templates.ScaffoldTests\\DKNet.Templates.ScaffoldTests.csproj"]);
    }

    [Fact]
    public void ProjectPathsEscapingSolutionDirectory_IsEmptyWhenEveryPathStaysInside()
    {
        const string slnContent = """
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Minimal.Api", "ApiEndpoints\Minimal.Api\Minimal.Api.csproj", "{GUID1}"
            EndProject
            """;

        CiSolutionGuard.ProjectPathsEscapingSolutionDirectory(slnContent).ShouldBeEmpty();
    }
}
