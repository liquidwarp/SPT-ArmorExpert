using Comfort.Common;
using EFT;
using EFT.ActiveHeadphones;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liquidwarp.ArmorExpert;

internal enum EArmorExtraAttributeId
{
    Deflection, Penetration, BluntThroughput, SoftBluntReduction, EffectiveDurability, SoundReduction,
    SelfNoise, AmbientNoise, Distortion, Tone
}

internal static class ArmorAttributes
{
    private static readonly string[] Names = Enum.GetNames(typeof(EArmorExtraAttributeId));

    public static EItemAttributeId GetIconId(EArmorExtraAttributeId id) => id switch
    {
        EArmorExtraAttributeId.Deflection => EItemAttributeId.Ricochet,
        EArmorExtraAttributeId.Penetration => EItemAttributeId.AmmoPenetrationPower,
        EArmorExtraAttributeId.BluntThroughput or EArmorExtraAttributeId.SoftBluntReduction => EItemAttributeId.MaxAmmoDamage,
        EArmorExtraAttributeId.EffectiveDurability => EItemAttributeId.ArmorMaterial,
        EArmorExtraAttributeId.SoundReduction or EArmorExtraAttributeId.SelfNoise or EArmorExtraAttributeId.AmbientNoise
            or EArmorExtraAttributeId.Distortion or EArmorExtraAttributeId.Tone => EItemAttributeId.Loudness,
        _ => EItemAttributeId.Undefined,
    };

    private static ItemAttribute Create(EArmorExtraAttributeId id, string displayName, Func<string> value,
        Func<float> @base = null, Func<string> tooltip = null, bool lessIsGood = false)
    {
        var attribute = new ItemAttribute(id)
        {
            Name = Names[(int)id],
            DisplayNameFunc = () => displayName,
            StringValue = value,
            DisplayType = () => EItemAttributeDisplayType.Compact,
            LessIsGood = lessIsGood,
        };
        if (@base != null)
            attribute.Base = @base;
        if (tooltip != null)
            attribute.Tooltip = tooltip;
        return attribute;
    }

    public static void AddExtraAttributes(this ArmorComponent armor)
    {
        Item item = armor.Item;

        item.Attributes.Add(Create(EArmorExtraAttributeId.Deflection, "Deflection chance",
            () => new Deflection(armor).Summary,
            @base: () => armor.Template.RicochetVals.x,
            tooltip: () => new Deflection(armor).Details));

        item.Attributes.Add(Create(EArmorExtraAttributeId.Penetration, "Penetrated at 50/100%",
            () => Penetration.Summary(armor),
            @base: () => Penetration.IsDestroyed(armor) ? 0f : Penetration.Current(armor).Half));

        item.Attributes.Add(Create(EArmorExtraAttributeId.BluntThroughput, "Blunt at pen",
            () => Blunt.Summary(armor),
            @base: () => armor.BluntThroughput,
            tooltip: () => "Percentage of damage applied as blunt if the shot doesn't penetrate",
            lessIsGood: true));

        if (Blunt.IsRemovablePlate(armor))
        {
            item.Attributes.Add(Create(EArmorExtraAttributeId.SoftBluntReduction, "Soft blunt reduction",
                () => Format.Percent(Blunt.SoftArmorReduction),
                @base: () => Blunt.SoftArmorReduction,
                tooltip: () => "Blunt damage reduced by this amount when there is soft armor behind this plate"));
        }

        item.Attributes.Add(Create(EArmorExtraAttributeId.EffectiveDurability, "Effective durability",
            () => Format.WithMax(Durability.EffectiveCurrent(armor), Durability.EffectiveMax(armor)),
            @base: () => Durability.EffectiveMax(armor),
            tooltip: () => Durability.Details(armor)));
    }

    public static void AddExtraAttributes(this ArmoredEquipment equipment)
    {
        if (!Hearing.IsShown(equipment))
            return;

        equipment.Attributes.Add(Create(EArmorExtraAttributeId.SoundReduction, "Sound reduction",
            () => Hearing.Strength(equipment).ToString(),
            @base: () => (float)Hearing.Strength(equipment),
            tooltip: () => Hearing.Details(equipment),
            lessIsGood: true));
    }

