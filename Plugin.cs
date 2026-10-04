using BepInEx;
using EFT.InventoryLogic;
using EFT.UI;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace Liquidwarp.ArmorExpert;

[BepInPlugin("com.liquidwarp.armorexpert", "Liquidwarp.ArmorExpert", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        new StaticIconsPatch().Enable();
        new ArmorComponentPatch().Enable();
    }
}

internal class StaticIconsPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        typeof(StaticIcons).GetMethod("GetAttributeIcon", BindingFlags.Public | BindingFlags.Instance);

    [PatchPrefix]
    private static bool PatchPrefix(Enum id) =>
        id is not EArmorExtraAttributeId;

    [PatchPostfix]
    private static void PatchPostfix(StaticIcons __instance, ref Sprite __result, Enum id)
    {
        if (id is EArmorExtraAttributeId extraId)
            __result = __instance.GetAttributeIcon(ArmorAttributes.GetIconId(extraId));
    }
}

internal class ArmorComponentPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        typeof(ArmorComponent).GetConstructor(
            [typeof(Item), typeof(IArmorComponentTemplate), typeof(RepairableComponent), typeof(BuffComponent)]);

    [PatchPostfix]
    private static void PatchPostfix(ArmorComponent __instance) =>
        __instance.AddExtraAttributes();
}
