using Microsoft.AspNetCore.Authorization;

namespace Remito.Api.Security;

public static class AdminAuthorizationPolicies
{
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        options.AddPolicy("PartnerManagement", policy => policy.RequireRole("Admin", "PartnerManager"));
        options.AddPolicy("AdsManagement", policy => policy.RequireRole("Admin", "AdsManager"));
        options.AddPolicy("ReadOnly", policy =>
            policy.RequireRole("Admin", "PartnerManager", "AdsManager", "ReadOnly"));
    }
}
