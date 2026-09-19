using System;
using System.Collections;
using UnityEngine;
using Memori.Audio;
using Unity.Mathematics;
using Unity.Entities;
using Shapes;
using UnityEngine.Serialization;

namespace TJ.Spells
{
public class ActiveSpell : MonoBehaviour
{
    [Header("Area Display")]
    // Everything here takes the spell's race colour, the same one its cast button and bar icon use.
    // areaScaleRoot is scaled to SpellRadius (the dust and edge particles sit under it). areaDisc is a
    // soft radial band whose outer edge sits on SpellRadius and whose colour ramps up over the outer
    // bandFraction of the radius. The whole set grows in on load, pulses between flashMinScale and full
    // (radius, band, particle ring and alpha together) for the last flashWarningDuration seconds and
    // shrinks out. Either may be unassigned on a prefab.
    [SerializeField] private Transform areaScaleRoot;
    [SerializeField] private Disc areaDisc;
    [SerializeField, Range(0.05f, 1f)] private float bandFraction = 0.25f;
    [SerializeField] private AnimationCurve growCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve shrinkCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private float radiusAnimationDuration = 0.5f;
    [SerializeField] private float flashWarningDuration = 1f;
    [SerializeField, FormerlySerializedAs("flashMinThicknessScale")] private float flashMinScale = 0.4f;
    [SerializeField] private float flashSpeed = 10f;

    private Color areaColor;
    // The spell's own art from SpellData.SpellVisualPrefab, spawned under this root in Load.
    private SpellVisualAddon visualAddon;
    private ParticleSystem[] areaParticles = System.Array.Empty<ParticleSystem>();
    private ParticleSystem.Particle[] particleScratch;

    private SpellData spellData;
    private Entity targetSquadEntity = Entity.Null;

    // Who cast this. Defaulted to the player casting from the hotbar, which was the only caster
    // when this class was written and used to be hardcoded down in CastSpell. Mage units pass their
    // own team and squad id, which is what lets an enemy mage damage the player rather than its own
    // army, and what gets a mage's kills credited instead of landing on the "no killer" sentinel.
    private Team sourceTeam = Team.Player;
    private int sourceSquadId = 0;

    // Placement spells (Starstep, Raise Dead): the formation the player drew before confirming.
    // Null for every other cast. The summon branch spawns onto it; the teleport branch has already
    // been applied by SpellManager at placement time and only plays the visuals here.
    private SpellPlacement placement;
    private bool effectHandledByCaster;

    // Snare Trap: the cast visuals wait for SnareTrapSystem to spring the trap. The entity vanishing
    // before its armed time is up means it sprung; the spell then lingers TrapSprungLinger seconds.
    private Entity trapEntity = Entity.Null;
    private float trapArmedUntil;
    private const float TrapSprungLinger = 2f;
    private Coroutine cleanUpCoroutine;

    /// <summary>
    /// TargetTeam on a SpellData is authored from the caster's point of view - Enemy means "whoever
    /// the caster is fighting". For a player cast that resolves to itself, but an enemy mage has to
    /// flip it or its Smite would land on the enemy army. Neutral is a wildcard and never flips.
    /// </summary>
    private Team ResolvedTargetTeam
    {
        get
        {
            if (spellData.TargetTeam == Team.Neutral) return Team.Neutral;
            if (sourceTeam != Team.Enemy) return spellData.TargetTeam;
            return spellData.TargetTeam == Team.Enemy ? Team.Player : Team.Enemy;
        }
    }

