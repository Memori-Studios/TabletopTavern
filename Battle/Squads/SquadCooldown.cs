using Unity.Entities;
using UnityEngine;

// Reads "how long until this squad can act again" for the two unit types slow enough that the wait
// is worth showing, so the flag health bar and the hover panel can never disagree about it.
//
// Both timers live on a UNIT entity rather than the squad, and both count DOWN to zero and then hold
// at or below it rather than cycling - see the reload-timing notes on RangedUnitAttackSystem and the
// matching comment in MageCastSystem. Progress is therefore reported as "how ready am I": 0 the
// instant after acting, 1 when able to act again.
public static class SquadCooldown
{
    /// <summary>
    /// Progress toward being able to act again, 0 to 1, plus the raw seconds left.
    /// False when this squad has no cooldown worth showing, in which case neither out is meaningful.
    /// </summary>
    public static bool TryGet(EntityManager entityManager, Entity squadEntity, UnitType unitType,
        out float progress, out float secondsRemaining)
    {
        progress = 1f;
        secondsRemaining = 0f;

        // Archers hold a ShootAttack timer too, but they reload fast enough that a bar would only
        // ever flicker. The predicate is what keeps this to artillery and casters.
        if (!TabletopTavernConstants.HasVisibleCooldown(unitType)) return false;

        if (!entityManager.Exists(squadEntity)) return false;
        if (!entityManager.HasBuffer<EntityReferenceBufferElement>(squadEntity)) return false;

        DynamicBuffer<EntityReferenceBufferElement> units = entityManager.GetBuffer<EntityReferenceBufferElement>(squadEntity);
        if (units.Length == 0) return false;

        // Unit 0 is the representative. Exact for a mage, which is a single model by design. For
        // artillery it is one gun's cycle rather than the squad's, because RangedUnitAttackSystem
        // deliberately desyncs guns in FireAtWill - chosen over an average because an average never
        // reaches zero in that mode, so the bar would never read as ready. Matches MageCastSystem,
        // which already drives a whole mage squad's casting off units[0].
        Entity unit = units[0].Entity;
        if (!entityManager.Exists(unit)) return false;

        float timer, timerMax;

        if (entityManager.HasComponent<MageCast>(unit))
        {
            MageCast cast = entityManager.GetComponentData<MageCast>(unit);
            timer = cast.Timer;
            timerMax = cast.Cooldown;
        }
        else if (entityManager.HasComponent<ShootAttack>(unit))
        {
            // ShootAttack is IEnableableComponent, so HasComponent stays true after
            // RangedMeleeConverter swaps the weapon on engagement. A disabled one is a stale timer
            // for a unit that is currently swinging a sword, and has nothing to count down to.
            if (!entityManager.IsComponentEnabled<ShootAttack>(unit)) return false;

            ShootAttack shoot = entityManager.GetComponentData<ShootAttack>(unit);
            timer = shoot.timer;
            timerMax = shoot.timerMax;
        }
        else return false;

        if (timerMax <= 0f) return false;

        secondsRemaining = Mathf.Max(0f, timer);
        progress = 1f - Mathf.Clamp01(timer / timerMax);
        return true;
    }
}
