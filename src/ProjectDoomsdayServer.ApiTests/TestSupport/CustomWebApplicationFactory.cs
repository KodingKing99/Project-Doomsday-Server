using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ProjectDoomsdayServer.Application.Files;
using NSubstitute;
using System.Linq;

namespace ProjectDoomsdayServer.ApiTests.TestSupport;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Expose the substitute so tests can assert Received calls
    public IFileStorage? FileStorageSubstitute { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove existing registration for IFileStorage if present
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFileStorage));
            if (descriptor != null) services.Remove(descriptor);

            // Create a substitute and register it
            var sub = Substitute.For<IFileStorage>();
            sub.GetPresignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
               .Returns(call => "https://example.com/presigned-url");

            FileStorageSubstitute = sub;
            services.AddSingleton(sub);
        });
    }
}
