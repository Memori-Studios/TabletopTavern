using System;
using System.Collections;
using UnityEngine;
using Memori.Audio;
using Unity.Mathematics;
using Unity.Entities;
using Shapes;
using TJ.Shapes;

namespace TJ.Spells
{
public class ActiveSpell : MonoBehaviour
{
    [Header("Visual Effect")]
    [SerializeField] private GameObject spellWarmupEffect;
    [SerializeField] private GameObject spellVisualEffect;

    [Header("Battlefield Bonus Display")]
    [SerializeField] private Disc disc1;
    [SerializeField] private Disc disc2;
    [SerializeField] private ParticleSystem particleSystemTransform, particleSystem2, particleSystem3;
    [SerializeField] private Color positiveColor, positiveInnerColor;
    [SerializeField] private Color negativeColor, negativeInnerColor;
    [SerializeField] private AnimationCurve radiusCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve cleanUpRadiusCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private float radiusAnimationDuration = 0.5f;
    [SerializeField] private float flashWarningDuration = 1f;
    [SerializeField] private float minFlashThickness = 0.01f;
    [SerializeField] private float maxFlashThickness = 0.1f;
    [SerializeField] private float flashSpeed = 10f;

    private bool HasBonusDiscs => spellData.GrantsBattlefieldBonus && disc1 != null && disc2 != null;

    private SpellData spellData;
    private Entity targetSquadEntity = Entity.Null;

