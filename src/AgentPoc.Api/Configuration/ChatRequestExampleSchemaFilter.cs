using System.Text.Json.Nodes;
using AgentPoc.Api.Contracts;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AgentPoc.Api.Configuration;

/// <summary>
/// Corrects the generated schema for <see cref="ChatRequest"/>. Swagger would otherwise
/// pre-fill conversationId with "string", which the API rejects, and Swashbuckle does not
/// see validation attributes declared on a positional record's constructor parameters.
/// </summary>
public sealed class ChatRequestExampleSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Type != typeof(ChatRequest) || schema is not OpenApiSchema concrete)
        {
            return;
        }

        concrete.Example = new JsonObject
        {
            ["message"] = "Can I cancel order ORD-1001?"
        };

        concrete.Required = new HashSet<string>(StringComparer.Ordinal) { "message" };

        if (concrete.Properties?.TryGetValue("message", out var message) is true &&
            message is OpenApiSchema messageSchema)
        {
            messageSchema.MaxLength = 2000;
        }
    }
}
