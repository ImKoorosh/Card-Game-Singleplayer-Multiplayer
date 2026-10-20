#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using CardBattle;

/// <summary>
/// The playable roster: 12 characters + 2 summoned minions, 2 buildings, 4 spells, all built from primitives with a
/// bone-like rig (pivots) that UnitAnimator animates. Each character is original, in a medieval-fantasy arena style.
/// </summary>
public static partial class CardBattleSetup
{
    class Roster { public Projectile Orb; public CardData[] PlayerDeck, AiDeck; }

    class Look
    {
        public Color torso = Color.white;
        public bool tintTorso = true;                  // torso takes the team colour
        public Color legs = new Color(0.25f, 0.25f, 0.3f);
        public Color skin = new Color(0.92f, 0.78f, 0.65f);
        public float scale = 1f, width = 1f;
        public string headgear = "none";               // helmet, hood, hat, horns, none
        public Color gear = new Color(0.6f, 0.6f, 0.66f);
        public bool tintGear;
        public string weapon = "none";                 // sword, dagger, spear, hammer, staff, bow, none
        public Color weaponColor = new Color(0.82f, 0.84f, 0.9f);
        public bool shield;
        public string extra = "none";                  // cape, bomb, halo, drum, core
    }

    struct Rig { public Transform body, head, armL, armR, legL, legR; }

    static readonly Color Gold = new Color(0.95f, 0.78f, 0.25f);
    static readonly Color Wood = new Color(0.5f, 0.33f, 0.18f);
    static readonly Color Stone = new Color(0.52f, 0.52f, 0.58f);

    // ------------------------------------------------------------------ roster