    public void Load(SpellData _spellData, float3 position, Entity _targetSquadEntity = default,
                     Team _sourceTeam = Team.Player, int _sourceSquadId = 0,
                     SpellPlacement _placement = null, bool _effectHandledByCaster = false)
    {
        spellData = _spellData;
        targetSquadEntity = _targetSquadEntity;
        sourceTeam = _sourceTeam;
        sourceSquadId = _sourceSquadId;
        placement = _placement;
        effectHandledByCaster = _effectHandledByCaster;
        transform.position = position;

        if (spellData.SpellVisualPrefab != null)
        {
            GameObject visual = Instantiate(spellData.SpellVisualPrefab, transform);
            visual.transform.localPosition = Vector3.zero;
            visualAddon = visual.GetComponent<SpellVisualAddon>();
            if (visualAddon != null)
            {
                if (visualAddon.warmupEffect != null) visualAddon.warmupEffect.SetActive(false);
                if (visualAddon.castEffect != null) visualAddon.castEffect.SetActive(false);
                if (visualAddon.authoredRadius > 0f) visual.transform.localScale = Vector3.one * (spellData.SpellRadius / visualAddon.authoredRadius);
            }
        }
        SetAreaDisplay(spellData.SpellRadius);

        StartCoroutine(WarmUpSpell());
    }
    private void SetAreaDisplay(float range)
    {
        areaColor = ColorData.GetRaceDisplayColor(spellData.Race);
        areaParticles = GetComponentsInChildren<ParticleSystem>(true);
        if (visualAddon != null && visualAddon.keepOwnColors)
            areaParticles = Array.FindAll(areaParticles, p => !p.transform.IsChildOf(visualAddon.transform));
        if (areaDisc != null)
        {
            areaDisc.Type = DiscType.Ring;
            areaDisc.ColorInner = new Color(areaColor.r, areaColor.g, areaColor.b, 0f);
        }
        ApplyAreaState(range, 0f);
        StartCoroutine(AnimateAreaSize(range, growCurve));
    }
    // One t for everything so the disc edge, the band, the particle ring and the alpha always agree:
    // disc radius and thickness, the root scale and the alpha of the disc and every particle.
    private void ApplyAreaState(float range, float t)
    {
        float alpha = Mathf.Clamp01(t);
        if (areaDisc != null)
        {
            float thickness = range * bandFraction * t;
            areaDisc.Thickness = thickness;
            areaDisc.Radius = range * t - thickness * 0.5f;
            areaDisc.ColorOuter = new Color(areaColor.r, areaColor.g, areaColor.b, alpha);
        }
        if (areaScaleRoot != null)
        {
            float scale = range * t;
            areaScaleRoot.localScale = new Vector3(scale, scale, scale);
        }
        SetParticleAlpha(alpha);
    }
    // startColor only reaches particles emitted from now on, so the live ones are rewritten too.
    private void SetParticleAlpha(float alpha)
    {
        Color color = new Color(areaColor.r, areaColor.g, areaColor.b, alpha);
        foreach (ParticleSystem particleSystem in areaParticles)
        {
            var main = particleSystem.main;
            main.startColor = color;
            int count = particleSystem.particleCount;
            if (count == 0) continue;
            if (particleScratch == null || particleScratch.Length < count) particleScratch = new ParticleSystem.Particle[Mathf.Max(count, 256)];
            int live = particleSystem.GetParticles(particleScratch);
            for (int i = 0; i < live; i++) particleScratch[i].startColor = color;
            particleSystem.SetParticles(particleScratch, live);
        }
    }
    private IEnumerator AnimateAreaSize(float range, AnimationCurve curve)
    {
        float elapsed = 0f;
        while (elapsed < radiusAnimationDuration)
        {
            elapsed += Time.deltaTime;
            ApplyAreaState(range, curve.Evaluate(Mathf.Clamp01(elapsed / radiusAnimationDuration)));
            yield return null;
        }
        ApplyAreaState(range, curve.Evaluate(1f));
    }
    // Pulses from full size down and back, starting at 1 so the first frame is continuous with the
    // steady state, and once releaseFlash is set ends on the next full-size pass so the shrink that
    // follows starts from 1 as well.
    private bool releaseFlash;
    private IEnumerator FlashArea(float range)
    {
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float phase = 1f - Mathf.PingPong(elapsed * flashSpeed, 1f);
            if (releaseFlash && phase > 0.98f)
            {
                ApplyAreaState(range, 1f);
                yield break;
            }
            ApplyAreaState(range, Mathf.Lerp(flashMinScale, 1f, phase));
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
        if (trapEntity != Entity.Null && !World.DefaultGameObjectInjectionWorld.EntityManager.Exists(trapEntity))
        {
            bool sprung = Time.time < trapArmedUntil - 0.5f;
            trapEntity = Entity.Null;
            if (sprung)
            {
                ShowCastVisuals();
                if (cleanUpCoroutine != null) StopCoroutine(cleanUpCoroutine);
                cleanUpCoroutine = StartCoroutine(CleanUpSpell(TrapSprungLinger));
            }
        }
    }
    private IEnumerator WarmUpSpell()
    {
        IAudioRequester.Instance.Play(spellData.warmupSound, transform.position, ignoreDucking: true);
        if (visualAddon != null && visualAddon.warmupEffect != null) visualAddon.warmupEffect.SetActive(true);
        yield return new WaitForSeconds(spellData.SpellWarmUpDuration);

        CastSpell();
    }
    private void CastSpell()
    {
        // Debug.Log($"ActiveSpell: {spellData.name} applying effect at {transform.position} (radius={spellData.SpellRadius}, force={spellData.SpellForce}, oneOff={spellData.IsOneOff})");

        if (!spellData.PlacesTrap) ShowCastVisuals();

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var ecb = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().CreateCommandBuffer();

        // 0 = no icon. Registered here so unit-cast spells outside SpellRegistry still resolve a sprite.
        int statusSpellId = spellData.ShowsStatusIcon ? (int)spellData.Spell : 0;
        if (statusSpellId > 0) SpellStatusIcons.Register(spellData);

        if (effectHandledByCaster)
        {
            // Starstep through the placement flow: SpellManager already teleported the squad onto the
            // drawn formation when the player confirmed it. This instance is the visual only.
        }
        else if (spellData.SummonsSquad)
        {
            // Spawns a real player squad that lasts until killed. No SpellEntity is created, so
            // SpellSystem never sees this cast - the squad is the entire effect. CleanUpSpell still
            // runs below, but that only governs this GameObject's visuals, not the squad's lifetime.
            //
            // SummonSquad only ever spawns for the player: it takes a positive squad id (9000+) and
            // EntityWatcher keys PlayerSquad off id > 0. An enemy caster would summon a squad that
            // fights for the player, so this stays player-only until enemy summoning is built.
            if (sourceTeam == Team.Enemy)
                Debug.LogError($"ActiveSpell: '{spellData.name}' summons, but enemy summoning is not supported - cast ignored.", spellData);
            else if (placement != null)
                BattleManager.Instance.ArmySpawnManager.SummonSquad(spellData.SummonedUnitName, placement);
            else
                BattleManager.Instance.ArmySpawnManager.SummonSquad(spellData.SummonedUnitName, transform.position);
        }
        else if (spellData.MarksTarget)
        {
            // Hunter's Mark - no SpellEntity and no damage of its own; just tag the targeted enemy
            // squad so HuntersMarkSystem amplifies all hostile damage to it for SpellDuration seconds.
            // SpellModifierValue is the percent bonus (50 -> x1.5), matching the tooltip's {1} slot.
            // A re-cast on an already-marked squad refreshes the mark rather than stacking it, so the
            // duration may legitimately outlast the cooldown (it does since the 10 s flatten).
            if (targetSquadEntity != Entity.Null && entityManager.Exists(targetSquadEntity))
            {
                HuntersMarkTag mark = new()
                {
                    RemainingDuration = spellData.SpellDuration,
                    DamageMultiplier = 1f + spellData.SpellModifierValue / 100f
                };
                if (entityManager.HasComponent<HuntersMarkTag>(targetSquadEntity))
                    ecb.SetComponent(targetSquadEntity, mark);
                else
                    ecb.AddComponent(targetSquadEntity, mark);
                WriteSquadStatus(entityManager, targetSquadEntity, statusSpellId);
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
                WriteSquadStatus(entityManager, targetSquadEntity, statusSpellId);
            }
        }
        else if (spellData.PlacesTrap)
        {
            // Snare Trap - drop a hidden armed trap at the cast point; SnareTrapSystem springs it into a
            // burst + knockback when an enemy wanders into range, or lets it expire after SpellDuration.
            trapEntity = entityManager.CreateEntity();
            trapArmedUntil = Time.time + spellData.SpellDuration;
            ecb.AddComponent(trapEntity, new SnareTrapEntity
            {
                Position = transform.position,
                TriggerRadius = spellData.SpellRadius * 0.4f,
                BlastRadius = spellData.SpellRadius,
                Damage = spellData.SpellModifierValue,
                SpellForce = spellData.SpellForce,
                OwnerTeam = sourceTeam,
                RemainingArmedTime = spellData.SpellDuration
            });
        }
        else if (spellData.TeleportsSquad)
        {
            // Starstep - blink the player's selected squad to the cast point (only the first selected
            // player squad). No squad selected means nothing happens.
            //
            // Reads the live player selection, which an enemy caster has no equivalent of, so this
            // is player-only. An enemy mage casting it would silently teleport a PLAYER squad.
            if (sourceTeam == Team.Enemy)
            {
                Debug.LogError($"ActiveSpell: '{spellData.name}' teleports the selected squad, which is player-only - enemy cast ignored.", spellData);
                cleanUpCoroutine = StartCoroutine(CleanUpSpell(spellData.SpellDuration));
                return;
            }
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
                        // Resolved, not raw: an enemy mage's "buff my side" has to land on the
                        // enemy side, and its debuff on the player's.
                        Team = ResolvedTargetTeam,
                        Value = value,
                        Guid = Guid.NewGuid(),
                        OriginationPoint = transform.position,
                        Range = spellData.SpellRadius,
                        Applied = false,
                        TargetedUnit = 0,
                        StatusSpellId = statusSpellId
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

            // TeamOfSource and DamageSourceSquadId come from whoever cast this - the hotbar passes
            // the player and squad 0, a mage unit passes its own team and squad id. They used to be
            // hardcoded to Player/0, which meant an enemy mage would have damaged its own army and
            // no mage kill could ever be credited.
            // Neutral-targeted spells still use Team.Neutral as a wildcard so ApplyDamageSystem's
            // team-alignment checks don't exempt either side (see ApplyDamageSystem.cs).
            DamageBufferElement damageBufferElement = new ()
            {
                DamageType = spellData.HealsInsteadOfDamage ? DamageType.Healing : DamageType.Magical,
                // Load-bearing: an element with no source defaults to Melee and takes the 0.25 melee
                // knob in ApplyDamageSystem, which is how every spell landed at a quarter until TT-78.
                DamageSource = DamageSource.Spell,
                AttackStrength = spellData.SpellModifierValue,
                TeamOfSource = spellData.TargetTeam == Team.Neutral ? Team.Neutral : sourceTeam,
                DamageSourceSquadId = sourceSquadId
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
                TickTimer = 0f, // first tick fires immediately, then every TickInterval seconds
                HitsSingleUnit = spellData.HitsSingleUnit,
                StatusSpellId = statusSpellId
            });
        }

