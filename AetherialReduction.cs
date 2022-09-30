using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace TartargaTweaks;

public unsafe class QuickAetherialReduction : TartargaTweaks.SubTweak
{

    public override string Name => "Quick Aetherial Reduction";
    public override string Description => "Hold a modifier key to use Aetherial Reduction one click.";

    public class Configs : TweakConfig
    {
        public bool Shift = true;
        public bool Ctrl = false;
        public bool Alt = false;
    }

    private delegate void* OpenInventoryContext(AgentInventoryContext* agent, InventoryType inventory, ushort slot, int a4, ushort a5, byte a6);
    private HookWrapper<OpenInventoryContext> openInventoryContextHook;

    public Configs Config { get; private set; }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        ImGui.Text("Modifier Keys to Quick Reduce:");
        ImGui.Dummy(Vector2.Zero);
        ImGui.Indent();
        ImGui.BeginGroup();
        hasChanged |= ImGui.Checkbox("Shift", ref Config.Shift);
        hasChanged |= ImGui.Checkbox("Ctrl", ref Config.Ctrl);
        hasChanged |= ImGui.Checkbox("Alt", ref Config.Alt);
        ImGui.EndGroup();
        var s = ImGui.GetItemRectSize();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddRect(min - ImGui.GetStyle().ItemSpacing, max + ImGui.GetStyle().ItemSpacing, 0x99999999);
        ImGui.SameLine();
        ImGui.BeginGroup();
        var s2 = ImGui.CalcTextSize(" + RIGHT CLICK");
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.Dummy(new Vector2(s.Y / 2 - s2.Y / 2));
        ImGui.Text(" + RIGHT CLICK");
        ImGui.PopStyleVar();

        if (!(Config.Shift || Config.Ctrl || Config.Alt))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, 0xFF3333DD);
            ImGui.Text("  At least one modifier key must be enabled.");
            ImGui.PopStyleColor();
        }

        ImGui.EndGroup();
        ImGui.Unindent();
    };

    public InventoryType[] CanReduceFrom = {
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4
    };

    private string arText = "Aetherial Reduction";

    public override void Enable()
    {
        Config = LoadConfig<Configs>() ?? new Configs();

        var arRow = Service.Data.Excel.GetSheet<Addon>()?.GetRow(2160);
        if (arRow != null) arText = arRow.Text?.RawString ?? "Aetherial Reduction";

        openInventoryContextHook ??= Common.Hook<OpenInventoryContext>("83 B9 ?? ?? ?? ?? ?? 7E 11", OpenInventoryContextDetour);
        openInventoryContextHook?.Enable();
        base.Enable();
    }

    private bool HotkeyIsHeld => (Service.KeyState[VirtualKey.SHIFT] || !Config.Shift) && (Service.KeyState[VirtualKey.CONTROL] || !Config.Ctrl) && (Service.KeyState[VirtualKey.MENU] || !Config.Alt) && (Config.Ctrl || Config.Shift || Config.Alt);

    private void* OpenInventoryContextDetour(AgentInventoryContext* agent, InventoryType inventoryType, ushort slot, int a4, ushort a5, byte a6)
    {
        var retVal = openInventoryContextHook.Original(agent, inventoryType, slot, a4, a5, a6);

        try
        {
            if (HotkeyIsHeld && CanReduceFrom.Contains(inventoryType))
            {
                var inventory = InventoryManager.Instance()->GetInventoryContainer(inventoryType);
                if (inventory != null)
                {
                    var itemSlot = inventory->GetInventorySlot(slot);
                    if (itemSlot != null)
                    {
                        var itemId = itemSlot->ItemID;
                        var item = Service.Data.Excel.GetSheet<Item>()?.GetRow(itemId);
                        if (item != null)
                        {
                            Dalamud.Logging.PluginLog.Debug("Got Item");
                            var addonId = agent->AgentInterface.GetAddonID();
                            if (addonId == 0) return retVal;
                            var addon = Common.GetAddonByID(addonId);
                            if (addon == null) return retVal;
                            Dalamud.Logging.PluginLog.Debug($"Searching Context Menu, {agent->ContextItemCount} items?");

                            for (var i = 0; i < agent->ContextItemCount; i++)
                            {
                                var contextItemParam = agent->EventParamsSpan[agent->ContexItemStartIndex + i];
                                if (contextItemParam.Type != ValueType.String) continue;
                                var contextItemName = contextItemParam.ValueString();
                                Dalamud.Logging.PluginLog.Debug($"Context Item {contextItemName}");

                                if (contextItemName == arText)
                                {
                                    Dalamud.Logging.PluginLog.Debug($"Context Item Found");
                                    Common.GenerateCallback(addon, 0, i, 0U, 0, 0);
                                    agent->AgentInterface.Hide();
                                    UiHelper.Close(addon);
                                    return retVal;
                                }
                                else Dalamud.Logging.PluginLog.Debug($"Did not find, arText is {arText}");
                            }
                        }
                    }
                }
            } else Dalamud.Logging.PluginLog.Debug($"InventoryType {inventoryType} was not expected.");
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex);
        }

        return retVal;
    }

    public override void Disable()
    {
        openInventoryContextHook?.Disable();
        SaveConfig(Config);
        base.Disable();
    }

    public override void Dispose()
    {
        openInventoryContextHook?.Dispose();
        base.Dispose();
    }
}