    static Roster BuildRoster(Projectile towerOrb, Projectile arrow)
    {
        Projectile orb = towerOrb;
        Roster r = new Roster { Orb = orb };
        UnitAnimator.AttackStyle Swing = UnitAnimator.AttackStyle.Swing, Thrust = UnitAnimator.AttackStyle.Thrust,
            Shoot = UnitAnimator.AttackStyle.Shoot, Cast = UnitAnimator.AttackStyle.Cast, Slam = UnitAnimator.AttackStyle.Slam;

        // ---- minions first: other prefabs reference their cards ----
        GameObject pebble = BuildCharacter("Unit_Pebbleling", typeof(MeleeUnit),
            new Look { torso = new Color(0.55f, 0.53f, 0.5f), tintTorso = false, legs = new Color(0.45f, 0.43f, 0.4f), skin = new Color(0.6f, 0.58f, 0.55f), scale = 0.6f, width = 1.3f, extra = "core" },
            1.3f, 0.8f, Swing, SfxId.SwordHit, null, null);
        CardData cPebble = MakeCard("Card_Pebbleling", "Pebbleling", "A living chip of rock. Released when a Bulwark falls.", 1, CardType.Unit, Rarity.Common,
            new Color(0.6f, 0.58f, 0.55f), pebble, new CardStats { health = 170, damage = 32, attackRange = 0.45f, attackSpeed = 1.1f, moveSpeed = 2.8f, detectionRange = 4f, bodyRadius = 0.32f }, null);

        GameObject scrapper = BuildCharacter("Unit_Scrapper", typeof(MeleeUnit),
            new Look { torso = Color.white, legs = new Color(0.3f, 0.25f, 0.15f), skin = new Color(0.5f, 0.75f, 0.35f), scale = 0.65f, headgear = "hood", gear = new Color(0.4f, 0.3f, 0.2f), weapon = "dagger" },
            1.4f, 0.85f, Thrust, SfxId.SwordHit, null, null);
        CardData cScrapper = MakeCard("Card_Scrapper", "Scrapper", "Scrappy little fighter from the Den.", 1, CardType.Unit, Rarity.Common,
            new Color(0.5f, 0.75f, 0.35f), scrapper, new CardStats { health = 110, damage = 30, attackRange = 0.4f, attackSpeed = 1.4f, moveSpeed = 3.4f, detectionRange = 4f, bodyRadius = 0.32f }, null);

        NotInPool(cPebble); // summoned-only cards never appear in random decks
        NotInPool(cScrapper);

        // ---- deck characters ----
        GameObject vanguard = BuildCharacter("Unit_Vanguard", typeof(MeleeUnit),
            new Look { headgear = "helmet", weapon = "sword", shield = true },
            2.0f, 1.2f, Swing, SfxId.SwordHit, null,
            root => { Component a = root.AddComponent<ShieldAbility>(); SetFloat(a, "shieldHealth", 150f); });
        CardData cVanguard = MakeCard("Card_Vanguard", "Vanguard", "Shielded knight. Iron Guard absorbs the first 150 damage.", 3, CardType.Unit, Rarity.Common,
            new Color(0.4f, 0.6f, 0.95f), vanguard, new CardStats { health = 650, damage = 75, attackRange = 0.5f, attackSpeed = 0.9f, moveSpeed = 2.4f, detectionRange = 5f, bodyRadius = 0.55f }, null);

        GameObject sprinter = BuildCharacter("Unit_Sprinter", typeof(MeleeUnit),
            new Look { legs = new Color(0.55f, 0.2f, 0.2f), scale = 0.7f, headgear = "hood", gear = new Color(0.25f, 0.25f, 0.3f), weapon = "dagger" },
            1.5f, 0.9f, Thrust, SfxId.SwordHit, null, null);
        CardData cSprinter = MakeCard("Card_Sprinter", "Sprinter", "Two fast, fragile skirmishers.", 2, CardType.Unit, Rarity.Common,
            new Color(0.95f, 0.8f, 0.3f), sprinter, new CardStats { health = 130, damage = 40, attackRange = 0.4f, attackSpeed = 1.6f, moveSpeed = 4.2f, detectionRange = 4f, bodyRadius = 0.36f, spawnCount = 2 }, null);

        GameObject ranger = BuildCharacter("Unit_Ranger", typeof(RangedUnit),
            new Look { legs = new Color(0.3f, 0.4f, 0.25f), scale = 0.95f, headgear = "hood", gear = new Color(0.25f, 0.45f, 0.3f), weapon = "bow" },
            1.95f, 1.0f, Shoot, SfxId.ArrowShot, arrow, null);
        CardData cRanger = MakeCard("Card_Ranger", "Ranger", "Shoots ground and air targets from a distance.", 3, CardType.Unit, Rarity.Common,
            new Color(0.4f, 0.85f, 0.5f), ranger, new CardStats { health = 260, damage = 55, attackRange = 5.5f, attackSpeed = 1f, moveSpeed = 2.6f, detectionRange = 6.5f, bodyRadius = 0.4f, canTargetAir = true, projectileSpeed = 14f }, null);

        GameObject bulwark = BuildCharacter("Unit_Bulwark", typeof(MeleeUnit),
            new Look { torso = new Color(0.5f, 0.5f, 0.55f), tintTorso = false, legs = new Color(0.4f, 0.4f, 0.45f), skin = new Color(0.55f, 0.55f, 0.6f), scale = 1.75f, width = 1.5f, extra = "core" },
            3.3f, 1.7f, Slam, SfxId.Deploy, null,
            root => { Component a = root.AddComponent<SpawnOnDeathAbility>(); SetObj(a, "minionCard", cPebble); SetInt(a, "count", 2); });
        CardData cBulwark = MakeCard("Card_Bulwark", "Bulwark", "Stone colossus. Only attacks buildings. Splits into two Pebblelings when destroyed.", 5, CardType.Unit, Rarity.Rare,
            new Color(0.6f, 0.6f, 0.65f), bulwark, new CardStats { health = 1900, damage = 110, attackRange = 0.7f, attackSpeed = 0.6f, moveSpeed = 1.5f, detectionRange = 4f, bodyRadius = 0.95f, targetPriority = TargetPriority.BuildingsOnly }, null);

        GameObject wisp = BuildWisp(root => { Component a = root.AddComponent<LifeStealAbility>(); SetFloat(a, "fraction", 0.5f); });
        CardData cWisp = MakeCard("Card_GaleWisp", "Gale Wisp", "Flies over the river. Siphons half the damage it deals as health.", 4, CardType.Unit, Rarity.Rare,
            new Color(0.6f, 0.9f, 1f), wisp, new CardStats { health = 320, damage = 80, attackRange = 1.2f, attackSpeed = 1.1f, moveSpeed = 3.2f, detectionRange = 5f, bodyRadius = 0.5f, canTargetAir = true }, null);

        GameObject seer = BuildCharacter("Unit_Emberseer", typeof(RangedUnit),
            new Look { torso = new Color(0.4f, 0.22f, 0.55f), tintTorso = false, legs = new Color(0.3f, 0.18f, 0.4f), headgear = "hat", gear = new Color(0.4f, 0.22f, 0.55f), tintGear = true, weapon = "staff", weaponColor = new Color(1f, 0.55f, 0.15f), extra = "cape" },
            2.2f, 1.1f, Cast, SfxId.MagicShot, orb, null);
        CardData cSeer = MakeCard("Card_Emberseer", "Emberseer", "Magic bolts explode in an area.", 4, CardType.Unit, Rarity.Epic,
            new Color(0.75f, 0.4f, 0.95f), seer, new CardStats { health = 340, damage = 120, attackRange = 5f, attackSpeed = 0.7f, moveSpeed = 2.2f, detectionRange = 6f, bodyRadius = 0.5f, canTargetAir = true, splashRadius = 1.6f, projectileSpeed = 9f }, null);

        GameObject lancer = BuildCharacter("Unit_Lancer", typeof(MeleeUnit),
            new Look { legs = new Color(0.35f, 0.3f, 0.3f), scale = 1.05f, headgear = "helmet", tintGear = true, weapon = "spear", extra = "cape" },
            2.15f, 1.2f, Thrust, SfxId.SwordHit, null,
            root =>
            {
                Component a = root.AddComponent<ChargeAbility>();
                SetFloat(a, "chargeDistance", 6f); SetFloat(a, "speedBoost", 1.9f); SetFloat(a, "damageMultiplier", 2f);
            });
        CardData cLancer = MakeCard("Card_Lancer", "Ridgeback Lancer", "After a long run-up the first strike deals double damage.", 4, CardType.Unit, Rarity.Rare,
            new Color(0.95f, 0.6f, 0.3f), lancer, new CardStats { health = 520, damage = 90, attackRange = 0.7f, attackSpeed = 0.9f, moveSpeed = 2.6f, detectionRange = 5f, bodyRadius = 0.5f }, null);

        GameObject imp = BuildCharacter("Unit_PowderImp", typeof(MeleeUnit),
            new Look { torso = new Color(0.75f, 0.2f, 0.15f), tintTorso = false, legs = new Color(0.2f, 0.15f, 0.15f), skin = new Color(0.85f, 0.35f, 0.25f), scale = 0.7f, headgear = "horns", gear = new Color(0.2f, 0.15f, 0.15f), extra = "bomb" },
            1.5f, 0.9f, Thrust, SfxId.Whoosh, null,
            root =>
            {
                Component a = root.AddComponent<DeathBurstAbility>();
                SetFloat(a, "radius", 2.1f); SetFloat(a, "damage", 260f); SetFloat(a, "towerMultiplier", 0.5f); SetBool(a, "explodeOnAttack", true);
            });
        CardData cImp = MakeCard("Card_PowderImp", "Powder Imp", "Sprints in and detonates on impact. Also explodes if destroyed.", 3, CardType.Unit, Rarity.Common,
            new Color(0.95f, 0.35f, 0.2f), imp, new CardStats { health = 90, damage = 1, attackRange = 0.4f, attackSpeed = 1f, moveSpeed = 3.9f, detectionRange = 4f, bodyRadius = 0.34f }, null);

        GameObject cleric = BuildCharacter("Unit_DawnCleric", typeof(RangedUnit),
            new Look { torso = new Color(0.95f, 0.95f, 0.9f), tintTorso = false, legs = new Color(0.9f, 0.88f, 0.8f), headgear = "none", weapon = "staff", weaponColor = new Color(1f, 0.95f, 0.5f), extra = "halo" },
            2.1f, 1.1f, Cast, SfxId.MagicShot, orb,
            root =>
            {
                Component a = root.AddComponent<AuraAbility>();
                SetEnum(a, "mode", 0); SetFloat(a, "radius", 3.5f); SetFloat(a, "healPerSecond", 45f);
            });
        CardData cCleric = MakeCard("Card_DawnCleric", "Dawn Cleric", "Support. Heals nearby allies 45 health per second.", 4, CardType.Unit, Rarity.Rare,
            new Color(1f, 0.95f, 0.5f), cleric, new CardStats { health = 300, damage = 28, attackRange = 4.5f, attackSpeed = 1f, moveSpeed = 2.3f, detectionRange = 5.5f, bodyRadius = 0.42f, canTargetAir = true, projectileSpeed = 10f }, null);

        GameObject storm = BuildCharacter("Unit_Stormcaller", typeof(RangedUnit),
            new Look { torso = new Color(0.2f, 0.3f, 0.55f), tintTorso = false, legs = new Color(0.15f, 0.2f, 0.4f), headgear = "hood", gear = new Color(0.2f, 0.3f, 0.55f), weapon = "staff", weaponColor = new Color(0.5f, 0.85f, 1f), extra = "cape" },
            2.1f, 1.1f, Cast, SfxId.Zap, null,
            root =>
            {
                Component a = root.AddComponent<ChainAttackAbility>();
                SetInt(a, "jumps", 2); SetFloat(a, "jumpRadius", 3.2f); SetFloat(a, "falloff", 0.6f);
            });
        CardData cStorm = MakeCard("Card_Stormcaller", "Stormcaller", "Chain lightning leaps to two more enemies at 60% damage each.", 4, CardType.Unit, Rarity.Epic,
            new Color(0.5f, 0.85f, 1f), storm, new CardStats { health = 300, damage = 90, attackRange = 5f, attackSpeed = 0.8f, moveSpeed = 2.3f, detectionRange = 6f, bodyRadius = 0.42f, canTargetAir = true }, null);

        GameObject warden = BuildCharacter("Unit_FrostWarden", typeof(MeleeUnit),
            new Look { torso = new Color(0.6f, 0.8f, 0.95f), tintTorso = false, legs = new Color(0.35f, 0.5f, 0.65f), scale = 1.15f, width = 1.15f, headgear = "horns", gear = new Color(0.75f, 0.9f, 1f), weapon = "hammer", weaponColor = new Color(0.7f, 0.85f, 1f), shield = true, extra = "cape" },
            2.3f, 1.4f, Slam, SfxId.Deploy, null,
            root =>
            {
                Component a = root.AddComponent<OnHitDebuffAbility>();
                SetFloat(a, "slowMultiplier", 0.55f); SetFloat(a, "slowSeconds", 1.8f); SetInt(a, "stunEvery", 3); SetFloat(a, "stunSeconds", 0.7f);
            });
        CardData cWarden = MakeCard("Card_FrostWarden", "Frost Warden", "Frostbite slows enemies. Every third hit stuns.", 4, CardType.Unit, Rarity.Rare,
            new Color(0.6f, 0.85f, 1f), warden, new CardStats { health = 900, damage = 70, attackRange = 0.6f, attackSpeed = 0.8f, moveSpeed = 2f, detectionRange = 5f, bodyRadius = 0.65f }, null);

        GameObject drummer = BuildCharacter("Unit_DrumCaptain", typeof(MeleeUnit),
            new Look { legs = new Color(0.3f, 0.2f, 0.15f), scale = 1.05f, headgear = "helmet", gear = new Color(0.55f, 0.4f, 0.25f), weapon = "sword", extra = "drum" },
            2.1f, 1.2f, Swing, SfxId.SwordHit, null,
            root =>
            {
                Component a = root.AddComponent<AuraAbility>();
                SetEnum(a, "mode", 1); SetFloat(a, "radius", 3.6f); SetFloat(a, "speedBonus", 1.3f); SetFloat(a, "damageBonus", 1.25f);
                SetColor(a, "color", new Color(1f, 0.6f, 0.2f));
            });
        CardData cDrummer = MakeCard("Card_DrumCaptain", "Drum Captain", "War drums enrage nearby allies: +30% speed, +25% damage.", 4, CardType.Unit, Rarity.Epic,
            new Color(1f, 0.6f, 0.2f), drummer, new CardStats { health = 480, damage = 55, attackRange = 0.5f, attackSpeed = 1f, moveSpeed = 2.4f, detectionRange = 5f, bodyRadius = 0.5f }, null);

        // ---- buildings ----
        GameObject boltTower = BuildBoltTowerPrefab(orb);
        CardData cBolt = MakeCard("Card_BoltTower", "Bolt Tower", "Defensive turret. Decays after 40 seconds.", 3, CardType.Building, Rarity.Common,
            new Color(0.95f, 0.55f, 0.3f), boltTower, new CardStats { health = 900, damage = 70, attackRange = 6.5f, attackSpeed = 1.5f, detectionRange = 6.5f, bodyRadius = 0.8f, canTargetAir = true, projectileSpeed = 16f, lifetime = 40f }, null);

        GameObject den = BuildDenPrefab(cScrapper);
        CardData cDen = MakeCard("Card_ScrapperDen", "Scrapper Den", "Barracks. Spawns a Scrapper every 4.5 seconds for 35 seconds.", 4, CardType.Building, Rarity.Rare,
            new Color(0.5f, 0.75f, 0.35f), den, new CardStats { health = 700, bodyRadius = 1.0f, lifetime = 35f }, null);

        // ---- spells ----
        GameObject emberBurst = BuildSpellPrefab("Spell_EmberBurst", typeof(DamageSpell), new Color(1f, 0.45f, 0.1f, 0.45f));
        GameObject frostField = BuildSpellPrefab("Spell_FrostField", typeof(SlowSpell), new Color(0.4f, 0.8f, 1f, 0.45f));
        GameObject verdantPulse = BuildSpellPrefab("Spell_VerdantPulse", typeof(HealSpell), new Color(0.3f, 1f, 0.4f, 0.45f));
        GameObject thunderSnare = BuildSpellPrefab("Spell_ThunderSnare", typeof(StunSpell), new Color(0.75f, 0.85f, 1f, 0.4f));

        CardData cBurst = MakeCard("Card_EmberBurst", "Ember Burst", "Area blast. Towers take reduced damage.", 4, CardType.Spell, Rarity.Rare,
            new Color(1f, 0.45f, 0.1f), emberBurst, null, new SpellStats { radius = 2.6f, amount = 320f, towerDamageMultiplier = 0.35f });
        CardData cFrost = MakeCard("Card_FrostField", "Frost Field", "Slows enemy units in the area for 4 seconds.", 3, CardType.Spell, Rarity.Common,
            new Color(0.4f, 0.8f, 1f), frostField, null, new SpellStats { radius = 3.2f, duration = 4f, tickInterval = 0.5f, amount = 0f, slowMultiplier = 0.45f });
        CardData cHeal = MakeCard("Card_VerdantPulse", "Verdant Pulse", "Heals friendly units and buildings over time.", 3, CardType.Spell, Rarity.Epic,
            new Color(0.3f, 1f, 0.4f), verdantPulse, null, new SpellStats { radius = 3.4f, duration = 4f, tickInterval = 0.5f, amount = 90f });
        CardData cSnare = MakeCard("Card_ThunderSnare", "Thunder Snare", "Lightning strikes deal damage and stun enemy units.", 3, CardType.Spell, Rarity.Epic,
            new Color(0.75f, 0.85f, 1f), thunderSnare, null, new SpellStats { radius = 2.8f, amount = 120f, stunSeconds = 1.4f });

        r.PlayerDeck = new[] { cVanguard, cRanger, cBulwark, cWisp, cLancer, cStorm, cCleric, cBurst };
        r.AiDeck = new[] { cWarden, cSprinter, cImp, cSeer, cDrummer, cBolt, cSnare, cHeal };
        // Not in a deck by default (swap them in from the DeckManager list): Scrapper Den, Frost Field.
        // Minion cards (Pebbleling, Scrapper) are spawned by abilities/buildings and are not meant to be played from a hand.
        return r;
    }

