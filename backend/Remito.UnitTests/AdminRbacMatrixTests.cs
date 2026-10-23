using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Remito.Api.Controllers;
using Remito.Api.Security;

namespace Remito.UnitTests;

public sealed class AdminRbacMatrixTests
{
    private static readonly string[] Roles = ["Admin", "PartnerManager", "AdsManager", "ReadOnly"];
    private static readonly string[] AdminActions = typeof(AdminController)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => $"{method.Name}:{attribute.Policy}"))
        .ToArray();

    [Fact]
    public async Task Every_admin_role_is_checked_against_each_protected_action_policy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(AdminAuthorizationPolicies.Configure);
        using var provider = services.BuildServiceProvider();
        var authorizer = provider.GetRequiredService<IAuthorizationService>();

        Assert.NotEmpty(AdminActions);
        foreach (var role in Roles)
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, role)],
                authenticationType: "test"));

            foreach (var action in AdminActions)
            {
                var policy = action[(action.IndexOf(':') + 1)..];
                var result = await authorizer.AuthorizeAsync(principal, resource: null, policy);
                Assert.Equal(IsAllowed(role, policy), result.Succeeded);
            }
        }
    }

    private static bool IsAllowed(string role, string policy) =>
        policy switch
        {
            "AdminOnly" => role == "Admin",
            "PartnerManagement" => role is "Admin" or "PartnerManager",
            "AdsManagement" => role is "Admin" or "AdsManager",
            "ReadOnly" => role is "Admin" or "PartnerManager" or "AdsManager" or "ReadOnly",
            _ => false
        };
}
