using System.Security.Claims;
using AgentPoc.Api.Configuration;
using AgentPoc.Application.Exceptions;
using AgentPoc.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace AgentPoc.Api.Services;

/// <summary>
/// Resolves the authenticated customer from the request claim. The header override and the
/// configured fallback stand in for the identity provider this POC does not have, and are
/// confined to Development; anywhere else an unauthenticated request is refused.
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
            return claim.Trim();
        }

        if (!environment.IsDevelopment())
        {
            logger.LogWarning("Refusing a request that carries no authenticated customer.");
            throw new UnauthenticatedCustomerException();
        }

        if (options.AllowHeaderOverrideInDevelopment)
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

        if (string.IsNullOrWhiteSpace(options.DevelopmentCustomerId))
        {
            throw new UnauthenticatedCustomerException();
        }

        logger.LogWarning(
            "No authenticated customer on the request; falling back to the configured development customer.");

        return options.DevelopmentCustomerId.Trim();
    }
}