    // ------------------------------------------------------------------ character construction

    static GameObject BuildCharacter(string name, Type type, Look look, float barHeight, float barWidth,
                                     UnitAnimator.AttackStyle style, SfxId sfx, Projectile projectile, Action<GameObject> addAbilities)
    {
        GameObject root = new GameObject(name);
        Transform visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        Rig rig = BuildHumanoid(visual, look);
        return FinishUnit(root, visual, rig, type, barHeight, barWidth, style, sfx, projectile, addAbilities, false, look.scale);
    }

    // A hovering wisp with flapping wings (armL / armR are the wings).
    static GameObject BuildWisp(Action<GameObject> addAbilities)
    {
        GameObject root = new GameObject("Unit_GaleWisp");
        Transform visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        Rig rig = new Rig();
        rig.body = Pivot(visual, "Body", Vector3.zero);
        P(PrimitiveType.Sphere, "Tint_Core", rig.body, Vector3.zero, Vector3.one * 0.85f, Color.white);
        P(PrimitiveType.Sphere, "EyeL", rig.body, new Vector3(-0.15f, 0.1f, 0.36f), Vector3.one * 0.12f, Color.black);
        P(PrimitiveType.Sphere, "EyeR", rig.body, new Vector3(0.15f, 0.1f, 0.36f), Vector3.one * 0.12f, Color.black);
        P(PrimitiveType.Sphere, "Tail", rig.body, new Vector3(0f, -0.05f, -0.5f), Vector3.one * 0.35f, new Color(0.9f, 0.95f, 1f));
        rig.armL = Pivot(rig.body, "ArmL", new Vector3(-0.3f, 0.12f, -0.05f));
        rig.armR = Pivot(rig.body, "ArmR", new Vector3(0.3f, 0.12f, -0.05f));
        P(PrimitiveType.Cube, "Wing", rig.armL, new Vector3(-0.55f, 0f, 0f), new Vector3(1.1f, 0.05f, 0.5f), new Color(0.9f, 0.95f, 1f));
        P(PrimitiveType.Cube, "Wing", rig.armR, new Vector3(0.55f, 0f, 0f), new Vector3(1.1f, 0.05f, 0.5f), new Color(0.9f, 0.95f, 1f));
        return FinishUnit(root, visual, rig, typeof(FlyingUnit), 1.3f, 1.0f, UnitAnimator.AttackStyle.Thrust, SfxId.Whoosh, null, addAbilities, true, 1f);
    }

