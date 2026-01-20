using FrooxEngine;
using ModelContextProtocol.Server;
using ResoniteModLoader;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Elements.Core;
using Component = FrooxEngine.Component;

namespace FluxMcp.Tools;

/// <summary>
/// Provides tools for managing components on slots in the focused world.
/// </summary>
[McpServerToolType]
public static class ComponentTools
{
    /// <summary>
    /// Gets all components on a specific slot.
    /// </summary>
    /// <param name="slotRefId">The RefID string of the slot to inspect.</param>
    /// <returns>A collection of components on the slot or null if an error occurs.</returns>
    [McpServerTool(Name = "getComponents"), Description("Gets all components on a specific slot.")]
    public static async Task<object?> GetComponents(string slotRefId)
    {
        return await NodeToolHelpers.HandleAsync(() => NodeToolHelpers.UpdateWorldAction(() =>
        {
            if (!RefID.TryParse(slotRefId, out var refID))
            {
                throw new ArgumentException("Invalid RefID format.", nameof(slotRefId));
            }

            var obj = NodeToolHelpers.FocusedWorld.ReferenceController.GetObjectOrNull(refID);
            if (obj is not Slot slot)
            {
                throw new InvalidOperationException($"Element with RefID {slotRefId} is not a Slot.");
            }

            return slot.Components;
        })).ConfigureAwait(false);
    }

    /// <summary>
    /// Attaches a component to a specific slot.
    /// </summary>
    /// <param name="slotRefId">The RefID string of the slot to attach the component to.</param>
    /// <param name="componentType">The type name of the component to attach (e.g., "FrooxEngine.BoxCollider").</param>
    /// <returns>The attached component or null if an error occurs.</returns>
    [McpServerTool(Name = "attachComponent"), Description("Attaches a component to a specific slot.")]
    public static async Task<object?> AttachComponent(string slotRefId, string componentType)
    {
        return await NodeToolHelpers.HandleAsync(() => NodeToolHelpers.UpdateWorldAction(() =>
        {
            if (!RefID.TryParse(slotRefId, out var refID))
            {
                throw new ArgumentException("Invalid RefID format.", nameof(slotRefId));
            }

            var obj = NodeToolHelpers.FocusedWorld.ReferenceController.GetObjectOrNull(refID);
            if (obj is not Slot slot)
            {
                throw new InvalidOperationException($"Element with RefID {slotRefId} is not a Slot.");
            }

            var type = NodeToolHelpers.Types.DecodeType(componentType);
            if (type == null)
            {
                // Try to find the type by simple name if full name fails, or search assemblies
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.FullName == componentType || t.Name == componentType);
                
                if (type == null)
                {
                    throw new ArgumentException($"Could not find type '{componentType}'.", nameof(componentType));
                }
            }

            if (!typeof(Component).IsAssignableFrom(type))
            {
                throw new ArgumentException($"Type '{componentType}' is not a Component.", nameof(componentType));
            }

            return slot.AttachComponent(type);
        })).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a component by its RefID.
    /// </summary>
    /// <param name="componentRefId">The RefID string of the component to remove.</param>
    /// <returns>True if the component was removed, false otherwise.</returns>
    [McpServerTool(Name = "removeComponent"), Description("Removes a component by its RefID.")]
    public static async Task<object?> RemoveComponent(string componentRefId)
    {
        return await NodeToolHelpers.HandleAsync(() => NodeToolHelpers.UpdateWorldAction(() =>
        {
            if (!RefID.TryParse(componentRefId, out var refID))
            {
                throw new ArgumentException("Invalid RefID format.", nameof(componentRefId));
            }

            var obj = NodeToolHelpers.FocusedWorld.ReferenceController.GetObjectOrNull(refID);
            if (obj is not Component component)
            {
                throw new InvalidOperationException($"Element with RefID {componentRefId} is not a Component.");
            }

            component.Slot.RemoveComponent(component);
            return true;
        })).ConfigureAwait(false);
    }
}