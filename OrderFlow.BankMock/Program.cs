using OrderFlow.Shared.Logging;
using OrderFlow.Shared.Extensions;
using OrderFlow.BankMock.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.ClearProviders();
builder.Host.ConfigureSerilog();

// OpenTelemetry (default, Jaeger/OTLP auto based on config)
builder.Services.AddDefaultOpenTelemetry(builder.Configuration, builder.Environment.ApplicationName, addMassTransitInstrumentation: false, addPrometheusMetrics: true);

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var app = builder.Build();

app.UseCorrelationIdLogging();
app.UseDefaultRequestLogging();
app.UseGlobalExceptionHandling();
app.MapPrometheusIfNotTesting();

// Swagger UI (only if enabled)
app.UseSwagger();
app.UseSwaggerUI();

// Endpoints
app.MapBankEndpoints();

app.Run();