    static GameObject FinishUnit(GameObject root, Transform visual, Rig rig, Type type, float barHeight, float barWidth,
                                 UnitAnimator.AttackStyle style, SfxId sfx, Projectile projectile, Action<GameObject> addAbilities,
                                 bool flap, float scale)
    {
        Component unit = root.AddComponent(type);
        SetObj(unit, "visual", visual);
        SetFloat(unit, "healthBarHeight", barHeight);
        SetFloat(unit, "healthBarWidth", barWidth);
        SetEnum(unit, "attackSfx", (int)sfx);
        if (projectile != null)
        {
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(visual, false);
            muzzle.localPosition = new Vector3(0f, 1.1f * scale, 0.5f * scale);
            SetObj(unit, "projectilePrefab", projectile);
            SetObj(unit, "muzzle", muzzle);
        }

        // The animator lives on the Visual child and drives the rig pivots.
        Component anim = visual.gameObject.AddComponent<UnitAnimator>();
        SetObj(anim, "body", rig.body);
        SetObj(anim, "head", rig.head);
        SetObj(anim, "armL", rig.armL);
        SetObj(anim, "armR", rig.armR);
        SetObj(anim, "legL", rig.legL);
        SetObj(anim, "legR", rig.legR);
        SetEnum(anim, "attackStyle", (int)style);
        SetBool(anim, "flapWings", flap);

        if (addAbilities != null) addAbilities(root);
        AddNetworkComponents(root, true); // units move: NetworkTransform replicates their position
        return Save(root, root.name);
    }

