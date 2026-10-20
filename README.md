# Card Battle – Unity prototype (placeholder graphics, procedural animation and sound)

Target: Unity 2021.3 LTS or newer (incl. Unity 6), Built-in or URP. No external assets needed.

## Quick start
1. Copy the `CardBattle` folder into your project's `Assets/` folder.
2. Project Settings > Player > Other Settings > **Active Input Handling = Both** (or Input Manager (Old)).
3. **Tools > Card Battle > Build Prototype Scene** creates all prefabs, 20 CardData assets and `Assets/CardBattle/Scenes/Battle.unity`, fully wired.
   Re-running overwrites the generated assets (copy any you edit).
4. Set the Game view to a portrait ratio (9:16), press Play.

## Controls
Tap/click a card, then click/tap your half of the arena (release to deploy), or drag a card onto the arena.
Keys: 1-8 select, Q/E cycle, Esc deselect, M mute. Camera: right/middle-drag pan, wheel zoom, arrows pan, F reset; touch: two-finger pan/pinch.

## Roster (all original)
| Card | Type | Ability |
|---|---|---|
| Vanguard | melee knight | Iron Guard: shield absorbs the first 150 damage |
| Sprinter (x2) | fast melee | fast and fragile |
| Ranger | archer | long range, hits air |
| Bulwark | heavy tank | only attacks buildings; splits into 2 Pebblelings on death |
| Gale Wisp | flyer | ignores the river; heals for 50% of damage dealt |
| Emberseer | ranged magic | area-damage bolts |
| Ridgeback Lancer | melee | Charge: after a 6-unit run-up, faster and first hit deals x2 |
| Powder Imp | melee | suicide bomber: explodes on first hit or when killed |
| Dawn Cleric | support | Heal aura: +45 HP/s to nearby allies |
| Stormcaller | ranged magic | Chain lightning: jumps to 2 more enemies at 60% |
| Frost Warden | melee | slows on hit; every 3rd hit stuns |
| Drum Captain | melee | Rage aura: allies +30% speed, +25% damage |
| Bolt Tower | building | defensive turret (decays after 40 s) |
| Scrapper Den | building | barracks: spawns a Scrapper every 4.5 s |
| Ember Burst / Frost Field / Verdant Pulse / Thunder Snare | spells | area damage / slow / heal / damage + stun |
Pebbleling and Scrapper are minion cards spawned by abilities/buildings (not meant for a hand).
Default player deck: Vanguard, Ranger, Bulwark, Gale Wisp, Lancer, Stormcaller, Dawn Cleric, Ember Burst.
AI deck: Frost Warden, Sprinter, Powder Imp, Emberseer, Drum Captain, Bolt Tower, Thunder Snare, Verdant Pulse.
Swap any CardData into `DeckManager` / `AIDeckManager` to change decks.

