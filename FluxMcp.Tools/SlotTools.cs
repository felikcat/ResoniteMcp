using FrooxEngine;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using Elements.Core;
using System.Threading.Tasks;
using SkyFrost.Base;
using Elements.Assets;
using System.IO;

namespace FluxMcp.Tools;

/// <summary>
/// Provides tools for manipulating slots in the focused world.
/// </summary>
[McpServerToolType]
public static class SlotTools
{
    /// <summary>
    /// Imports a file into the specified slot. Supports .brson, .7zbson, etc.
    /// </summary>
    /// <param name="path">The local path to the file to import.</param>
    /// <param name="parentSlotRefId">The RefID of the slot to import into.</param>
    /// <returns>A status message indicating the result.</returns>
    [McpServerTool(Name = "importFile"), Description("Imports a file (e.g. .brson) into the world. Can specify a parent slot.")]
    public static async Task<object> ImportFile(string path, string parentSlotRefId)
    {
        return await NodeToolHelpers.HandleAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", nameof(path));

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException($"File not found: {fullPath}");

            if (!RefID.TryParse(parentSlotRefId, out var refID)) throw new ArgumentException("Invalid RefID.", nameof(parentSlotRefId));

            return await NodeToolHelpers.UpdateAction(NodeToolHelpers.FocusedWorld.RootSlot, () =>
            {
                var parentSlot = NodeToolHelpers.FocusedWorld.ReferenceController.GetObjectOrNull(refID) as Slot;
                if (parentSlot == null) throw new InvalidOperationException($"Parent slot {parentSlotRefId} not found.");

                Slot? importedSlot = null;
                
                // FrooxEngine.SlotChildEvent usually takes (Slot slot, Slot child)
                void OnChildAdded(Slot slot, Slot child) 
                { 
                    // Capture the first child added during the import process
                    if (importedSlot == null) importedSlot = child; 
                }

                var world = parentSlot.World;
                
                // Monitor potential import locations
                world.RootSlot.ChildAdded += OnChildAdded;
                world.LocalUserSpace.ChildAdded += OnChildAdded;

                try
                {
                    // Use UniversalImporter with World overload
                    UniversalImporter.Import(AssetClass.Object, new[] { fullPath }, world, float3.Zero, floatQ.Identity, false);
                }
                finally
                {
                    world.RootSlot.ChildAdded -= OnChildAdded;
                    world.LocalUserSpace.ChildAdded -= OnChildAdded;
                }

                if (importedSlot != null)
                {
                    // Reparent the detected new slot to the requested parent
                    importedSlot.Parent = parentSlot;
                    return $"Successfully imported {Path.GetFileName(fullPath)} and moved to {parentSlot.Name} ({parentSlot.ReferenceID}).";
                }

                return $"Import started for {Path.GetFileName(fullPath)}. Note: Could not automatically detect and reparent the new slot. Please check World Root or Local User Space.";
            });
        }).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Moves a slot into a new parent slot.
    /// </summary>
    /// <param name="slotRefId">The RefID of the slot to move.</param>
    /// <param name="newParentRefId">The RefID of the new parent slot.</param>
    /// <returns>A status message indicating the result.</returns>
    [McpServerTool(Name = "moveSlot"), Description("Moves a slot into a new parent slot.")]
    public static async Task<object> MoveSlot(string slotRefId, string newParentRefId)
    {
         return await NodeToolHelpers.HandleAsync(async () =>
        {
            if (!RefID.TryParse(slotRefId, out var childRef)) throw new ArgumentException("Invalid RefID for slot.", nameof(slotRefId));
            if (!RefID.TryParse(newParentRefId, out var parentRef)) throw new ArgumentException("Invalid RefID for parent.", nameof(newParentRefId));

            return await NodeToolHelpers.UpdateAction(NodeToolHelpers.FocusedWorld.RootSlot, () =>
            {
                var childSlot = NodeToolHelpers.FocusedWorld.ReferenceController.GetObjectOrNull(childRef) as Slot;
                if (childSlot == null) throw new InvalidOperationException($"Slot {slotRefId} not found.");
                
                var parentSlot = NodeToolHelpers.FocusedWorld.ReferenceController.GetObjectOrNull(parentRef) as Slot;
                if (parentSlot == null) throw new InvalidOperationException($"Parent {newParentRefId} not found.");

                childSlot.Parent = parentSlot;
                return $"Moved {childSlot.Name} to {parentSlot.Name}.";
            });
        }).ConfigureAwait(false);
    }
}