    // Builds a rigged humanoid out of primitives. Pivots: LegL/LegR at the hips, Body (torso), Head, ArmL/ArmR at the shoulders.
    static Rig BuildHumanoid(Transform visual, Look l)
    {
        float s = l.scale, w = l.width;
        string tt = l.tintTorso ? "Tint_" : "";
        Rig r = new Rig();

        float hip = 0.55f * s;
        r.legL = Pivot(visual, "LegL", new Vector3(-0.16f * s * w, hip, 0f));
        r.legR = Pivot(visual, "LegR", new Vector3(0.16f * s * w, hip, 0f));
        P(PrimitiveType.Cube, "Leg", r.legL, new Vector3(0f, -hip * 0.5f, 0f), new Vector3(0.2f * s * w, hip, 0.22f * s * w), l.legs);
        P(PrimitiveType.Cube, "Leg", r.legR, new Vector3(0f, -hip * 0.5f, 0f), new Vector3(0.2f * s * w, hip, 0.22f * s * w), l.legs);

        r.body = Pivot(visual, "Body", new Vector3(0f, hip, 0f));
        P(PrimitiveType.Cube, tt + "Torso", r.body, new Vector3(0f, 0.35f * s, 0f), new Vector3(0.56f * s * w, 0.7f * s, 0.34f * s * w), l.torso);

        r.head = Pivot(r.body, "Head", new Vector3(0f, 0.72f * s, 0f));
        P(PrimitiveType.Sphere, "HeadMesh", r.head, new Vector3(0f, 0.17f * s, 0f), Vector3.one * 0.4f * s, l.skin);
        P(PrimitiveType.Sphere, "EyeL", r.head, new Vector3(-0.08f * s, 0.2f * s, 0.17f * s), Vector3.one * 0.06f * s, Color.black);
        P(PrimitiveType.Sphere, "EyeR", r.head, new Vector3(0.08f * s, 0.2f * s, 0.17f * s), Vector3.one * 0.06f * s, Color.black);

        float shoulderX = (0.28f * w + 0.09f) * s;
        r.armL = Pivot(r.body, "ArmL", new Vector3(-shoulderX, 0.66f * s, 0f));
        r.armR = Pivot(r.body, "ArmR", new Vector3(shoulderX, 0.66f * s, 0f));
        foreach (Transform arm in new[] { r.armL, r.armR })
        {
            P(PrimitiveType.Cube, tt + "Arm", arm, new Vector3(0f, -0.24f * s, 0f), new Vector3(0.14f * s * w, 0.5f * s, 0.16f * s * w), l.torso);
            P(PrimitiveType.Sphere, "Hand", arm, new Vector3(0f, -0.5f * s, 0f), Vector3.one * 0.13f * s, l.skin);
        }

        // ---- headgear ----
        switch (l.headgear)
        {
            case "helmet":
                P(PrimitiveType.Sphere, "Helmet", r.head, new Vector3(0f, 0.24f * s, 0f), new Vector3(0.44f, 0.3f, 0.44f) * s, l.gear);
                if (l.tintGear) P(PrimitiveType.Cube, "Tint_Plume", r.head, new Vector3(0f, 0.42f * s, -0.05f * s), new Vector3(0.06f, 0.22f, 0.3f) * s, Color.white);
                break;
            case "hood":
                P(PrimitiveType.Sphere, "Hood", r.head, new Vector3(0f, 0.2f * s, -0.03f * s), Vector3.one * 0.47f * s, l.gear);
                break;
            case "hat":
                P(PrimitiveType.Cylinder, l.tintGear ? "Tint_HatBrim" : "HatBrim", r.head, new Vector3(0f, 0.32f * s, 0f), new Vector3(0.62f, 0.02f, 0.62f) * s, l.gear);
                P(PrimitiveType.Cylinder, l.tintGear ? "Tint_Hat" : "Hat", r.head, new Vector3(0f, 0.5f * s, 0f), new Vector3(0.3f, 0.16f, 0.3f) * s, l.gear);
                break;
            case "horns":
                P(PrimitiveType.Sphere, "Cap", r.head, new Vector3(0f, 0.24f * s, 0f), new Vector3(0.43f, 0.28f, 0.43f) * s, l.gear);
                GameObject hl = P(PrimitiveType.Cube, "HornL", r.head, new Vector3(-0.2f * s, 0.4f * s, 0f), new Vector3(0.06f, 0.22f, 0.06f) * s, new Color(0.95f, 0.92f, 0.8f));
                hl.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
                GameObject hr = P(PrimitiveType.Cube, "HornR", r.head, new Vector3(0.2f * s, 0.4f * s, 0f), new Vector3(0.06f, 0.22f, 0.06f) * s, new Color(0.95f, 0.92f, 0.8f));
                hr.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
                break;
        }

        // ---- weapon (held in the hand; the arm swing of the attack animation carries it) ----
        Color wc = l.weaponColor;
        Transform handR = Pivot(r.armR, "Weapon", new Vector3(0f, -0.5f * s, 0f));
        switch (l.weapon)
        {
            case "sword":
                handR.localRotation = Quaternion.Euler(80f, 0f, 0f);
                P(PrimitiveType.Cube, "Blade", handR, new Vector3(0f, 0.42f * s, 0f), new Vector3(0.07f, 0.8f, 0.03f) * s, wc);
                P(PrimitiveType.Cube, "Guard", handR, new Vector3(0f, 0.03f * s, 0f), new Vector3(0.24f, 0.04f, 0.06f) * s, Gold);
                break;
            case "dagger":
                handR.localRotation = Quaternion.Euler(80f, 0f, 0f);
                P(PrimitiveType.Cube, "Blade", handR, new Vector3(0f, 0.22f * s, 0f), new Vector3(0.06f, 0.42f, 0.03f) * s, wc);
                break;
            case "spear":
                handR.localRotation = Quaternion.Euler(80f, 0f, 0f);
                P(PrimitiveType.Cylinder, "Shaft", handR, new Vector3(0f, 0.5f * s, 0f), new Vector3(0.04f, 0.7f, 0.04f) * s, Wood);
                P(PrimitiveType.Cube, "Tip", handR, new Vector3(0f, 1.08f * s, 0f), new Vector3(0.1f, 0.22f, 0.04f) * s, wc);
                break;
            case "hammer":
                handR.localRotation = Quaternion.Euler(70f, 0f, 0f);
                P(PrimitiveType.Cylinder, "Handle", handR, new Vector3(0f, 0.36f * s, 0f), new Vector3(0.05f, 0.4f, 0.05f) * s, Wood);
                P(PrimitiveType.Cube, "Head", handR, new Vector3(0f, 0.78f * s, 0f), new Vector3(0.4f, 0.26f, 0.26f) * s, wc);
                break;
            case "staff":
                P(PrimitiveType.Cylinder, "Shaft", handR, new Vector3(0f, 0.22f * s, 0f), new Vector3(0.05f, 0.55f, 0.05f) * s, Wood);
                P(PrimitiveType.Sphere, "Orb", handR, new Vector3(0f, 0.85f * s, 0f), Vector3.one * 0.26f * s, wc);
                break;
            case "bow":
                {
                    Transform handL = Pivot(r.armL, "Weapon", new Vector3(0f, -0.5f * s, 0f));
                    P(PrimitiveType.Cylinder, "Bow", handL, new Vector3(0f, 0.1f * s, 0.1f * s), new Vector3(0.04f, 0.5f, 0.04f) * s, Wood);
                    P(PrimitiveType.Cube, "String", handL, new Vector3(0f, 0.1f * s, 0.03f * s), new Vector3(0.012f, 0.95f, 0.012f) * s, Color.white);
                    break;
                }
        }

        if (l.shield)
        {
            Transform sh = Pivot(r.armL, "Shield", new Vector3(-0.1f * s, -0.4f * s, 0.1f * s));
            P(PrimitiveType.Cube, "Tint_Shield", sh, Vector3.zero, new Vector3(0.08f, 0.55f, 0.42f) * s, Color.white);
            P(PrimitiveType.Cube, "ShieldBoss", sh, new Vector3(-0.05f * s, 0f, 0f), new Vector3(0.05f, 0.15f, 0.15f) * s, Gold);
        }

        // ---- extras ----
        switch (l.extra)
        {
            case "cape":
                P(PrimitiveType.Cube, "Tint_Cape", r.body, new Vector3(0f, 0.3f * s, -0.22f * s * w), new Vector3(0.5f * w, 0.68f, 0.05f) * s, Color.white);
                break;
            case "bomb":
                P(PrimitiveType.Sphere, "Bomb", r.body, new Vector3(0f, 0.3f * s, -0.3f * s), Vector3.one * 0.42f * s, new Color(0.12f, 0.12f, 0.14f));
                P(PrimitiveType.Sphere, "Fuse", r.body, new Vector3(0f, 0.58f * s, -0.32f * s), Vector3.one * 0.12f * s, new Color(1f, 0.6f, 0.15f));
                break;
            case "halo":
                P(PrimitiveType.Cylinder, "Halo", r.head, new Vector3(0f, 0.52f * s, 0f), new Vector3(0.4f, 0.012f, 0.4f) * s, Gold);
                break;
            case "drum":
                {
                    GameObject drum = P(PrimitiveType.Cylinder, "Drum", r.body, new Vector3(0f, 0.32f * s, 0.24f * s * w), new Vector3(0.42f, 0.13f, 0.42f) * s, Wood);
                    drum.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    GameObject rim = P(PrimitiveType.Cylinder, "Tint_DrumRim", r.body, new Vector3(0f, 0.32f * s, 0.3f * s * w), new Vector3(0.46f, 0.02f, 0.46f) * s, Color.white);
                    rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                }
            case "core":
                P(PrimitiveType.Sphere, "Tint_Core", r.body, new Vector3(0f, 0.4f * s, 0.2f * s * w), Vector3.one * 0.3f * s, Color.white);
                break;
        }
        return r;
    }

