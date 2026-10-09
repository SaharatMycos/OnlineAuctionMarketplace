using System.Net;
using System.Net.Http.Json;
using Marketplace.Bootstrap;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.Api.Tests;

/// <summary>Boots the api in-process without a database ("Testing" has no connection string and no migrations).</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
}

public class ApiSmokeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Liveness_is_ok()
    {
        var response = await factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Lists_every_module()
    {
        var expected = ModuleCatalog.All.Select(m => m.Name).ToArray();

        var modules = await factory.CreateClient().GetFromJsonAsync<string[]>("/v1/modules");

        Assert.Equal(expected, modules);
    }
}
