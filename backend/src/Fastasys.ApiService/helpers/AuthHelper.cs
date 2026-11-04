using Microsoft.AspNetCore.Authorization;

namespace Fastasys.ApiService.helpers;

public static class AuthHelper
{

    const string AdminRole = "Admin";
    const string PrivilegeClaim = "privilege";

    extension(AuthorizationBuilder authBuild)
    {
        public AuthorizationBuilder AddPolicy(string name, string value)
        {
            return authBuild
                .AddPolicy(name, policy =>
                    policy.RequireAssertion(context =>
                        context.User.IsInRole(AdminRole) ||
                        context.User.HasClaim(PrivilegeClaim, value)));
        }
    }
}
