// Append only - ordinals are saved in CampaignSaveData.ordeals and packed into OrdealMask bits.
public enum OrdealId
{
    None,
    VeteranHosts, QuickenedHosts, EliteGuard, UnbrokenRanks, Ambush,
    PressGanged, GreenRecruits, ShortQuivers, BluntedCharge, Deserters, RustedArms,
    TheTithe, IronCoffers, NoQuarter, Embargo, ScorchedEarth, FogOnTheRoad, LongNight, BloodPrice,
    ArcaneDrought, SealedPage,
    MercenaryContract, BloodPact,
    // The March's two standing laws: held by every run past the last story act, never drawn.
    NoRespite, NoReinforcements,
    Outnumbered, ForcedMarch, NoRetreat, Dread, BlindMarch, TwinBanners,
    DeathWish, LastStand, BurnTheWagons,
    // Only ever a node's Twist, never a card.
    FoulWeather,
}

/// <summary>The run's Ordeals as one bit per OrdealId, so ECS code can read them from CampaignSaveDataHolder.</summary>
public static class OrdealMask
{
    // A ulong holds 64 bits, so OrdealId must stay below 64 values.
    public const int MAX_ORDEALS = 64;

    public static ulong Bit(OrdealId id) => id == OrdealId.None ? 0UL : 1UL << (int)id;
    public static bool Has(ulong mask, OrdealId id) => (mask & Bit(id)) != 0;

    // Here rather than in OrdealRegistry because the battle systems cannot see the main assembly.
    public const float BLOOD_PACT_DAMAGE = 1.10f;
    public const float DEATH_WISH_DAMAGE = 1.25f;
    // Last Stand: a squad that enters a battle below half strength holds longer.
    public const int LAST_STAND_LEADERSHIP = 20;
    public const int DREAD_LEADERSHIP = 10;
    public const int BURN_THE_WAGONS_LEADERSHIP = 10;
    // Forced March: how long the player's squads stay Weary from the start of a battle.
    public const float FORCED_MARCH_WEARY_SECONDS = 60f;

    public static bool BelowHalfStrength(int currentHealth, int maxHealth) => maxHealth > 0 && currentHealth * 2 < maxHealth;

    /// <summary>What the damage Ordeals multiply a hit by, dealt or taken by the player's squads.</summary>
    public static float DamageScale(ulong mask)
    {
        float scale = 1f;
        if (Has(mask, OrdealId.BloodPact)) scale *= BLOOD_PACT_DAMAGE;
        if (Has(mask, OrdealId.DeathWish)) scale *= DEATH_WISH_DAMAGE;
        return scale;
    }
}
