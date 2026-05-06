using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace RogueNet.IntegrationTests.Fixtures;

public sealed class RogueNetWebAppFactory : WebApplicationFactory<Program>
{
    public string ConnectionString { get; init; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", ConnectionString),
            });
        });
    }
}
