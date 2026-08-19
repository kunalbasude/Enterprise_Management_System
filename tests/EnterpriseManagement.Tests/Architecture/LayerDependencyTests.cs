using System.Reflection;

namespace EnterpriseManagement.Tests.Architecture;

/// <summary>
/// Executable guards for the Clean Architecture dependency rule.
///
/// Project references already make an illegal <c>using</c> fail to compile, but a
/// reference can be added by accident (or by an IDE quick-fix) and nobody notices
/// in review. These tests fail the build when that happens, and name the offender.
/// </summary>
public class LayerDependencyTests
{
    private const string ApplicationAssembly = "EnterpriseManagement.Application";
    private const string InfrastructureAssembly = "EnterpriseManagement.Infrastructure";
    private const string ApiAssembly = "EnterpriseManagement.Api";

    private static string[] ReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

    [Theory]
    [InlineData(ApplicationAssembly)]
    [InlineData(InfrastructureAssembly)]
    [InlineData(ApiAssembly)]
    public void Domain_should_not_reference_outer_layers(string forbidden)
    {
        var references = ReferencesOf(Domain.AssemblyReference.Assembly);

        Assert.DoesNotContain(forbidden, references);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Npgsql")]
    public void Domain_should_not_reference_persistence_or_web_frameworks(string forbidden)
    {
        // The domain model must be expressible without EF Core or HTTP. If this
        // fails, a persistence concern has leaked into the innermost layer.
        var references = ReferencesOf(Domain.AssemblyReference.Assembly);

        Assert.DoesNotContain(references, r => r.StartsWith(forbidden, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(InfrastructureAssembly)]
    [InlineData(ApiAssembly)]
    public void Application_should_not_reference_outer_layers(string forbidden)
    {
        // Application declares interfaces; Infrastructure implements them.
        // A reference in this direction would invert the dependency rule.
        var references = ReferencesOf(Application.AssemblyReference.Assembly);

        Assert.DoesNotContain(forbidden, references);
    }

    // NOTE: there is deliberately no "Application_should_reference_Domain" test.
    // The C# compiler omits assembly references that are never used from the
    // emitted metadata, so GetReferencedAssemblies() can prove a dependency
    // EXISTS but never that a declared one is missing. Asserting the positive
    // would fail spuriously whenever a layer happens not to use another yet.

    [Fact]
    public void Infrastructure_should_not_reference_Api()
    {
        var references = ReferencesOf(Infrastructure.AssemblyReference.Assembly);

        Assert.DoesNotContain(ApiAssembly, references);
    }
}
