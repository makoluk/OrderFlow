using System.Linq;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using OrderFlow.PaymentService.Data;
using Microsoft.Data.Sqlite;

namespace OrderFlow.Tests;

public class PaymentServiceFactory : WebApplicationFactory<OrderFlow.PaymentService.EntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Replace PaymentDbContext with SQLite in-memory
            var dbDescriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<PaymentDbContext>) ||
                d.ServiceType == typeof(PaymentDbContext)).ToList();
            foreach (var d in dbDescriptors) services.Remove(d);

            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            services.AddSingleton(connection);
            services.AddDbContext<PaymentDbContext>(o => o.UseSqlite(connection));

            // Remove MassTransit to avoid hosted service startup
            var mtDescriptors = services.Where(d =>
                d.ServiceType?.Namespace?.StartsWith("MassTransit") == true ||
                d.ImplementationType?.Namespace?.StartsWith("MassTransit") == true).ToList();
            foreach (var d in mtDescriptors) services.Remove(d);
            var hostedMt = services.Where(d =>
                d.ServiceType == typeof(IHostedService) &&
                (d.ImplementationType?.FullName?.Contains("MassTransit") ?? false)).ToList();
            foreach (var d in hostedMt) services.Remove(d);

            // Provide a minimal IBusControl stub so health/ready can resolve and read Address
            var busMock = new Mock<IBusControl>();
            busMock.SetupGet(b => b.Address).Returns(new Uri("loopback://localhost/"));
            services.AddSingleton(busMock.Object);

            // Remove OpenTelemetry (Jaeger) to avoid network calls in tests
            var otelDescriptors = services.Where(d =>
                (d.ServiceType?.Namespace?.StartsWith("OpenTelemetry") ?? false) ||
                (d.ImplementationType?.Namespace?.StartsWith("OpenTelemetry") ?? false)).ToList();
            foreach (var d in otelDescriptors) services.Remove(d);
            var hostedOtel = services.Where(d =>
                d.ServiceType == typeof(IHostedService) &&
                (d.ImplementationType?.FullName?.Contains("OpenTelemetry") ?? false)).ToList();
            foreach (var d in hostedOtel) services.Remove(d);

            // Build and ensure DB schema
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            db.Database.EnsureCreated();
        });
    }
}

