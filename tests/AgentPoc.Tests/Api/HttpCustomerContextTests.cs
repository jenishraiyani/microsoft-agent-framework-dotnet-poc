using System.Security.Claims;
using AgentPoc.Api.Configuration;
using AgentPoc.Api.Services;
using AgentPoc.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentPoc.Tests.Api;

/// <summary>
/// Identity resolution fails closed: the demo conveniences exist only in Development.
/// </summary>
public sealed class HttpCustomerContextTests
{
    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "AgentPoc.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static HttpCustomerContext Create(string environmentName, HttpContext httpContext) =>
        new(
            new HttpContextAccessor { HttpContext = httpContext },
            new StubEnvironment(environmentName),
            Options.Create(new CustomerIdentityOptions()),
            NullLogger<HttpCustomerContext>.Instance);

    [Fact]
    public void An_unauthenticated_request_is_refused_outside_development()
    {
        var context = Create(Environments.Production, new DefaultHttpContext());

        Assert.Throws<UnauthenticatedCustomerException>(() => context.CustomerId);
    }

    [Fact]
    public void The_demo_header_is_ignored_outside_development()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Customer-Id"] = "CUST-002";

        var context = Create(Environments.Production, httpContext);

        Assert.Throws<UnauthenticatedCustomerException>(() => context.CustomerId);
    }

    [Fact]
    public void A_claim_identifies_the_customer_in_every_environment()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("customer_id", "CUST-009")]))
        };

        Assert.Equal("CUST-009", Create(Environments.Production, httpContext).CustomerId);
    }

    [Fact]
    public void Development_falls_back_so_the_poc_runs_without_an_identity_provider()
    {
        var context = Create(Environments.Development, new DefaultHttpContext());

        Assert.Equal("CUST-001", context.CustomerId);
    }
}
