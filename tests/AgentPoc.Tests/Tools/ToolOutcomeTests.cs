using System.Text.Json;
using AgentPoc.Application.Tools;

namespace AgentPoc.Tests.Tools;

/// <summary>
/// Pins the JSON shape the rest of the turn report depends on: by the time a result is
/// inspected, the tool's own type is gone.
/// </summary>
public sealed class ToolOutcomeTests
{
    private static JsonElement Serialize<T>(ToolResult<T> result) =>
        JsonSerializer.SerializeToElement(result, JsonSerializerOptions.Web);

    [Fact]
    public void A_refusal_survives_serialisation()
    {
        var refused = Serialize(ToolResult<string>.Failed("The order has already shipped."));

        Assert.True(ToolOutcome.IsRefusal(refused));
    }

    [Fact]
    public void A_not_found_result_is_a_refusal_too()
    {
        Assert.True(ToolOutcome.IsRefusal(Serialize(ToolResult<string>.NotFound("ORD-9999"))));
    }

    [Fact]
    public void A_success_is_not_a_refusal()
    {
        Assert.False(ToolOutcome.IsRefusal(Serialize(ToolResult<string>.Ok("data", "Done."))));
    }

    [Fact]
    public void Anything_that_is_not_a_tool_result_is_left_alone()
    {
        Assert.False(ToolOutcome.IsRefusal(null));
        Assert.False(ToolOutcome.IsRefusal("plain text"));
        Assert.False(ToolOutcome.IsRefusal(JsonDocument.Parse("[]").RootElement));
    }
}