    // ------------------------------------------------------------------ buildings / projectiles

    static GameObject BuildDenPrefab(CardData minion)
    {
        GameObject root = new GameObject("Building_ScrapperDen");
        Transform visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        P(PrimitiveType.Cube, "Walls", visual, new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 1.2f, 1.7f), Wood);
        GameObject roofL = P(PrimitiveType.Cube, "Tint_RoofL", visual, new Vector3(-0.55f, 1.5f, 0f), new Vector3(1.3f, 0.12f, 2.0f), Color.white);
        roofL.transform.localRotation = Quaternion.Euler(0f, 0f, 32f);
        GameObject roofR = P(PrimitiveType.Cube, "Tint_RoofR", visual, new Vector3(0.55f, 1.5f, 0f), new Vector3(1.3f, 0.12f, 2.0f), Color.white);
        roofR.transform.localRotation = Quaternion.Euler(0f, 0f, -32f);
        P(PrimitiveType.Cube, "Door", visual, new Vector3(0f, 0.4f, 0.86f), new Vector3(0.6f, 0.8f, 0.06f), new Color(0.15f, 0.1f, 0.08f));
        P(PrimitiveType.Cylinder, "Pole", visual, new Vector3(0.8f, 1.9f, 0.6f), new Vector3(0.06f, 0.6f, 0.06f), Wood);
        P(PrimitiveType.Cube, "Tint_Flag", visual, new Vector3(0.98f, 2.3f, 0.6f), new Vector3(0.36f, 0.24f, 0.03f), Color.white);