    public static void AddExtraAttributes(this Headphones headphones)
    {
        HeadphonesTemplate template = headphones.Template;

        headphones.Attributes.Add(Create(EArmorExtraAttributeId.SelfNoise, "Self noise",
            () => Format.Decibels(template.ClientPlayerCompressorSendLevel),
            @base: () => template.ClientPlayerCompressorSendLevel,
            tooltip: () => "Player sounds: steps, rustle, etc",
            lessIsGood: true));

        headphones.Attributes.Add(Create(EArmorExtraAttributeId.AmbientNoise, "Ambient noise",
            () => Format.Decibels(template.AmbientCompressorSendLevel),
            @base: () => template.AmbientCompressorSendLevel,
            lessIsGood: true));

        headphones.Attributes.Add(Create(EArmorExtraAttributeId.Distortion, "Distortion",
            () => Format.Percent(template.Distortion),
            @base: () => template.Distortion,
            lessIsGood: true));

        headphones.Attributes.Add(Create(EArmorExtraAttributeId.Tone, "Tone",
            () => Tone.Summary(template),
            tooltip: () => Tone.Details(template)));
    }
}

internal static class Tone
{
    private const float FlatThreshold = 0.5f; // dB
    private const float CutThreshold = -2f; // dB
    private const float MinGain = 1e-5f; // -100 dB

    private static EQBand[] Bands(HeadphonesTemplate template) =>
        [template.GetEQBand1(), template.GetEQBand2(), template.GetEQBand3()];

    // The mixer's EQ gains are linear multipliers.
    private static float Decibels(EQBand band) => 20f * Mathf.Log10(Mathf.Max(band.Gain, MinGain));

    private static string Range(float frequency) => frequency switch
    {
        < 500f => "low",
        < 2000f => "mid",
        < 4500f => "upper-mid",
        _ => "treble",
    };

    // Named after the strongest cut if any, else the strongest boost.
    public static string Summary(HeadphonesTemplate template)
    {
        EQBand[] bands = Bands(template);
        float lowest = float.MaxValue, highest = float.MinValue;
        foreach (EQBand band in bands)
        {
            float decibels = Decibels(band);
            lowest = Mathf.Min(lowest, decibels);
            highest = Mathf.Max(highest, decibels);
        }

        bool cut = lowest <= CutThreshold;
        float peak = cut ? lowest : highest;
        if (!cut && peak < FlatThreshold)
            return "Flat";

        var ranges = new List<string>();
        foreach (EQBand band in bands)
        {
            string range = Range(band.Frequency);
            if (Mathf.Abs(Decibels(band) - peak) < 0.05f && !ranges.Contains(range))
                ranges.Add(range);
        }

        string joined = string.Join("/", ranges);
        return char.ToUpper(joined[0]) + joined.Substring(1) + (cut ? " cut" : " boost");
    }

    public static string Details(HeadphonesTemplate template)
    {
        var lines = new List<string> { $"Bass cut below {Format.Frequency(template.HighpassFreq)}" };
        foreach (EQBand band in Bands(template))
            lines.Add($"{Format.Frequency(band.Frequency)}: {Decibels(band):+0.0;-0.0;0.0} dB");
        return string.Join("\n", lines);
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
        // Inverse of ArmorComponent.Deflects: Lerp(grazing, threshold, InverseLerp(90, minAngle, angle)).
        float angle = Mathf.Lerp(90f, _minAngle, Mathf.InverseLerp(_grazingChance, _thresholdChance, chance));
        return $"from {Format.Angle(angle)}";
    }
}

