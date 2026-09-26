namespace AgentPoc.Application.Interfaces;

/// <summary>
/// The authenticated customer for the current request. Resolved from the request
/// principal, never from a tool argument or the model.
/// </summary>
public interface ICustomerContext
{
    string CustomerId { get; }
}
