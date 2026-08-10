using System.IO;
using Chronomancers.Sim.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Chronomancers.Sim.EditorTools
{
    /// <summary>
    /// Builds a scene that exercises every rule, so the system can be played rather than only reasoned
    /// about. Creates the archetype registry and the bullet prefab too, since a body's archetype id has to
    /// agree between the registry, the prefab and the gun that fires it — a mismatch there produces a
    /// confusing runtime failure rather than a compile error.
    /// </summary>
    public static class PlaytestSceneBuilder
    {
        const int BulletArchetype = 1;
        const int CopyArchetype = 2;

        const string SettingsFolder = "Assets/Settings";
        const string PrefabFolder = "Assets/Prefabs";
        const string RegistryPath = SettingsFolder + "/ArchetypeRegistry.asset";
        const string BulletPath = PrefabFolder + "/SimBullet.prefab";
        const string CopyPath = PrefabFolder + "/SimInvertedCopy.prefab";
        const string ScenePath = "Assets/Scenes/Playtest.unity";
        const string SpritePath = SettingsFolder + "/SimWhite.png";

        [MenuItem("Chronomancers/Build Playtest Scene")]
        public static void Build()
        {
            EnsureFolder(SettingsFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder("Assets/Scenes");

            var sprite = EnsureSprite();
            var bullet = BuildBulletPrefab(sprite);
            var copy = BuildCopyPrefab(sprite);
            var registry = BuildRegistry(bullet, copy);

            // Replacing the scene destroys whatever an inspector is currently drawing, and some package
            // editors (URP's volume component editor, for one) throw from OnEnable when their target has
            // become null. Dropping the selection first avoids the noise; it is not our exception.
            Selection.activeObject = null;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildScenery(sprite);

            // Ids are left to SimBody's own assignment so the scene agrees with what a human editing it
            // would get. Only the archetype has to be forced, because it decides how a body materialises.
            var alice = BuildCharacter("Alice", new Vector3(-6f, 0f, 0f), 1f, sprite);
            var bob = BuildCharacter("Bob", new Vector3(-6f, 3f, 0f), 0.35f, sprite);
            BuildCrate(new Vector3(-2.5f, -2f, 0f), sprite);
            BuildDoor(new Vector3(2f, 0f, 0f), sprite);
            BuildMachine(new Vector3(7f, -3f, 0f), sprite);

            var simulation = new GameObject("Simulation");
            var runner = simulation.AddComponent<SimRunner>();
            simulation.AddComponent<PlayerIntentSource>();
            simulation.AddComponent<SimHud>();

            var spawnParent = new GameObject("Spawned").transform;

            var so = new SerializedObject(runner);
            so.FindProperty("archetypes").objectReferenceValue = registry;
            so.FindProperty("spawnParent").objectReferenceValue = spawnParent;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runner);

            // Read back rather than assumed. This reference failed to persist once, and a null registry only
            // shows up much later as a body that cannot be spawned.
            var check = new SerializedObject(runner).FindProperty("archetypes").objectReferenceValue;
            if (check == null)
                Debug.LogError("failed to assign the ArchetypeRegistry on SimRunner; assign it by hand");

            EditorSceneManager.SaveScene(alice.scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"playtest scene built at {ScenePath}. " +
                      $"Characters: {alice.name} (rate 1), {bob.name} (rate 0.35, bullet time).");
        }

        // ------------------------------------------------------------------ content

        // Prefabs are always regenerated rather than reused. Re-running this is the repair path when a
        // script reference in a prefab has gone stale, so quietly keeping the old asset would defeat it.
        static GameObject BuildBulletPrefab(Sprite sprite)
        {
            var root = new GameObject("SimBullet");
            root.transform.localScale = new Vector3(0.25f, 0.25f, 1f);

            Art(root, sprite, new Color(1f, 0.85f, 0.3f));

            var rigidbody = root.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;

            root.AddComponent<CircleCollider2D>();
            root.AddComponent<SimTransform>();
            root.AddComponent<SimRigidbody2D>();
            root.AddComponent<SimBullet>();
            root.AddComponent<SimDamageSource>();

            var body = root.AddComponent<SimBody>();
            SetArchetype(body, BulletArchetype);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BulletPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// The inverted copy the reversal machine emits. It is an ordinary character whose
        /// <see cref="SimRate"/> is negative — watching it is what drives the cursor backwards, and that is
        /// the entirety of the mechanism (rule 2). Nothing about it is special-cased.
        /// </summary>
        static GameObject BuildCopyPrefab(Sprite sprite)
        {
            var root = BuildCharacter("InvertedCopy", Vector3.zero, -1f, sprite);
            SetArchetype(root.GetComponent<SimBody>(), CopyArchetype);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CopyPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static ArchetypeRegistry BuildRegistry(GameObject bullet, GameObject copy)
        {
            var registry = AssetDatabase.LoadAssetAtPath<ArchetypeRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<ArchetypeRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }

            var so = new SerializedObject(registry);
            var entries = so.FindProperty("entries");
            entries.arraySize = 2;

            SetEntry(entries.GetArrayElementAtIndex(0), BulletArchetype, bullet, 64);
            SetEntry(entries.GetArrayElementAtIndex(1), CopyArchetype, copy, 4);
            so.ApplyModifiedPropertiesWithoutUndo();

            return registry;
        }

        static void SetEntry(SerializedProperty entry, int id, GameObject prefab, int maxIdle)
        {
            entry.FindPropertyRelative("id").intValue = id;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("maxIdle").intValue = maxIdle;
        }

        static void BuildCamera()
        {
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.tag = "MainCamera";
        }

        /// <summary>
        /// Two rooms joined by a single doorway, seen from above. Walls are deliberately <i>not</i> simulated
        /// bodies: they never move, so recording them would spend a sample per step to say so, and a body
        /// with no channel worth recording is exactly what should stay ordinary scenery.
        /// <para>
        /// The layout is the point. One doorway means the door is the only way between rooms, so shutting it
        /// on a past self's recorded path is a thing that actually happens in play rather than something you
        /// have to contrive.
        /// </para>
        /// </summary>
        static void BuildScenery(Sprite sprite)
        {
            var wall = new Color(0.3f, 0.3f, 0.36f);

            Block("WallNorth", new Vector3(0f, 5.5f, 0f), new Vector3(20f, 1f, 1f), sprite, wall);
            Block("WallSouth", new Vector3(0f, -5.5f, 0f), new Vector3(20f, 1f, 1f), sprite, wall);
            Block("WallWest", new Vector3(-9.5f, 0f, 0f), new Vector3(1f, 12f, 1f), sprite, wall);
            Block("WallEast", new Vector3(9.5f, 0f, 0f), new Vector3(1f, 12f, 1f), sprite, wall);

            // The divider, with a gap at y in [-1, 1] for the door to fill.
            Block("DividerNorth", new Vector3(2f, 3.25f, 0f), new Vector3(0.6f, 3.5f, 1f), sprite, wall);
            Block("DividerSouth", new Vector3(2f, -3.25f, 0f), new Vector3(0.6f, 3.5f, 1f), sprite, wall);
        }

        static GameObject Block(string name, Vector3 position, Vector3 scale, Sprite sprite, Color tint)
        {
            var block = new GameObject(name);
            block.transform.position = position;
            block.transform.localScale = scale;
            Art(block, sprite, tint);
            block.AddComponent<BoxCollider2D>();
            return block;
        }

        static GameObject BuildCharacter(string name, Vector3 position, float rate, Sprite sprite)
        {
            var character = new GameObject(name);
            character.transform.position = position;
            character.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

            Art(character, sprite, rate < 1f ? new Color(0.5f, 0.8f, 1f) : new Color(1f, 0.6f, 0.6f));

            character.AddComponent<BoxCollider2D>();
            var rigidbody = character.AddComponent<Rigidbody2D>();

            // Top-down: gravity is sideways here, so there is none. Rotation is driven from aim rather than
            // by physics, so freeze it — a bullet impact must not set the character spinning.
            rigidbody.gravityScale = 0f;
            rigidbody.freezeRotation = true;

            character.AddComponent<SimTransform>();
            character.AddComponent<SimRigidbody2D>();
            character.AddComponent<SimCharacter>();
            character.AddComponent<SimHealth>();
            character.AddComponent<SimRate>().rate = rate;

            // The gun is a child so the muzzle can sit apart from the body's centre. It rotates with the
            // body, which SimCharacter turns to match aim.
            var gun = new GameObject("Gun");
            gun.transform.SetParent(character.transform, false);
            gun.transform.localPosition = Vector3.zero;
            gun.AddComponent<SimGun>();

            character.AddComponent<SimBody>();
            return character;
        }

        static GameObject BuildCrate(Vector3 position, Sprite sprite)
        {
            var crate = new GameObject("Crate");
            crate.transform.position = position;
            crate.transform.localScale = new Vector3(1f, 1f, 1f);

            Art(crate, sprite, new Color(0.8f, 0.65f, 0.4f));
            crate.AddComponent<BoxCollider2D>();

            var rigidbody = crate.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;

            // Damped, or a crate shoved once would slide across the room for the rest of the loop. It still
            // has to keep moving after you look away, though — that is rule 11, and the damping only decides
            // how far it gets.
            rigidbody.linearDamping = 4f;
            rigidbody.angularDamping = 4f;

            crate.AddComponent<SimTransform>();
            crate.AddComponent<SimRigidbody2D>();
            crate.AddComponent<SimBody>();
            return crate;
        }

        static GameObject BuildDoor(Vector3 position, Sprite sprite)
        {
            var door = new GameObject("Door");
            door.transform.position = position;
            door.transform.localScale = new Vector3(0.6f, 2f, 1f); // exactly fills the gap in the divider

            var art = Art(door, sprite, new Color(0.7f, 0.4f, 0.8f));
            door.AddComponent<BoxCollider2D>();

            // No Rigidbody2D: a door does not move, so SimTransform writes the transform directly and
            // SimRigidbody2D would have nothing to record.
            door.AddComponent<SimTransform>();
            SetReference(door.AddComponent<SimDoor>(), "art", art);
            door.AddComponent<SimBody>();
            return door;
        }

        static GameObject BuildMachine(Vector3 position, Sprite sprite)
        {
            var machine = new GameObject("ReversalMachine");
            machine.transform.position = position;
            machine.transform.localScale = new Vector3(1.6f, 1.6f, 1f);

            var art = Art(machine, sprite, new Color(0.4f, 1f, 0.8f));

            // A trigger, so standing in the machine does not also mean bumping into it. Overlap queries
            // still find it, which is all the interact scan needs.
            machine.AddComponent<BoxCollider2D>().isTrigger = true;

            machine.AddComponent<SimTransform>();
            SetReference(machine.AddComponent<SimTimeMachine>(), "art", art);
            machine.AddComponent<SimBody>();
            return machine;
        }

        // ------------------------------------------------------------------ plumbing

        static SpriteRenderer Art(GameObject target, Sprite sprite, Color tint)
        {
            var renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            return renderer;
        }

        /// <summary>Wires a component's private serialized reference without opening up its API.</summary>
        static void SetReference(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArchetype(SimBody body, int archetype)
        {
            var so = new SerializedObject(body);
            so.FindProperty("archetype").intValue = archetype;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>
        /// A 1x1 white sprite, scaled per object. Enough to see what is happening without importing art,
        /// and it keeps the scene readable as pure geometry.
        /// </summary>
        static Sprite EnsureSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (existing != null) return existing;

            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(SpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 1f;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }
    }
}
