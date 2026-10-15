using System;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using SPT.Reflection.Patching;

namespace Liquidwarp.ArmorExpert;

internal class StaticIconsPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        typeof(StaticIcons).GetMethod("GetAttributeIcon", BindingFlags.Public | BindingFlags.Instance);

    [PatchPrefix]
    private static void PatchPrefix(ref Enum id)
    {
        if (id is EArmorExtraAttributeId extraId)
            id = ArmorAttributes.GetIconId(extraId);
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

internal class ArmoredEquipmentPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        typeof(ArmoredEquipment).GetConstructor([typeof(string), typeof(ArmoredEquipmentTemplate)]);

    [PatchPostfix]
    private static void PatchPostfix(ArmoredEquipment __instance) =>
        __instance.AddExtraAttributes();
}

internal class HeadphonesPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        typeof(Headphones).GetConstructor([typeof(string), typeof(HeadphonesTemplate)]);

    [PatchPostfix]
    private static void PatchPostfix(Headphones __instance) =>
        __instance.AddExtraAttributes();
}
