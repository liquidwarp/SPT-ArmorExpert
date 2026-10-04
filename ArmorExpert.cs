using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace Liquidwarp.ArmorExpert;

internal enum EArmorExtraAttributeId
{
    Deflection, Penetration, BluntThroughput, SoftBluntReduction, EffectiveDurability
}

internal static class ArmorAttributes
{
    public static EItemAttributeId GetIconId(EArmorExtraAttributeId id) => id switch
    {
        EArmorExtraAttributeId.Deflection => EItemAttributeId.Ricochet,
        EArmorExtraAttributeId.Penetration => EItemAttributeId.AmmoPenetrationPower,
        EArmorExtraAttributeId.BluntThroughput => EItemAttributeId.MaxAmmoDamage,
        EArmorExtraAttributeId.SoftBluntReduction => EItemAttributeId.MaxAmmoDamage,
        EArmorExtraAttributeId.EffectiveDurability => EItemAttributeId.ArmorMaterial,
        _ => EItemAttributeId.Undefined,
    };

    public static void AddExtraAttributes(this ArmorComponent armor)
    {
        Item item = armor.Item;

        item.Attributes.Add(new ItemAttribute(EArmorExtraAttributeId.Deflection)
        {
            Name = EArmorExtraAttributeId.Deflection.ToString(),
            DisplayNameFunc = () => "Deflection chance",
            Base = () => armor.Template.RicochetVals.x,
            StringValue = () => new Deflection(armor).Summary,
            Tooltip = () => new Deflection(armor).Details,
            DisplayType = () => EItemAttributeDisplayType.Compact,
        });

        item.Attributes.Add(new ItemAttribute(EArmorExtraAttributeId.Penetration)
        {
            Name = EArmorExtraAttributeId.Penetration.ToString(),
            DisplayNameFunc = () => "Penetrated at 50/100%",
            Base = () => Penetration.Current(armor).Half,
            StringValue = () => Penetration.Summary(armor),
            DisplayType = () => EItemAttributeDisplayType.Compact,
        });

        item.Attributes.Add(new ItemAttribute(EArmorExtraAttributeId.BluntThroughput)
        {
            Name = EArmorExtraAttributeId.BluntThroughput.ToString(),
            DisplayNameFunc = () => "Blunt at pen",
            Base = () => armor.BluntThroughput,
            StringValue = () => Blunt.Summary(armor),
            Tooltip = () => "Percentage of damage applied as blunt if the shot doesn't penetrate",
            DisplayType = () => EItemAttributeDisplayType.Compact,
            LessIsGood = true,
        });

        if (Blunt.IsRemovablePlate(armor))
        {
            item.Attributes.Add(new ItemAttribute(EArmorExtraAttributeId.SoftBluntReduction)
            {
                Name = EArmorExtraAttributeId.SoftBluntReduction.ToString(),
                DisplayNameFunc = () => "Soft blunt reduction",
                Base = () => Blunt.SoftArmorReduction,
                StringValue = () => Format.Percent(Blunt.SoftArmorReduction),
                Tooltip = () => "Blunt damage reduced by this amount when there is soft armor behind this plate",
                DisplayType = () => EItemAttributeDisplayType.Compact,
            });
        }

        item.Attributes.Add(new ItemAttribute(EArmorExtraAttributeId.EffectiveDurability)
        {
            Name = EArmorExtraAttributeId.EffectiveDurability.ToString(),
            DisplayNameFunc = () => "Effective durability",
            Base = () => Durability.EffectiveMax(armor),
            StringValue = () => Format.WithMax(Durability.EffectiveCurrent(armor).ToString("0"), Durability.EffectiveMax(armor).ToString("0")),
            Tooltip = () => Durability.Details(armor),
            DisplayType = () => EItemAttributeDisplayType.Compact,
        });
    }
}

