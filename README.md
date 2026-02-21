# EliteMobs — Save Corruption Isolation Test

Standalone V Rising mod that promotes random mobs into elite variants (Champion, Warlord, Apex) with stat scaling, visual auras, and combat affixes.

## The Problem

This system causes **save file corruption** in V Rising. After autosave, the server crashes on next boot because the persistence system can't deserialize entities with modified archetypes.

## Suspected Corruption Sources

All suspected operations involve **archetype changes** — adding or removing ECS components from entities, which changes how the serializer handles them:

| Feature Toggle | What It Does | Why It's Suspected |
|---|---|---|
| `AddDontSaveToMob` | Adds `DontSaveEntity` to the mob entity | Archetype change on mob |
| `ApplyTierAura` | Creates buff entity via `DebugEventsSystem.ApplyBuff` | New buff entity + modifications |
| `StripBuffTriggers` | Removes 5 components from buff entity (`RemoveBuffOnGameplayEvent`, etc.) | Archetype changes on buff entity |
| `AddDontSaveToBuffs` | Adds `DontSaveEntity` to buff entities | Archetype change on buff entity |
| `ApplyBolstering` | Creates Sentinel Level Aura buff entity | New buff entity + modifications |
| `EnableShielded` | Applies Gargoyle WingShield buff periodically | Buff entity creation in tick loop |
| `EnablePhasing` | Applies Stealth buff periodically | Buff entity creation in tick loop |

**SAFE operations** (value-only writes, no archetype changes) that should NOT cause corruption:
- `ScaleStats` — writes to `Health.MaxHealth._Value`, `UnitStats.PhysicalPower._Value`
- `ApplyFrenzied` — writes to `AbilityBar_Shared` values
- `ApplyIronhide` — writes to `UnitStats.DamageReduction._Value`
- `UpdateBerserker` — writes to `UnitStats` values per tick

## How to Test

1. Build and install the mod (requires VampireCommandFramework)
2. All feature toggles default to **true** (everything enabled)
3. Edit `BepInEx/config/spence.EliteMobs.cfg` to disable specific features
4. Test procedure:
   - Boot server fresh
   - Run around to let mobs spawn (or use `.em champion` / `.em debug apex vampiric,thorns`)
   - Let at least one autosave cycle complete
   - Restart server
   - If server boots clean → the disabled feature was the problem
   - If server crashes → the corruption source is still active

## Recommended Test Order

1. **All features ON** — confirm corruption still happens
2. **Disable ALL toggles** (only stat scaling active) — confirm clean boot
3. **Enable one at a time** starting with `AddDontSaveToMob`
4. Then `ApplyTierAura` alone
5. Then `StripBuffTriggers` alone
6. Then `AddDontSaveToBuffs` alone
7. Then buff-applying affixes (`Bolstering`, `Shielded`, `Phasing`)

## Commands

| Command | Description |
|---|---|
| `.em champion` | Promote nearest mob to Champion |
| `.em warlord` | Promote nearest mob to Warlord |
| `.em apex` | Promote nearest mob to Apex |
| `.em debug [tier] [affix,affix]` | Force specific tier + affixes |
| `.em info` | Show info on nearest elite |
| `.em count` | Show active elite count |
| `.em nearby` | List elites within 50 units |
| `.em demote` | Demote nearest elite |
| `.em purge` | Revert ALL elites server-wide |
| `.em toggles` | Show current feature toggle states |
| `.em affixes` | List available affixes |

## Context

This mod was extracted from Ordain (Spence's larger V Rising mod). The identical stat modification pattern used in AscensionZones (another mod) ran for 6 months with zero corruption — but AZ only does value-writes, never adds/removes components. The elite system does both.
