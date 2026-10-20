#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CardBattle;

/// <summary>
/// One-click prototype builder: Tools > Card Battle > Build Prototype Scene.
/// Generates placeholder prefabs (primitives only), CardData assets, and a fully wired Battle scene.
/// Re-running it refreshes the generated assets (your own cards/prefabs are never touched).
/// </summary>
public static partial class CardBattleSetup
{
    const string Root = "Assets/CardBattle";

    [MenuItem("Tools/Card Battle/Build Prototype Scene")]
    public static void BuildPrototype()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (string f in new[] { "Prefabs", "Resources/Cards", "Materials", "Scenes" })
            Directory.CreateDirectory(Root + "/" + f);
        AssetDatabase.Refresh();
        MigrateOldCards(); // earlier versions stored cards in Assets/CardBattle/Cards

        // ---- Characters, buildings, spells and cards (see CardBattleSetup.Roster.cs) ----
        Roster roster = BuildRoster(towerOrb: BuildProjectilePrefab(), arrow: BuildArrowPrefab());
        GameObject towerSide = BuildTowerPrefab("Tower_Side", false, roster.Orb);
        GameObject towerMain = BuildTowerPrefab("Tower_Main", true, roster.Orb);

        AssetDatabase.SaveAssets();
        BuildScene(towerSide, towerMain, roster.PlayerDeck, roster.AiDeck);
    }

    // ---------------- Scene ----------------

    static void BuildScene(GameObject towerSide, GameObject towerMain, CardData[] playerDeck, CardData[] aiDeck)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.6f, 0.65f, 0.75f);

        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.13f, 0.2f);
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<BattleCameraController>();

        GameObject systems = new GameObject("GameSystems");
        GameManager gm = systems.AddComponent<GameManager>();
        SetObj(gm, "overlayMaterial", OverlayMaterial(Color.white));
        SetObj(gm, "litMaterial", LitMaterial(Color.white));
        systems.AddComponent<MatchManager>();
        systems.AddComponent<ResourceManager>();
        systems.AddComponent<CardManager>();
        systems.AddComponent<AudioManager>(); // procedural sound effects + music (no audio files needed)

        GameObject arena = new GameObject("Arena");
        ArenaLayout layout = arena.AddComponent<ArenaLayout>();

        GameObject towers = new GameObject("Towers");
        foreach (Team team in new[] { Team.Player, Team.Enemy })
            for (int slot = 0; slot < 3; slot++)
            {
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(slot == 1 ? towerMain : towerSide);
                go.name = team + (slot == 1 ? "_MainTower" : (slot == 0 ? "_LeftTower" : "_RightTower"));
                go.transform.SetParent(towers.transform, false);
                go.transform.position = layout.TowerSlot(team, slot);
                SetEnum(go.GetComponent<Tower>(), "team", (int)team);
            }

        GameObject player = new GameObject("PlayerControl");
        DeckManager deck = player.AddComponent<DeckManager>();
        PlayerController pc = player.AddComponent<PlayerController>();
        SetObjList(deck, "deck", playerDeck);
        SetObj(pc, "indicatorMaterial", OverlayMaterial(Color.white));

        GameObject ai = new GameObject("EnemyAI");
        AIDeckManager aiDeckMgr = ai.AddComponent<AIDeckManager>();
        AIPlayer aiPlayer = ai.AddComponent<AIPlayer>();
        SetObjList(aiDeckMgr, "deck", aiDeck);
        SetObj(aiPlayer, "deck", aiDeckMgr);

        new GameObject("BattleUI").AddComponent<BattleUI>();
        AddNetworkSceneObjects(); // NetworkMatchManager + ConnectionStatusUI (inert when playing offline)

        string path = Root + "/Scenes/Battle.unity";
        EditorSceneManager.SaveScene(scene, path);

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        BuildMenuScene(path); // multiplayer menu scene; also sets Build Settings = [Menu, Battle]

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Debug.LogWarning("Card Battle: this project only uses the new Input System. Set Project Settings > Player > Active Input Handling to 'Both' (or 'Input Manager (Old)') or the scripts will throw on Input.*.");
#endif
        Debug.Log("Card Battle prototype built: " + path + ". Set the Game view to a portrait ratio (e.g. 9:16) and press Play.");
    }

    // ---------------- Prefab builders ----------------

    static Projectile BuildProjectilePrefab()
    {
        GameObject root = new GameObject("Projectile");
        P(PrimitiveType.Sphere, "Tint_Orb", root.transform, Vector3.zero, Vector3.one * 0.3f, Color.white);
        root.AddComponent<Projectile>();
        return Save(root, "Projectile").GetComponent<Projectile>();
    }

    static GameObject BuildBoltTowerPrefab(Projectile orb)
    {
        GameObject root = new GameObject("Building_BoltTower");
        Transform visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        P(PrimitiveType.Cylinder, "Base", visual, new Vector3(0f, 0.3f, 0f), new Vector3(1.7f, 0.3f, 1.7f), new Color(0.5f, 0.5f, 0.55f));
        P(PrimitiveType.Cylinder, "Tint_Column", visual, new Vector3(0f, 0.9f, 0f), new Vector3(1.2f, 0.5f, 1.2f), Color.white);
        Transform turret = new GameObject("Turret").transform;
        turret.SetParent(visual, false);
        turret.localPosition = new Vector3(0f, 1.65f, 0f);
        P(PrimitiveType.Sphere, "Head", turret, Vector3.zero, Vector3.one * 0.9f, new Color(0.35f, 0.35f, 0.4f));
        GameObject barrel = P(PrimitiveType.Cylinder, "Barrel", turret, new Vector3(0f, 0f, 0.65f), new Vector3(0.28f, 0.5f, 0.28f), new Color(0.25f, 0.25f, 0.3f));
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Transform muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(turret, false);
        muzzle.localPosition = new Vector3(0f, 0f, 1.2f);

        Component b = root.AddComponent<DefensiveBuilding>();
        SetObj(b, "visual", visual);
        SetObj(b, "turret", turret);
        SetObj(b, "muzzle", muzzle);
        SetObj(b, "projectilePrefab", orb);
        SetFloat(b, "healthBarHeight", 2.8f);
        SetFloat(b, "healthBarWidth", 1.6f);
        SetFloat(b, "aimHeight", 1.5f);
        AddNetworkComponents(root, false);
        return Save(root, "Building_BoltTower");
    }

    static GameObject BuildTowerPrefab(string name, bool main, Projectile orb)
    {
        GameObject root = new GameObject(name);
        Transform visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        float s = main ? 1.35f : 1f;
        visual.localScale = Vector3.one * s;

        Color stone = new Color(0.62f, 0.62f, 0.68f);
        P(PrimitiveType.Cylinder, "Base", visual, new Vector3(0f, 0.35f, 0f), new Vector3(3.0f, 0.35f, 3.0f), new Color(0.5f, 0.5f, 0.56f));
        P(PrimitiveType.Cylinder, "Body", visual, new Vector3(0f, 1.65f, 0f), new Vector3(2.2f, 1.3f, 2.2f), stone);
        P(PrimitiveType.Cylinder, "Tint_Roof", visual, new Vector3(0f, 3.1f, 0f), new Vector3(2.7f, 0.2f, 2.7f), Color.white);
        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0 ? -1f : 1f) * 0.95f, z = (i < 2 ? -1f : 1f) * 0.95f;
            P(PrimitiveType.Cube, "Crenel", visual, new Vector3(x, 3.4f, z), Vector3.one * 0.4f, stone);
        }
        Transform turret = new GameObject("Turret").transform;
        turret.SetParent(visual, false);
        turret.localPosition = new Vector3(0f, 3.55f, 0f);
        P(PrimitiveType.Sphere, "Tint_Cannon", turret, Vector3.zero, Vector3.one * 1.0f, Color.white);
        GameObject barrel = P(PrimitiveType.Cylinder, "Barrel", turret, new Vector3(0f, 0f, 0.75f), new Vector3(0.3f, 0.55f, 0.3f), new Color(0.25f, 0.25f, 0.3f));
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Transform muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(turret, false);
        muzzle.localPosition = new Vector3(0f, 0f, 1.35f);
        if (main) P(PrimitiveType.Sphere, "Tint_Crest", visual, new Vector3(0f, 4.3f, 0f), Vector3.one * 0.5f, Color.white);

        Component t = root.AddComponent<Tower>();
        SetObj(t, "visual", visual);
        SetObj(t, "turret", turret);
        SetObj(t, "muzzle", muzzle);
        SetObj(t, "projectilePrefab", orb);
        SetBool(t, "isMainTower", main);
        SetFloat(t, "maxHealth", main ? 2600f : 1800f);
        SetFloat(t, "bodyRadius", main ? 1.9f : 1.4f);
        SetFloat(t, "damage", main ? 80f : 65f);
        SetFloat(t, "attackRange", main ? 8.5f : 7.5f);
        SetFloat(t, "attackSpeed", main ? 1.3f : 1.2f);
        SetFloat(t, "projectileSpeed", 16f);
        SetBool(t, "canTargetAir", true);
        SetFloat(t, "aimHeight", 2.5f);
        SetFloat(t, "healthBarHeight", main ? 6.2f : 4.8f);
        SetFloat(t, "healthBarWidth", main ? 3.6f : 3.0f);
        AddNetworkComponents(root, false);
        return Save(root, name);
    }

    static GameObject BuildSpellPrefab(string name, Type type, Color areaColor)
    {
        GameObject root = new GameObject(name);
        Component spell = root.AddComponent(type);
        GameObject area = P(PrimitiveType.Cylinder, "Area", root.transform, Vector3.zero, new Vector3(6f, 0.02f, 6f), OverlayMaterial(areaColor));
        SetObj(spell, "areaVisual", area.transform);
        SetColor(spell, "fxColor", new Color(areaColor.r, areaColor.g, areaColor.b, 1f));
        return Save(root, name);
    }

    // ---------------- Cards ----------------

    static CardData MakeCard(string file, string cardName, string description, int cost, CardType type, Rarity rarity,
                             Color iconColor, GameObject prefab, CardStats stats, SpellStats spell)
    {
        string path = Root + "/Resources/Cards/" + file + ".asset"; // Resources/Cards = auto-discovered card pool
        CardData c = AssetDatabase.LoadAssetAtPath<CardData>(path);
        bool isNew = c == null;
        if (isNew) c = ScriptableObject.CreateInstance<CardData>();
        c.cardName = cardName;
        c.description = description;
        c.cost = cost;
        c.cardType = type;
        c.rarity = rarity;
        c.iconColor = iconColor;
        c.prefab = prefab;
        if (stats != null) c.stats = stats;
        if (spell != null) c.spell = spell;
        if (isNew) AssetDatabase.CreateAsset(c, path); else EditorUtility.SetDirty(c);
        return c;
    }

    // ---------------- Helpers ----------------

    static GameObject Save(GameObject root, string name)
    {
        string path = Root + "/Prefabs/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(path); // lets Netcode generate the NetworkObject hash
        return prefab;
    }

    static GameObject P(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color color)
    {
        return PlaceholderUtil.Part(type, name, parent, pos, scale, LitMaterial(color));
    }

    static GameObject P(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material material)
    {
        return PlaceholderUtil.Part(type, name, parent, pos, scale, material);
    }

    static Material LitMaterial(Color c)
    {
        string path = Root + "/Materials/Lit_" + ColorUtility.ToHtmlStringRGBA(c) + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(PlaceholderUtil.GetDefaultLitMaterial());
        m.color = c;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material OverlayMaterial(Color c)
    {
        string path = Root + "/Materials/Overlay_" + ColorUtility.ToHtmlStringRGBA(c) + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = PlaceholderUtil.MakeOverlay(c);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static SerializedProperty Prop(UnityEngine.Object target, string field, out SerializedObject so)
    {
        so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) Debug.LogWarning("CardBattleSetup: field '" + field + "' not found on " + target.GetType().Name);
        return p;
    }

    static void SetObj(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        SerializedObject so; SerializedProperty p = Prop(target, field, out so);
        if (p == null) return;
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(UnityEngine.Object target, string field, float value)
    {
        SerializedObject so; SerializedProperty p = Prop(target, field, out so);
        if (p == null) return;
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetBool(UnityEngine.Object target, string field, bool value)
    {
        SerializedObject so; SerializedProperty p = Prop(target, field, out so);
        if (p == null) return;
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetEnum(UnityEngine.Object target, string field, int index)
    {
        SerializedObject so; SerializedProperty p = Prop(target, field, out so);
        if (p == null) return;
        p.enumValueIndex = index;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetObjList(UnityEngine.Object target, string field, UnityEngine.Object[] values)
    {
        SerializedObject so; SerializedProperty p = Prop(target, field, out so);
        if (p == null) return;
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