internal readonly struct Penetration(float resistance)
{
    private const float Window = 15f; // no chance at R - 15 or less, certain at R + 15 or more
    private static readonly float HalfOffset = Mathf.Sqrt(50f / 0.4f); // 0.4 * (R - pen - 15)^2 = 50

    private readonly float _resistance = resistance;

    public static Penetration Current(ArmorComponent armor) =>
        new(ShotSharedMethods.RealResistance(
            armor.Repairable.Durability, armor.Repairable.TemplateDurability, armor.ArmorClass, 0f).RealResistance);

    public int Half => Mathf.CeilToInt(_resistance - Window + HalfOffset);
    public int Full => Mathf.CeilToInt(_resistance + Window);

    // Blunt damage share is scaled by clamp(1 - 0.03 * (R - pen), 0.2, 1).
    public int BluntFull => Mathf.CeilToInt(_resistance);

    // Where the share bottoms out at a fifth, or 1 for low-class armor that never gets there.
    public int BluntLow => Mathf.Max(1, Mathf.FloorToInt(_resistance - 0.8f / 0.03f));
    public float BluntLowFactor => Mathf.Clamp(1f - 0.03f * (_resistance - BluntLow), 0.2f, 1f);

    public static bool IsDestroyed(ArmorComponent armor) => armor.Repairable.Durability <= 0f;

    // At current durability.
    public static string Summary(ArmorComponent armor)
    {
        if (IsDestroyed(armor))
            return "0 / 0";
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
        if (Penetration.IsDestroyed(armor))
            return "None";
        float share = armor.BluntThroughput;
        Penetration current = Penetration.Current(armor);
        return $"{Format.Percent(share * current.BluntLowFactor, sign: false)}-{Format.Percent(share)} at "
            + $"{current.BluntLow}-{current.BluntFull}";
    }
}

internal static class Hearing
{
    public static bool IsShown(ArmoredEquipment equipment) =>
        IsArmoredHeadGear(equipment) || Strength(equipment) != EDeafStrength.None;

    private static bool IsArmoredHeadGear(ArmoredEquipment equipment) =>
        equipment is Headwear or FaceCover && (equipment.Armor != null || HasArmorSlot(equipment));

    private static bool HasArmorSlot(ArmoredEquipment equipment)
    {
        foreach (Slot slot in equipment.Slots)
        {
            if (slot is ArmorSlot)
                return true;
        }
        return false;
    }

    public static EDeafStrength Strength(ArmoredEquipment equipment)
    {
        EDeafStrength strength = equipment._template.DeafStrength;
        foreach (CompositeArmorComponent component in equipment.GetItemComponentsInChildren<CompositeArmorComponent>())
        {
            if (component.Deaf > strength)
                strength = component.Deaf;
        }
        return strength;
    }

    public static string Details(ArmoredEquipment equipment)
    {
        var lines = new List<string> { "Only applies when no headset is worn. Can depend on attachments." };
        if (equipment is FaceCover)
            lines.Add("Only applies if the helmet doesn't reduce sound.");

        List<string> sources = Sources(equipment);
        if (sources.Count > 0)
            lines.Add("From: " + string.Join(", ", sources));

        return string.Join("\n", lines);
    }

    // Attachments responsible for the strength, when the item itself isn't.
    private static List<string> Sources(ArmoredEquipment equipment)
    {
        var sources = new List<string>();
        EDeafStrength strength = Strength(equipment);
        if (strength == equipment._template.DeafStrength)
            return sources;

        foreach (CompositeArmorComponent component in equipment.GetItemComponentsInChildren<CompositeArmorComponent>())
        {
            if (component.Deaf != strength || component.Item == equipment)
                continue;
            string name = component.Item.ShortName.Localized();
            if (!sources.Contains(name))
                sources.Add(name);
        }
        return sources;
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

    public static string Details(ArmorComponent armor) =>
        $"Repair degradation: {Range(armor.Repairable.RepairDegradation)}"
        + $"\nWith repair kit: {Range(armor.Repairable.RepairKitDegradation)}";

    private static string Range(Vector2 minMax) => $"{Format.Percent(minMax.x)}-{Format.Percent(minMax.y)}";
}

internal static class Format
{
    public static string Percent(float fraction, bool sign = true) =>
        (fraction * 100f).ToString("0.#") + (sign ? "%" : "");

    public static string Angle(float degrees) => degrees.ToString("0.#") + "°";

    public static string Decibels(float decibels) => decibels.ToString("0.#") + " dB";

    public static string Frequency(float hertz) =>
        hertz < 1000f ? hertz.ToString("0") + " Hz" : (hertz / 1000f).ToString("0.#") + " kHz";

    public static string WithMax(float current, float max)
    {
        string currentText = current.ToString("0"), maxText = max.ToString("0");
        return currentText == maxText ? currentText : $"{currentText}({maxText})";
    }
}
