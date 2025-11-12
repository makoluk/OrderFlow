using System.Linq;
using System.Net.Http.Headers;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using OrderFlow.OrderService.Data;
using OrderFlow.Shared.Contracts;
using Microsoft.Data.Sqlite;

namespace OrderFlow.Tests;

public class OrderServiceFactory : WebApplicationFactory<OrderFlow.OrderService.EntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Replace OrderDbContext with InMemory
            var descriptors = services.Where(d => d.ServiceType == typeof(DbContextOptions<OrderDbContext>)
                                               || d.ServiceType == typeof(OrderDbContext)).ToList();
            foreach (var d in descriptors) services.Remove(d);

            // Use SQLite in-memory (supports transactions)
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            services.AddSingleton(connection);
            services.AddDbContext<OrderDbContext>(o => o.UseSqlite(connection));

            // Remove MassTransit registrations (if any)
            var toRemove = services.Where(d =>
                d.ServiceType.Namespace != null &&
                d.ServiceType.Namespace.StartsWith("MassTransit")).ToList();
            foreach (var d in toRemove)
                services.Remove(d);
            var hostedToRemove = services.Where(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType?.FullName?.Contains("MassTransit", StringComparison.OrdinalIgnoreCase) == true).ToList();
            foreach (var d in hostedToRemove) services.Remove(d);

            // Remove OpenTelemetry/Jaeger exporters for tests
            var otelDescriptors = services.Where(d =>
                (d.ServiceType?.Namespace?.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (d.ImplementationType?.Namespace?.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            foreach (var d in otelDescriptors) services.Remove(d);
            var otelHosted = services.Where(d =>
                d.ServiceType == typeof(IHostedService) &&
                (d.ImplementationType?.FullName?.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            foreach (var d in otelHosted) services.Remove(d);

            // Register a minimal IBus mock to swallow Publish calls
            var busMock = new Mock<IBus>();
            busMock.Setup(b => b.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            busMock.Setup(b => b.Publish(It.IsAny<OrderCreated>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            services.AddSingleton(busMock.Object);

            // Build a temporary provider to initialize the database schema
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            db.Database.EnsureCreated();
        });
    }
}

