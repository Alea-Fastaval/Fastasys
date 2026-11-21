using Microsoft.AspNetCore.Authorization;

namespace Fastasys.ApiService.helpers;

public static class AuthHelper
{

    const string AdminRole = "Admin";
    const string PrivilegeClaim = "privilege";

    extension(AuthorizationBuilder authBuild)
    {
        // The policy passes for the Admin role and for a user with any of the privileges
        public AuthorizationBuilder AddPolicy(string name, params string[] values)
        {

            return authBuild
                .AddPolicy(name, policy =>
                    policy.RequireAssertion(context =>
                        context.User.IsInRole(AdminRole) ||
                        values.Any(value => context.User.HasClaim(PrivilegeClaim, value))));
        }
    }
}
