using System.Security.Claims;
using AgentPoc.Api.Configuration;
using AgentPoc.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace AgentPoc.Api.Services;

/// <summary>
/// Resolves the authenticated customer from the request: a claim first, then a
/// Development-only header for demos, then the configured fallback.
/// </summary>
public sealed class HttpCustomerContext : ICustomerContext
{
    private const string CustomerIdClaimType = "customer_id";
    private const string CustomerIdHeaderName = "X-Customer-Id";

    private readonly Lazy<string> _customerId;

    public HttpCustomerContext(
        IHttpContextAccessor httpContextAccessor,
        IHostEnvironment environment,
        IOptions<CustomerIdentityOptions> options,
        ILogger<HttpCustomerContext> logger)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _customerId = new Lazy<string>(() =>
            Resolve(httpContextAccessor.HttpContext, environment, options.Value, logger));
    }

    public string CustomerId => _customerId.Value;

    private static string Resolve(
        HttpContext? httpContext,
        IHostEnvironment environment,
        CustomerIdentityOptions options,
        ILogger logger)
    {
        var claim = httpContext?.User?.FindFirstValue(CustomerIdClaimType);
        if (!string.IsNullOrWhiteSpace(claim))
        {
            return claim;
        }

        if (environment.IsDevelopment() && options.AllowHeaderOverrideInDevelopment)
        {
            var header = httpContext?.Request.Headers[CustomerIdHeaderName].ToString();
            if (!string.IsNullOrWhiteSpace(header))
            {
                logger.LogWarning(
                    "Using {HeaderName} to select the customer. This is Development-only behaviour.",
                    CustomerIdHeaderName);
                return header.Trim();
            }
        }

        logger.LogWarning(
            "No authenticated customer on the request; falling back to the configured development customer.");

        return options.DevelopmentCustomerId;
    }
}
