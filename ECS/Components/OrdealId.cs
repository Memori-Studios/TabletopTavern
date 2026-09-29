// Append only - ordinals are saved in CampaignSaveData.ordeals and packed into OrdealMask bits.
public enum OrdealId
{
    None,
    VeteranHosts, QuickenedHosts, EliteGuard, UnbrokenRanks, Ambush,
    PressGanged, GreenRecruits, ShortQuivers, BluntedCharge, Deserters, RustedArms,
    TheTithe, IronCoffers, NoQuarter, Embargo, ScorchedEarth, FogOnTheRoad, LongNight, BloodPrice,
    ArcaneDrought, SealedPage,
    MercenaryContract, BloodPact,
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
}
