using FrooxEngine.ProtoFlux;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Elements.Core;

namespace FluxMcp.Tools;

/// <summary>
/// Provides tools for getting and setting values in ProtoFlux input nodes.
/// </summary>
[McpServerToolType]
public static class NodeValueTools
{
    /// <summary>
    /// Gets the current value of a ProtoFlux input node.
    /// </summary>
    /// <param name="nodeRefId">The RefID of the input node to read from.</param>
    /// <returns>The current value of the input node or null if an error occurs.</returns>
    [McpServerTool(Name = "getInputNodeValue"), Description("Gets the current value of an input node. Use this to read values from input nodes like ValueInput<T>.")]
    public static object? GetInputNodeValue(string nodeRefId)
    {
        return NodeToolHelpers.Handle(() =>
        {
            var node = NodeLookupTools.FindNodeInternal(nodeRefId);
            if (node is not IInput inputNode)
            {
                throw new InvalidOperationException($"Node {nodeRefId} is not an input node.");
            }
            return inputNode.BoxedValue;
        });
    }

    /// <summary>
    /// Sets the value of a ProtoFlux input node with automatic type conversion.
    /// </summary>
    /// <param name="nodeRefId">The RefID of the input node to modify.</param>
    /// <param name="value">The new value to set, as a JSON string.</param>
    /// <returns>A task that represents the asynchronous operation, containing the set value or null if an error occurs.</returns>
    [McpServerTool(Name = "setInputNodeValue"), Description("Sets the value of an input node. Automatically handles type conversion for basic types like float, int, bool, and vectors. Use this to configure input nodes with specific values. Value must be a valid JSON string.")]
    public static async Task<object?> SetInputNodeValue(string nodeRefId, string value)
    {
        return await NodeToolHelpers.HandleAsync(async () =>
        {
            return await NodeToolHelpers.UpdateAction(NodeToolHelpers.WorkspaceSlot, () =>
            {
                var node = NodeLookupTools.FindNodeInternal(nodeRefId);
                if (node is not IInput inputNode)
                {
                    throw new InvalidOperationException($"Node {nodeRefId} is not an input node.");
                }
                var targetType = inputNode.InputType();

                JsonNode? jsonValue;
                try
                {
                    jsonValue = JsonNode.Parse(value);
                }
                catch (JsonException)
                {
                     // Fallback: treat string as a string literal if it fails parsing? 
                     // Or just wrap it if target is string?
                     // For now, let's assume it MUST be valid JSON.
                     throw new ArgumentException("Value must be a valid JSON string.");
                }

                if (jsonValue == null) throw new ArgumentNullException(nameof(value), "JSON value cannot be null");

                try
                {
                    if (targetType == typeof(colorX) && jsonValue is JsonObject jo && !jo.ContainsKey("profile"))
                    {
                        inputNode.BoxedValue = (colorX)jsonValue.Deserialize<color>(NodeToolHelpers.JsonOptions);
                    }
                    else
                    {
                        inputNode.BoxedValue = jsonValue.Deserialize(targetType, NodeToolHelpers.JsonOptions)!;
                    }
                }
                catch (InvalidCastException)
                {
                    throw new InvalidOperationException($"Cannot set input value of {value} into {targetType}");
                }

                return inputNode.BoxedValue;
            }).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