## Hierarchy (built for you by the tool)
| GameObject | Components |
|---|---|
| Main Camera (tag MainCamera) | Camera, AudioListener, BattleCameraController |
| GameSystems | GameManager (Overlay Material), MatchManager, ResourceManager, CardManager, AudioManager |
| Arena | ArenaLayout |
| Towers/* (6) | Tower |
| PlayerControl | DeckManager (Deck = 8 cards), PlayerController |
| EnemyAI | AIDeckManager (Deck = 8 cards), AIPlayer |
| BattleUI | BattleUI |

## Prefab rules
- Unit: root = MeleeUnit / RangedUnit / FlyingUnit (+ any UnitAbility components); child "Visual" holds the model and a `UnitAnimator`. Never rotate the root.
  Parts named `Tint_*` take the team colour. Ranged units take `Projectile Prefab` + `Muzzle`; `Attack Sfx` picks the attack sound.
- Building: Building / DefensiveBuilding / SpawnerBuilding (+ Visual, Turret, Muzzle, Projectile Prefab).
- Spell: DamageSpell / SlowSpell / HealSpell / StunSpell + `Area Visual` disc and `Fx Color`.
- CardData: `Prefab` must carry the component matching `Card Type`.

## Animation (UnitAnimator)
Procedural, so primitive characters move without clips: idle breathing, distance-based walk cycle, five attack styles
(Swing, Thrust, Shoot, Cast, Slam), hit reaction, stun wobble, wing flapping, death (topple or spin-fall), spawn pop.
Rig = pivots `body, head, armL, armR, legL, legR` (limbs hang down from their pivot; any can be empty).
Imported models: assign an `Animator` and it receives `Speed` (float), `Attack`/`Hit` (triggers), `Dead` (bool) if those parameters exist.
Buildings pop up, towers recoil and flash, projectiles trail, spells animate their area, UI punches/pops, camera shakes on blasts.

## Abilities (Units/Abilities)
Add one or more components to a unit prefab. New ability: derive from `UnitAbility` and override only the hooks you need
(`SpeedMultiplier`, `ModifyOutgoingDamage`, `ModifyIncomingDamage`, `OnMoved`, `OnAttack`, `OnOwnerDeath`).
Included: Charge, Shield, LifeSteal, OnHitDebuff (slow/stun), DeathBurst, SpawnOnDeath, Aura (heal/rage), ChainAttack.
Status effects on Unit: `ApplySlow`, `ApplyStun`, `ApplyBuff`, `SelfDestruct`.

## Sound (Audio/)
Every effect and a looping battle theme are synthesised at runtime by `SfxSynth` (no audio files). Throttled voice pool.
To use your own audio: add entries to `AudioManager > Overrides` (SfxId -> AudioClip). Mute with the HUD button or M.

## Online 1v1 multiplayer
Host a match on your laptop and let a friend join from another computer. Install the **Netcode for GameObjects** package, run
**Tools > Card Battle > Build Prototype Scene**, open `Menu.unity` and press Play. Full guide (setup, one-laptop testing, port forwarding or
Tailscale, troubleshooting, what is replicated): see **MULTIPLAYER.md**.

## Build a game / make an installer
1. **Tools > Card Battle > Fix Build Settings** (shaders, portrait, Android IL2CPP/ARM64/API 24). Fixes pink/purple screens in builds.
2. **Tools > Card Battle > Build Windows (.exe)** (or **Build Android (.apk)**). Output goes to `<project>/Builds/`.
3. Windows installer: install Inno Setup (jrsoftware.org), copy `Installer~/CardBattle.iss` to `<project>/Installer/CardBattle.iss`, open it, press Build > Compile. Result: `Installer/Output/CardBattle-Setup.exe`.
4. Android: the `.apk` is the installable file (copy to the phone, open it). For Google Play build an `.aab` instead (File > Build Profiles > Android > Build App Bundle).
Background colour around the arena: Main Camera > Battle Camera Controller > Background Color.

## Card artwork
Every card face shows a 2D illustration of its character/building/spell, so you can tell cards apart at a glance.
The 20 PNGs live in `Resources/CardArt` and are matched to cards by file name (`Card_Vanguard.png` <-> the `Card_Vanguard` CardData asset); nothing to assign.
To use your own art for a card, drop a sprite into its `Icon` field (it wins over the PNG). `Art~/make_card_art.py` regenerates/tweaks the included art (needs Pillow).

## Random decks (no dragging cards into the scene)
On Play, `DeckManager` and `AIDeckManager` draw a fresh random 8-card deck from the **card pool** = every CardData in a
`Resources/Cards` folder (`Assets/CardBattle/Resources/Cards`). Rules (best effort): 1-2 spells, at most 2 buildings, at least one anti-air card.
Summoned minions have `In Deck Pool` off, so they never show up in a hand.
Turn `Random Deck` off on the component to use the fixed Deck list instead. Set `Hand Size` to 4 for a rotating draw from the 8.
Updating an older build? Run **Tools > Card Battle > Fix Card Pool (move cards to Resources)**.

## Add a new card (no code)
Create > Card Battle > Card, set stats, assign a prefab with the matching component, and save it in a `Resources/Cards` folder: it joins the random pool automatically.
