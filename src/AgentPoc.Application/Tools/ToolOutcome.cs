using System.Text.Json;

namespace AgentPoc.Application.Tools;

/// <summary>
/// Reads the outcome back out of an invoked tool's result.
/// <para>
/// The framework serialises a tool's return value before middleware or the response ever
/// see it, so by then the <see cref="ToolResult{T}"/> is a <see cref="JsonElement"/> and
/// its type is gone. Without this, a refusal is indistinguishable from a success: both
/// returned normally.
/// </para>
/// </summary>
public static class ToolOutcome
{
    public static bool IsRefusal(object? result) => result is JsonElement json && IsRefusal(json);

    private static bool IsRefusal(JsonElement json)
    {
        if (json.ValueKind is not JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in json.EnumerateObject())
        {
            if (string.Equals(property.Name, nameof(ToolResult<object>.Success), StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind is JsonValueKind.False;
            }
        }

        return false;
    }
}
