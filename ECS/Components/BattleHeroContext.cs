// The hero whose bonus rules apply to one team's squads in a live battle, and the inputs those rules
// compare against. The player side has the run's hero; the enemy side has one only when a warlord
// leads the army (EnemyWarlord in the main assembly decides that).
public readonly struct BattleHeroContext
{
    public readonly int HeroID;
    public readonly Race HeroRace;
    // What an EnemyRace condition compares against: the other side's race.
    public readonly Race EnemyRace;
    // Faction rules apply only while every squad on this side is Sakura Dynasty, as for the player.
    public readonly bool OnlySakuraUnits;

    public bool HasHero => HeroID > 0;

    public BattleHeroContext(int heroID, Race heroRace, Race enemyRace, bool onlySakuraUnits)
    {
        HeroID = heroID;
        HeroRace = heroRace;
        EnemyRace = enemyRace;
        OnlySakuraUnits = onlySakuraUnits;
    }

    // A warlord's rules read the player's hero race wherever the player's rules read the enemy race.
    public static BattleHeroContext For(in CampaignSaveDataHolder holder, Team team) => team == Team.Player
        ? new BattleHeroContext(holder.ActiveHeroID, holder.PlayerHeroRace, holder.EnemyRace, holder.OnlySakuraUnits)
        : new BattleHeroContext(holder.EnemyWarlordHeroID, holder.EnemyWarlordRace, holder.PlayerHeroRace, holder.EnemyOnlySakuraUnits);
}