internal readonly struct Deflection(ArmorComponent armor)
{
    private readonly float _grazingChance = armor.Template.RicochetVals.x;
    private readonly float _thresholdChance = armor.Template.RicochetVals.y;
    private readonly float _minAngle = armor.Template.RicochetVals.z;

    public bool Possible => _grazingChance > 0f && _minAngle < 90f;

    public float Max => Possible ? Mathf.Max(_grazingChance, _thresholdChance) : 0f;

    public string Summary => Possible
        ? $"{Format.Percent(_thresholdChance, sign: false)}-{Format.Percent(_grazingChance)} above {Format.Angle(_minAngle)}"
        : "None";

    // No tooltip when the armor never deflects.
    public string Details => Possible ? $"50% {Reach(0.5f)}, 75% {Reach(0.75f)}" : null;

    // Smallest angle at which the chance reaches `chance`.
    private string Reach(float chance)
    {
        if (chance > Max)
            return "never";
        if (chance <= _thresholdChance)
            return $"just above {Format.Angle(_minAngle)}";
        float angle = 90f - (90f - _minAngle) * (_grazingChance - chance) / (_grazingChance - _thresholdChance);
        return $"from {Format.Angle(angle)}";
    }
}

internal readonly struct Penetration
{
    private const float Window = 15f; // no chance at R - 15 or less, certain at R + 15 or more
    private static readonly float HalfOffset = Mathf.Sqrt(50f / 0.4f); // 0.4 * (R - pen - 15)^2 = 50

    private readonly float _resistance;

    private Penetration(float resistance) => _resistance = resistance;

    public static Penetration Current(ArmorComponent armor) =>
        At(armor, armor.Repairable.Durability);

    private static Penetration At(ArmorComponent armor, float durability) =>
        new(ShotSharedMethods.RealResistance(durability, armor.Repairable.TemplateDurability, armor.ArmorClass, 0f).RealResistance);

    public int Half => Mathf.CeilToInt(_resistance - Window + HalfOffset);
    public int Full => Mathf.CeilToInt(_resistance + Window);

    // Blunt damage share is scaled by clamp(1 - 0.03 * (R - pen), 0.2, 1).
    public int BluntFull => Mathf.CeilToInt(_resistance);

    // Where the share bottoms out at a fifth, or 1 for low-class armor that never gets there.
    public int BluntLow => Mathf.Max(1, Mathf.FloorToInt(_resistance - 0.8f / 0.03f));
    public float BluntLowFactor => Mathf.Clamp(1f - 0.03f * (_resistance - BluntLow), 0.2f, 1f);

    // At current durability.
    public static string Summary(ArmorComponent armor)
    {
        Penetration current = Current(armor);
        return $"{current.Half} / {current.Full}";
    }
}

internal static class Blunt
{
    public static float SoftArmorReduction => 1f - Singleton<GlobalConfiguration>.Instance.BluntDamageReduceFromSoftArmorMod;

    public static bool IsRemovablePlate(ArmorComponent armor) =>
        armor.Item is ArmorPlate and not BuiltInInserts;

    public static string Summary(ArmorComponent armor)
    {
        float share = armor.BluntThroughput;
        Penetration current = Penetration.Current(armor);
        return $"{Format.Percent(share * current.BluntLowFactor, sign: false)}-{Format.Percent(share)} at "
            + $"{current.BluntLow}-{current.BluntFull}";
    }
}

internal static class Durability
{
    private static GlobalConfiguration.ArmorMaterialValues Material(ArmorComponent armor) =>
        Singleton<GlobalConfiguration>.Instance.ArmorMaterials[armor.Template.ArmorMaterial];

    public static float EffectiveCurrent(ArmorComponent armor) =>
        armor.Repairable.Durability / Material(armor).Destructibility;

    public static float EffectiveMax(ArmorComponent armor) =>
        armor.Repairable.MaxDurability / Material(armor).Destructibility;

    public static string Details(ArmorComponent armor)
    {
        GlobalConfiguration.ArmorMaterialValues material = Material(armor);
        return $"Repair degradation: {Format.Percent(material.MinRepairDegradation)}-{Format.Percent(material.MaxRepairDegradation)}"
            + $"\nWith repair kit: {Format.Percent(material.MinRepairKitDegradation)}-{Format.Percent(material.MaxRepairKitDegradation)}";
    }
}

internal static class Format
{
    public static string Percent(float fraction) => Percent(fraction, sign: true);

    public static string Percent(float fraction, bool sign) =>
        (fraction * 100f).ToString("0.#") + (sign ? "%" : "");

    public static string Angle(float degrees) => degrees.ToString("0.#") + "°";

    public static string WithMax(object current, object max)
    {
        string currentText = current.ToString(), maxText = max.ToString();
        return currentText == maxText ? currentText : $"{currentText}({maxText})";
    }
}
