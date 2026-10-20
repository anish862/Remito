using FluentAssertions;
using NetArchTest.Rules;

namespace Remito.UnitTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_must_not_depend_on_other_layers()
    {
        var domain = typeof(Remito.Domain.AssemblyMarker).Assembly;

        Types.InAssembly(domain)
            .ShouldNot()
            .HaveDependencyOnAny("Remito.Application", "Remito.Infrastructure", "Remito.Api", "Remito.Workers")
            .GetResult()
            .IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_must_not_depend_on_outer_layers()
    {
        var application = typeof(Remito.Application.AssemblyMarker).Assembly;

        Types.InAssembly(application)
            .ShouldNot()
            .HaveDependencyOnAny("Remito.Infrastructure", "Remito.Api", "Remito.Workers")
            .GetResult()
            .IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Infrastructure_must_not_depend_on_hosts()
    {
        var infrastructure = typeof(Remito.Infrastructure.AssemblyMarker).Assembly;

        Types.InAssembly(infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny("Remito.Api", "Remito.Workers")
            .GetResult()
            .IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Infrastructure_must_depend_on_application_contracts()
    {
        var infrastructure = typeof(Remito.Infrastructure.AssemblyMarker).Assembly;

        infrastructure.GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Should().Contain("Remito.Application");
    }

    [Fact]
    public void Api_must_not_depend_on_workers()
    {
        var api = typeof(global::Program).Assembly;

        Types.InAssembly(api)
            .ShouldNot()
            .HaveDependencyOn("Remito.Workers")
            .GetResult()
            .IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Api_must_depend_on_application()
    {
        var api = typeof(global::Program).Assembly;

        api.GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Should().Contain("Remito.Application");
    }
}
