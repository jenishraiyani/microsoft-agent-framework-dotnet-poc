using AgentPoc.Api.Configuration;
using AgentPoc.Api.ErrorHandling;
using AgentPoc.Api.Services;
using AgentPoc.Application;
using AgentPoc.Application.Interfaces;
using AgentPoc.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SchemaFilter<ChatRequestExampleSchemaFilter>());

builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});

// Only this project knows which model provider is in use.
builder.Services.AddModelProvider(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure();

builder.Services
    .AddOptions<CustomerIdentityOptions>()
    .Bind(builder.Configuration.GetSection(CustomerIdentityOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICustomerContext, HttpCustomerContext>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AgentExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

/// <summary>Exposed so integration tests can host the API with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