        cleanUpCoroutine = StartCoroutine(CleanUpSpell(spellData.SpellDuration));
    }
    // The addon's cast art and the hit sound; every spell but the trap plays them the moment it lands.
    private void ShowCastVisuals()
    {
        if (visualAddon != null && visualAddon.castEffect != null) visualAddon.castEffect.SetActive(true);
        if (visualAddon != null && visualAddon.hideWarmupOnCast && visualAddon.warmupEffect != null) visualAddon.warmupEffect.SetActive(false);
        IAudioRequester.Instance.Play(spellData.hitSound, transform.position, ignoreDucking: true);
    }
    // Tag spells (Mark, Shieldwall) expire on their own tag timer; the status entry mirrors that length.
    private void WriteSquadStatus(EntityManager entityManager, Entity squadEntity, int statusSpellId)
    {
        if (statusSpellId <= 0 || !entityManager.HasBuffer<SpellStatusBufferElement>(squadEntity)) return;
        double now = World.DefaultGameObjectInjectionWorld.Time.ElapsedTime;
        SpellStatus.Set(entityManager.GetBuffer<SpellStatusBufferElement>(squadEntity), statusSpellId, now + spellData.SpellDuration, now);
    }

    private IEnumerator CleanUpSpell(float duration)
    {
        float leadTime = Mathf.Max(0f, duration - flashWarningDuration);
        yield return new WaitForSeconds(leadTime);

        Coroutine flashCoroutine = StartCoroutine(FlashArea(spellData.SpellRadius));
        // Looping addon art drains over the warning second instead of cutting at Destroy; one-shots run out on their own.
        if (visualAddon != null)
            foreach (ParticleSystem addonSystem in visualAddon.GetComponentsInChildren<ParticleSystem>())
                if (addonSystem.main.loop) addonSystem.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        yield return new WaitForSeconds(duration - leadTime);

        releaseFlash = true;
        yield return flashCoroutine;
        yield return AnimateAreaSize(spellData.SpellRadius, shrinkCurve);

        Destroy(gameObject);
    }
}
}