    public void Load(SpellData _spellData, float3 position, Entity _targetSquadEntity = default)
    {
        spellData = _spellData;
        targetSquadEntity = _targetSquadEntity;
        transform.position = position;

        if(HasBonusDiscs) SetDisplayOfBonus(spellData.SpellModifierValue, spellData.SpellRadius);

        StartCoroutine(WarmUpSpell());
    }
    private void SetDisplayOfBonus(int value, float range)
    {
        bool isPositive = value >= 0;

        if(disc1.TryGetComponent(out ShapesBloom bloom)) bloom.Bloom(isPositive ? positiveColor : negativeColor);

        disc2.ColorOuter = isPositive ? positiveInnerColor : negativeInnerColor;
        var mainModule = particleSystemTransform.main;
        mainModule.startColor = isPositive ? positiveColor : negativeColor;
        var mainModule2 = particleSystem2.main;
        mainModule2.startColor = isPositive ? positiveColor : negativeColor;
        var mainModule3 = particleSystem3.main;
        mainModule3.startColor = isPositive ? positiveColor : negativeColor;
        disc1.Radius = range;
        disc2.Radius = range;
        particleSystemTransform.transform.localScale = new Vector3(range, range, range);
    }
    private IEnumerator AnimateDiscRadius(float range, AnimationCurve curve)
    {
        float elapsed = 0f;
        while(elapsed < radiusAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / radiusAnimationDuration);
            float radius = curve.Evaluate(t) * range;
            disc1.Radius = radius;
            disc2.Radius = radius;
            particleSystemTransform.transform.localScale = new Vector3(radius, radius, radius);
            yield return null;
        }
        float finalRadius = curve.Evaluate(1f) * range;
        disc1.Radius = finalRadius;
        disc2.Radius = finalRadius;
        particleSystemTransform.transform.localScale = new Vector3(finalRadius, finalRadius, finalRadius);
    }
    private IEnumerator FlashDiscThickness()
    {
        float elapsed = 0f;
        while(true)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.PingPong(elapsed * flashSpeed, 1f);
            disc1.Thickness = Mathf.Lerp(minFlashThickness, maxFlashThickness, t);
            yield return null;
        }
    }
    private void Update()
    {
        if(spellData == null) return;

        if(spellData.SpellTargetingType == SpellTargetingType.Squad && targetSquadEntity != Entity.Null) {
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if(entityManager.Exists(targetSquadEntity) && entityManager.HasComponent<SquadMovementComponent>(targetSquadEntity)) {
                transform.position = entityManager.GetComponentData<SquadMovementComponent>(targetSquadEntity).SquadCenter;
            }
        }
    }
    private IEnumerator WarmUpSpell()
    {
        if (spellData.warmupSound) IAudioRequester.Instance.PlaySFX(spellData.warmupSound.sfxKey);
        if (spellWarmupEffect != null) spellWarmupEffect.SetActive(true);
        yield return new WaitForSeconds(spellData.SpellWarmUpDuration);
        // if (spellWarmupEffect != null) spellWarmupEffect.SetActive(false);

        CastSpell();
    }
    private void CastSpell()
    {
        // Debug.Log($"ActiveSpell: {spellData.name} applying effect at {transform.position} (radius={spellData.SpellRadius}, force={spellData.SpellForce}, oneOff={spellData.IsOneOff})");

        if (spellVisualEffect != null) spellVisualEffect.SetActive(true);
        if (spellData.hitSound) IAudioRequester.Instance.PlaySFX(spellData.hitSound.sfxKey);

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var ecb = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().CreateCommandBuffer();

        if (spellData.SummonsSquad)
        {
            // Spawns a real player squad that lasts until killed. No SpellEntity is created, so
            // SpellSystem never sees this cast - the squad is the entire effect. CleanUpSpell still
            // runs below, but that only governs this GameObject's visuals, not the squad's lifetime.
            BattleManager.Instance.ArmySpawnManager.SummonSquad(spellData.SummonedUnitName, transform.position);
        }
        else if (spellData.MarksTarget)
        {
            // Hunter's Mark - no SpellEntity and no damage of its own; just tag the targeted enemy
            // squad so HuntersMarkSystem amplifies all hostile damage to it for SpellDuration seconds.
            // SpellModifierValue is the percent bonus (50 -> x1.5), matching the tooltip's {1} slot.
            if (targetSquadEntity != Entity.Null && entityManager.Exists(targetSquadEntity))
            {
                ecb.AddComponent(targetSquadEntity, new HuntersMarkTag
                {
                    RemainingDuration = spellData.SpellDuration,
                    DamageMultiplier = 1f + spellData.SpellModifierValue / 100f
                });
            }
        }
        else if (spellData.BracesTarget)
        {
            // Shieldwall - brace the targeted friendly squad; ShieldwallSystem applies the knockback
            // immunity + speed penalty and reverses them after SpellDuration. Guard against re-bracing an
            // already-braced squad so the one-time speed halving can't stack.
            if (targetSquadEntity != Entity.Null && entityManager.Exists(targetSquadEntity)
                && !entityManager.HasComponent<ShieldwallTag>(targetSquadEntity))
            {
                ecb.AddComponent(targetSquadEntity, new ShieldwallTag
                {
                    RemainingDuration = spellData.SpellDuration,
                    Applied = false
                });
            }
        }
        else if (spellData.PlacesTrap)
        {
            // Snare Trap - drop a hidden armed trap at the cast point; SnareTrapSystem springs it into a
            // burst + knockback when an enemy wanders into range, or lets it expire after SpellDuration.
            Entity trapEntity = entityManager.CreateEntity();
            ecb.AddComponent(trapEntity, new SnareTrapEntity
            {
                Position = transform.position,
                TriggerRadius = spellData.SpellRadius * 0.4f,
                BlastRadius = spellData.SpellRadius,
                Damage = spellData.SpellModifierValue,
                SpellForce = spellData.SpellForce,
                OwnerTeam = Team.Player,
                RemainingArmedTime = spellData.SpellDuration
            });
        }
        else if (spellData.TeleportsSquad)
        {
            // Starstep - blink the player's selected squad to the cast point (only the first selected
            // player squad). No squad selected means nothing happens.
            UnitSelectionManager selection = BattleManager.Instance.UnitSelectionManager;
            foreach (int squadId in selection.SelectedSquadIds)
            {
                if (squadId <= 0) continue; // player squads have positive ids
                BattleManager.Instance.ArmySpawnManager.TeleportSquadToPoint(squadId, transform.position);
                break;
            }
        }
        else if (spellData.GrantsBattlefieldBonus)
        {
            // Pure buff spell - no damage pipeline at all, just a battlefield-bonus aura
            // functioning exactly like the world's static bonus prefabs (Shrine/Statue/etc.),
            // except it self-expires after SpellDuration instead of lasting forever.
            void CreateBonusApplicator(UnitStat stat, BattlefieldBonusEnum type, float value)
            {
                Entity bonusApplicatorEntity = entityManager.CreateEntity();
                ecb.AddComponent(bonusApplicatorEntity, new BattlefieldBonusApplicator
                {
                    BattlefieldBonus = new BattlefieldBonus
                    {
                        UnitStat = stat,
                        BattlefieldBonusEnum = type,
                        Team = spellData.TargetTeam,
                        Value = value,
                        Guid = Guid.NewGuid(),
                        OriginationPoint = transform.position,
                        Range = spellData.SpellRadius,
                        Applied = false,
                        TargetedUnit = 0
                    },
                    TimerMax = 0.5f,
                    Lifetime = spellData.SpellDuration
                });
            }

            if (spellData.BonusStats != null && spellData.BonusStats.Count > 0)
            {
                // Multi-stat spell: one independent applicator per (stat, value) pair. Each gets a fresh
                // Guid, so BattlefieldBonusApplicationSystem's Guid dedupe lets them coexist on the same
                // squad, and each routes through the generic per-unit stat switch in BattlefieldBonusSystem
                // via the SpellStatBonus enum. Removal is per-stat, so they clear independently too.
                foreach (SpellBonusStat bonusStat in spellData.BonusStats)
                    CreateBonusApplicator(bonusStat.UnitStat, BattlefieldBonusEnum.SpellStatBonus, bonusStat.Value);
            }
            else
            {
                // Single-bonus spell (morale rate, wind, weapon strength, etc.) - the original path,
                // where BonusType selects the apply branch and SpellModifierValue is the magnitude.
                CreateBonusApplicator(spellData.BonusUnitStat, spellData.BonusType, spellData.SpellModifierValue);
            }
        }
        else
        {
            Entity spellEntity = entityManager.CreateEntity();

            // Only the player casts spells right now, so TeamOfSource/DamageSourceSquadId are
            // fixed here rather than left as designer-configurable fields on the SpellData asset.
            // Neutral-targeted spells use Team.Neutral as a wildcard so ApplyDamageSystem's
            // team-alignment checks don't exempt either side (see ApplyDamageSystem.cs).
            DamageBufferElement damageBufferElement = new ()
            {
                DamageType = spellData.HealsInsteadOfDamage ? DamageType.Healing : DamageType.Magical,
                AttackStrength = spellData.SpellModifierValue,
                TeamOfSource = spellData.TargetTeam == Team.Neutral ? Team.Neutral : Team.Player,
                DamageSourceSquadId = 0
            };

            ecb.AddComponent(spellEntity, new SpellEntity {
                Entity = spellEntity,
                DamageBufferElement = damageBufferElement,
                SpellPosition = transform.position,
                SpellRadius = spellData.SpellRadius,
                IsOneOff = spellData.IsOneOff,
                SpellForce = spellData.SpellForce,
                RemainingDuration = spellData.SpellDuration,
                TargetSquadEntity = targetSquadEntity, // Entity.Null unless this is a Squad-targeted cast
                TickInterval = spellData.TickInterval,
                TickTimer = 0f // first tick fires immediately, then every TickInterval seconds
            });
        }

        StartCoroutine(CleanUpSpell());
    }
    private IEnumerator CleanUpSpell()
    {
        float leadTime = Mathf.Max(0f, spellData.SpellDuration - flashWarningDuration);
        yield return new WaitForSeconds(leadTime);

        Coroutine flashCoroutine = null;
        if(HasBonusDiscs) flashCoroutine = StartCoroutine(FlashDiscThickness());

        yield return new WaitForSeconds(spellData.SpellDuration - leadTime);

        if(flashCoroutine != null) StopCoroutine(flashCoroutine);

        if(HasBonusDiscs) yield return AnimateDiscRadius(spellData.SpellRadius, cleanUpRadiusCurve);

        Destroy(gameObject);
    }
}
}
