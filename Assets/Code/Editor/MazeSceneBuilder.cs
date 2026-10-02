using Maze;
using Maze.Gameplay;
using Maze.Presentation;
using Maze.UI;
using UnityEditor;
using UnityEngine;

namespace Maze.EditorTools
{
    /// <summary>
    /// Builds the complete demo rig (generator, player, camera, HUD, minimap and solver) into the
    /// active scene. Lets a user drop the whole project into an existing scene with one click, and
    /// documents in code how the components of the demo work together.
    /// </summary>
    public static class MazeSceneBuilder
    {
        private const string WallPrefabPath = "Assets/Prefabs/Wall.prefab";

        /// <summary>Creates the full demo rig. Returns the created generator.</summary>
        public static MazeGenerator BuildDemoRig()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Maze Demo Rig");

            GameObject root = new GameObject("Maze Demo");
            Undo.RegisterCreatedObjectUndo(root, "Create Maze Demo Rig");

            // --- maze ---------------------------------------------------------
            GameObject generatorObject = new GameObject("Maze Generator");
            generatorObject.transform.SetParent(root.transform, false);
            MazeGenerator generator = generatorObject.AddComponent<MazeGenerator>();
            generator.WallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
            generator.Settings.MazeWidth = 21;
            generator.Settings.MazeHeight = 21;
            generator.Settings.OpeningMode = Maze.Core.MazeOpeningMode.FixedEntranceAndExit;
            Undo.RegisterCreatedObjectUndo(generatorObject, "Create Maze Generator");

            // --- floor --------------------------------------------------------
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = Vector3.zero;
            floor.transform.localScale = new Vector3(6f, 1f, 6f);
            Undo.RegisterCreatedObjectUndo(floor, "Create Ground");

            // --- light --------------------------------------------------------
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            Undo.RegisterCreatedObjectUndo(lightObject, "Create Light");

            // --- player -------------------------------------------------------
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.SetParent(root.transform, false);
            player.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            Collider capsuleCollider = player.GetComponent<Collider>();
            if (capsuleCollider != null)
            {
                Object.DestroyImmediate(capsuleCollider);
            }

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.9f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.95f, 0f);
            MazePlayerController playerController = player.AddComponent<MazePlayerController>();
            Undo.RegisterCreatedObjectUndo(player, "Create Player");

            // --- camera -------------------------------------------------------
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.15f;
            cameraObject.AddComponent<AudioListener>();
            MazeCameraRig cameraRig = cameraObject.AddComponent<MazeCameraRig>();
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Camera");

            // --- solution path -------------------------------------------------
            GameObject pathObject = new GameObject("Solution Path");
            pathObject.transform.SetParent(generatorObject.transform, false);
            MazePathRenderer pathRenderer = pathObject.AddComponent<MazePathRenderer>();
            Undo.RegisterCreatedObjectUndo(pathObject, "Create Path Renderer");

            // --- minimap + HUD --------------------------------------------------
            GameObject hudObject = new GameObject("Maze HUD");
            hudObject.transform.SetParent(root.transform, false);
            MazeMinimap minimap = hudObject.AddComponent<MazeMinimap>();
            MazeObjectiveTracker objective = hudObject.AddComponent<MazeObjectiveTracker>();
            MazeHud hud = hudObject.AddComponent<MazeHud>();
            Undo.RegisterCreatedObjectUndo(hudObject, "Create HUD");

            // Wire the references so that nothing has to be dragged in the inspector.
            AssignReference(playerController, "_generator", generator);
            AssignReference(cameraRig, "_generator", generator);
            AssignReference(cameraRig, "_player", playerController);
            AssignReference(pathRenderer, "_generator", generator);
            AssignReference(minimap, "_generator", generator);
            AssignReference(minimap, "_player", playerController);
            AssignReference(objective, "_generator", generator);
            AssignReference(objective, "_player", playerController);
            AssignReference(objective, "_pathRenderer", pathRenderer);
            AssignReference(hud, "_generator", generator);
            AssignReference(hud, "_objective", objective);
            AssignReference(hud, "_minimap", minimap);
            AssignReference(hud, "_cameraRig", cameraRig);

            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = generatorObject;
            EditorGUIUtility.PingObject(generatorObject);
            generator.Generate();
            return generator;
        }

        /// <summary>
        /// Assigns a private <c>[SerializeField]</c> through <see cref="SerializedObject"/> so the
        /// builder does not need public setters for every reference.
        /// </summary>
        private static void AssignReference(Object component, string fieldName, Object value)
        {
            if (component == null || value == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning("[Maze] Could not wire '" + fieldName + "' on " + component.GetType().Name +
                                 " - the field name changed?");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
