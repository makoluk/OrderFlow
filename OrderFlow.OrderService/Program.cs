using OrderFlow.Shared.Logging;
using OrderFlow.Shared.Extensions;
using OrderFlow.OrderService.Extensions;

// W3C trace + baggage
TracingDefaults.ConfigureW3C();

var builder = WebApplication.CreateBuilder(args);

// Logging tek provider
builder.Logging.ClearProviders();
builder.Host.ConfigureSerilog();

// Register OrderService module
builder.Services.AddOrderServiceModule(builder.Configuration);

var app = builder.Build();

app.UseOrderServicePipeline();

app.Run();

public partial class Program { }
namespace OrderFlow.OrderService { public class EntryPoint { } }