        Component b = root.AddComponent<SpawnerBuilding>();
        SetObj(b, "visual", visual);
        SetObj(b, "minionCard", minion);
        SetFloat(b, "healthBarHeight", 2.8f);
        SetFloat(b, "healthBarWidth", 1.7f);
        SetFloat(b, "aimHeight", 1f);
        AddNetworkComponents(root, false);
        return Save(root, "Building_ScrapperDen");
    }

    static Projectile BuildArrowPrefab()
    {
        GameObject root = new GameObject("Projectile_Arrow");
        P(PrimitiveType.Cube, "Tint_Arrow", root.transform, Vector3.zero, new Vector3(0.06f, 0.06f, 0.6f), Color.white);
        root.AddComponent<Projectile>();
        return Save(root, "Projectile_Arrow").GetComponent<Projectile>();
    }

    // ------------------------------------------------------------------ helpers

    static Transform Pivot(Transform parent, string name, Vector3 localPos)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    static void NotInPool(CardData c)
    {
        if (c == null || !c.inDeckPool) return;
        c.inDeckPool = false;
        EditorUtility.SetDirty(c);
    }

    /// <summary>
    /// Repairs projects built with an earlier version: moves the card assets into Assets/CardBattle/Resources/Cards
    /// (keeps GUIDs, so nothing in your scene breaks) and keeps the summoned minions out of random decks.
    /// </summary>
    [MenuItem("Tools/Card Battle/Fix Card Pool (move cards to Resources)")]
    public static void FixCardPool()
    {
        System.IO.Directory.CreateDirectory(Root + "/Resources/Cards");
        AssetDatabase.Refresh();
        MigrateOldCards();
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CardData", new[] { Root + "/Resources/Cards" }))
        {
            CardData c = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
            if (c == null) continue;
            count++;
            if (c.name == "Card_Pebbleling" || c.name == "Card_Scrapper") NotInPool(c);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Card Battle: " + count + " cards are in the pool (Assets/CardBattle/Resources/Cards). Random decks are drawn from them on Play.");
    }

    static void MigrateOldCards()
    {
        string oldFolder = Root + "/Cards", newFolder = Root + "/Resources/Cards";
        if (!AssetDatabase.IsValidFolder(oldFolder)) return;
        foreach (string guid in AssetDatabase.FindAssets("t:CardData", new[] { oldFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string dest = newFolder + "/" + System.IO.Path.GetFileName(path);
            if (AssetDatabase.LoadAssetAtPath<CardData>(dest) != null) { AssetDatabase.DeleteAsset(path); continue; } // already moved
            string error = AssetDatabase.MoveAsset(path, dest);
            if (!string.IsNullOrEmpty(error)) Debug.LogWarning("Card Battle: could not move " + path + ": " + error);
        }
        if (AssetDatabase.FindAssets("", new[] { oldFolder }).Length == 0) AssetDatabase.DeleteAsset(oldFolder);
    }

    static void SetInt(UnityEngine.Object target, string field, int value)
    {
        SerializedObject so; SerializedProperty p = Prop(target, field, out so);
        if (p == null) return;
        p.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetColor(UnityEngine.Object target, string field, Color value)
    {
        SerializedObject so; SerializedProperty p = Prop(target, field, out so);
        if (p == null) return;
        p.colorValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
