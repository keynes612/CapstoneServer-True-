using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 2D 전투 테스트 에셋을 한 번만 만든다. 이미 있는 에셋·배치는 덮어쓰지 않는다.
public static class CombatAssetBuilder2D
{
    const string Prefabs = "Assets/Prefabs/Combat";
    const string Sprites = "Assets/Sprites/Combat";
    const string ScenePath = "Assets/Scenes/KDH_testScene.unity";

    static readonly Color PlayerColor = new Color(0.35f, 0.8f, 1);
    static readonly Color MonsterColor = new Color(0.9f, 0.12f, 0.12f);
    static readonly Color GroundColor = new Color(0.18f, 0.23f, 0.27f);
    static readonly Color WallColor = new Color(0.32f, 0.38f, 0.44f);
    static readonly Color AttackColor = new Color(1, 0.8f, 0.15f);

    [MenuItem("Tools/Combat/Create 2D Combat Assets (Once)")]
    public static void CreateAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Combat 2D: Play Mode에서는 실행하지 않습니다.");
            return;
        }
        string[] outputs = { Prefabs + "/PlayerMelee.prefab", Prefabs + "/PlayerRanged.prefab",
            Prefabs + "/MonsterMelee.prefab", Prefabs + "/MonsterRanged.prefab",
            Prefabs + "/Projectile.prefab", Sprites + "/Square.png", Sprites + "/Circle.png" };
        foreach (string output in outputs)
            if (AssetDatabase.LoadMainAssetAtPath(output) != null)
            {
                Debug.LogWarning("Combat 2D: 이미 있는 에셋을 보존하고 중단합니다: " + output);
                return;
            }
        if (AssetDatabase.LoadMainAssetAtPath(ScenePath) == null)
            throw new InvalidOperationException("Combat 2D: 씬이 없습니다: " + ScenePath);

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;
        if (scene.IsValid() && scene.isLoaded)
        {
            if (scene.isDirty)
            {
                Debug.LogWarning("Combat 2D: KDH_testScene에 저장하지 않은 변경이 있습니다. 저장한 뒤 다시 실행하세요.");
                return;
            }
        }
        else
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Ground" || root.name.StartsWith("Player") || root.name.StartsWith("Monster"))
            {
                Debug.LogWarning("Combat 2D: 씬에 이미 전투 오브젝트가 있어 중단합니다: " + root.name);
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                return;
            }

        Folder("Assets", "Prefabs"); Folder("Assets/Prefabs", "Combat");
        Folder("Assets", "Sprites"); Folder("Assets/Sprites", "Combat");
        Sprite square = SpriteAsset("Square", 4, false);
        Sprite circle = SpriteAsset("Circle", 64, true);

        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try
        {
            GameObject ball = new GameObject("Projectile");
            Renderer2D(ball, circle, AttackColor, 2);
            ball.AddComponent<CombatProjectile>();
            CombatProjectile projectile = SavePrefab(ball).GetComponent<CombatProjectile>();

            GameObject playerMelee = Player("PlayerMelee", square, circle, projectile, CombatPlayerAttack.AttackType.Melee);
            Player("PlayerRanged", square, circle, projectile, CombatPlayerAttack.AttackType.Ranged);
            GameObject monsterMelee = Monster("MonsterMelee", square, projectile, CombatPlayerAttack.AttackType.Melee);
            GameObject monsterRanged = Monster("MonsterRanged", square, projectile, CombatPlayerAttack.AttackType.Ranged);

            GameObject ground = new GameObject("Ground");
            Renderer2D(ground, square, GroundColor, -10);
            ground.transform.localScale = new Vector3(40, 40, 1);
            // 낮은 벽이 테스트 플레이어를 바닥 안에 가둔다.
            Wall("North", new Vector2(0, 20), new Vector2(40, 0.5f), square);
            Wall("South", new Vector2(0, -20), new Vector2(40, 0.5f), square);
            Wall("East", new Vector2(20, 0), new Vector2(0.5f, 40), square);
            Wall("West", new Vector2(-20, 0), new Vector2(0.5f, 40), square);
            Place(playerMelee, new Vector2(0, 0));
            Place(monsterMelee, new Vector2(0, 4));
            Place(monsterRanged, new Vector2(4, 4));

            Camera camera = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out Camera found)) { camera = found; break; }
            if (camera == null) throw new InvalidOperationException("Combat 2D: 씬에 카메라가 없습니다.");
            if (!camera.TryGetComponent(out CombatCamera follow)) follow = camera.gameObject.AddComponent<CombatCamera>();
            camera.transform.position = follow.offset;

            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Combat 2D: 씬을 저장하지 못했습니다.");
            AssetDatabase.SaveAssets();
            Debug.Log("Combat 2D: 에셋 생성 완료. KDH_testScene을 열고 Play로 확인하세요. Play Mode는 실행하지 않았습니다.");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded && previous != scene) SceneManager.SetActiveScene(previous);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    // 흰색 사각형·원 텍스처를 PNG로 만들고 1유닛 크기 스프라이트로 가져온다.
    static Sprite SpriteAsset(string name, int size, bool round)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - radius, dy = y + 0.5f - radius;
                float alpha = round ? Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy)) : 1;
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        string path = Sprites + "/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = size;
        importer.filterMode = round ? FilterMode.Bilinear : FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Combat 2D: 스프라이트를 만들지 못했습니다: " + path);
        return sprite;
    }

    static SpriteRenderer Renderer2D(GameObject obj, Sprite sprite, Color color, int order)
    {
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    static GameObject SavePrefab(GameObject obj)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, Prefabs + "/" + obj.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(obj);
        if (prefab == null) throw new InvalidOperationException("Combat 2D: 프리팹 저장 실패: " + obj.name);
        return prefab;
    }

    static GameObject Player(string name, Sprite square, Sprite circle,
        CombatProjectile projectile, CombatPlayerAttack.AttackType type)
    {
        GameObject obj = new GameObject(name);
        Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
        body.gravityScale = 0;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        obj.AddComponent<CircleCollider2D>().radius = 0.5f;

        GameObject sprite = new GameObject("Body");
        sprite.transform.SetParent(obj.transform, false);
        SpriteRenderer bodyRenderer = Renderer2D(sprite, circle, PlayerColor, 1);
        GameObject marker = new GameObject("ForwardMarker");
        marker.transform.SetParent(obj.transform, false);
        marker.transform.localPosition = new Vector3(0, 0.52f, 0);
        marker.transform.localScale = new Vector3(0.2f, 0.34f, 1);
        Renderer2D(marker, square, AttackColor, 2);

        CombatHealth health = obj.AddComponent<CombatHealth>();
        health.team = CombatHealth.Team.Player;
        health.maxHealth = 100;
        health.body = bodyRenderer;
        obj.AddComponent<CombatPlayerMovement>().moveSpeed = 5;
        CombatPlayerAttack weapon = obj.AddComponent<CombatPlayerAttack>();
        weapon.attackType = type;
        weapon.damage = 20;
        weapon.attackInterval = 0.4f;
        weapon.projectileSpeed = 12;
        weapon.projectileRange = 15;
        weapon.projectilePrefab = projectile;
        return SavePrefab(obj);
    }

    static GameObject Monster(string name, Sprite square, CombatProjectile projectile,
        CombatPlayerAttack.AttackType type)
    {
        GameObject obj = new GameObject(name);
        obj.transform.localScale = new Vector3(1.5f, 1.5f, 1);
        SpriteRenderer bodyRenderer = Renderer2D(obj, square, MonsterColor, 1);
        Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        obj.AddComponent<BoxCollider2D>().size = Vector2.one;

        CombatHealth health = obj.AddComponent<CombatHealth>();
        health.team = CombatHealth.Team.Monster;
        health.maxHealth = 100;
        health.body = bodyRenderer;
        CombatPlayerAttack weapon = obj.AddComponent<CombatPlayerAttack>();
        weapon.attackType = type;
        weapon.damage = 10;
        weapon.attackInterval = 1;
        weapon.projectileSpeed = 9;
        weapon.projectileRange = 10;
        weapon.detectionRange = 8;
        weapon.chaseSpeed = 2.5f;
        weapon.meleeAttackDistance = 1.9f;
        weapon.windupDuration = 0.55f;
        weapon.projectilePrefab = projectile;
        return SavePrefab(obj);
    }

    static void Place(GameObject prefab, Vector2 position)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = position;
    }

    static void Wall(string name, Vector2 position, Vector2 size, Sprite square)
    {
        GameObject wall = new GameObject(name);
        Renderer2D(wall, square, WallColor, 0);
        wall.transform.position = position;
        wall.transform.localScale = new Vector3(size.x, size.y, 1);
        wall.AddComponent<BoxCollider2D>().size = Vector2.one;
    }
}
