using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectDoomsdayServer.TestSupport;

public static class TestAuthenticationExtensions
{
    public static void AddTestAuthentication(this IServiceCollection services) =>
        services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName,
                _ => { }
            );

    public static HttpClient CreateClientAs<TEntryPoint>(
        this WebApplicationFactory<TEntryPoint> factory,
        string userId
    )
        where TEntryPoint : class
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestIdHeader, userId);
        return client;
    }
}
