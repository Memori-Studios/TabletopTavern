using UnityEngine;

namespace TJ.Prestige
{
    /// <summary>
    /// The icon each prestige trait shows on the Prestige III picker: the stat it works on, from the IconLibrary.
    /// Placeholders until trait icons are drawn.
    /// </summary>
    public static class PrestigeTraitIcons
    {
        private const string FALLBACK = "Prestige";

        public static Sprite Get(UnitAttribute trait) => SpriteData.GetSprite(Key(trait));

        public static string Key(UnitAttribute trait) => trait switch
        {
            UnitAttribute.ArmorPiercing => "Armor",
            UnitAttribute.Emblazing => "Armor",
            UnitAttribute.AntiInfantry => "MeleeAttack",
            UnitAttribute.BackStabbers => "MeleeAttack",
            UnitAttribute.AntiLarge => "WeaponStrength",
            UnitAttribute.BloodFrenzy => "WeaponStrength",
            UnitAttribute.Rage => "AttackDamage",
            UnitAttribute.MonsterSlayer => "AttackDamage",
            UnitAttribute.Terrifying => "Leadership",
            UnitAttribute.Stalwart => "Leadership",
            UnitAttribute.FlamingAmmo => "MissileStrength",
            UnitAttribute.Demolisher => "MissileStrength",
            UnitAttribute.ShotDiscipline => "Accuracy",
            UnitAttribute.SteadyAim => "Accuracy",
            UnitAttribute.Overdraw => "Range",
            UnitAttribute.PowderReserves => "Ammunition",
            UnitAttribute.DeepQuivers => "Ammunition",
            UnitAttribute.PotentMagic => "AttackDamage",
            UnitAttribute.WideWeave => "SpellStatArea",
            UnitAttribute.FarCast => "Range",
            UnitAttribute.Quickcast => "SpellStatCooldown",
            UnitAttribute.SwiftStride => "Speed",
            UnitAttribute.ProjectileWard => "Armor",
            UnitAttribute.ThickScales => "Armor",
            UnitAttribute.Ethereal => "Armor",
            UnitAttribute.ForestDweller => "MeleeAttack",
            UnitAttribute.SpellWard => "Mana",
            _ => FALLBACK,
        };
    }
}
