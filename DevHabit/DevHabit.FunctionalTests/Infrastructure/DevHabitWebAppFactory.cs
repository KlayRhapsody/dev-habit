using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using WireMock.Server;

namespace DevHabit.FunctionalTests.Infrastructure;

public sealed class DevHabitWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:17.2")
        .WithDatabase("devhabit")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    
    private WireMockServer _wireMockServer;

    public WireMockServer GetWireMockServer() => _wireMockServer;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", _postgresContainer.GetConnectionString());
        builder.UseSetting("Github:BaseUrl", _wireMockServer.Urls[0]);
        builder.UseSetting("Encryption:Key", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        Quartz.Logging.LogContext.SetCurrentLogProvider(NullLoggerFactory.Instance);

        builder.UseSetting("Jwt:ExternalAuth", "false");
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        _wireMockServer = WireMockServer.Start();
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.StopAsync();
        _wireMockServer.Stop();
    }
}
