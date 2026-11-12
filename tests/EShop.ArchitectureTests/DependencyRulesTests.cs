using System.Reflection;

namespace EShop.ArchitectureTests;

public sealed class DependencyRulesTests
{
    private static readonly Assembly Domain = Assembly.Load("EShop.Domain");
    private static readonly Assembly Infrastructure = Assembly.Load("EShop.Infrastructure");

    [Fact]
    public void Domain_does_not_depend_on_other_layers_or_frameworks()
    {
        var found = Forbidden(
            Domain,
            "EShop.Api",
            "EShop.Infrastructure",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore");

        Assert.Empty(found);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        var found = Forbidden(Infrastructure, "EShop.Api");

        Assert.Empty(found);
    }

    [Fact]
    public void Infrastructure_does_not_use_the_web_pipeline()
    {
        var found = Forbidden(
            Infrastructure,
            "Microsoft.AspNetCore.Http",
            "Microsoft.AspNetCore.Mvc",
            "Microsoft.AspNetCore.Routing");

        Assert.Empty(found);
    }

    [Fact]
    public void Infrastructure_depends_on_Domain()
    {
        Assert.Contains("EShop.Domain", References(Infrastructure));
    }

    private static string[] References(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty)];

    private static string[] Forbidden(Assembly assembly, params string[] prefixes) =>
        [.. References(assembly).Where(name =>
            prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))];
}