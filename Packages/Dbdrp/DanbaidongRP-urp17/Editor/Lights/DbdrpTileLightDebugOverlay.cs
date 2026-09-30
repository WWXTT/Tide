using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    /// <summary>
    /// SceneView overlay that splits the view into cluster-sized tiles and labels how many
    /// light bounds cover each tile — the deferred-pipeline counterpart of the 2D-only
    /// Light Batching Debugger. Toggle via Window/Rendering/Dbdrp Tile Light Debug.
    /// </summary>
    [InitializeOnLoad]
    internal static class DbdrpTileLightDebugOverlay
    {
        const string kMenuPath = "Window/Rendering/Dbdrp Tile Light Debug";
        const int kTileSize = 32; // matches LightDefinitions.s_TileSizeClustered

        static readonly Color kCold = new Color(0.2f, 0.9f, 0.3f, 0.35f);
        static readonly Color kHot = new Color(0.95f, 0.25f, 0.15f, 0.45f);

        // Created lazily inside the GUI pass: EditorStyles is not ready when the static
        // constructor runs during domain reload.
        static GUIStyle s_TileLabel;
        static GUIStyle tileLabel => s_TileLabel ?? (s_TileLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });

        static bool s_Enabled;
        static int[] s_TileCounts = new int[0];
        static double s_NextRepaintTime;

        static DbdrpTileLightDebugOverlay()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += PollRepaint;
        }

        static void PollRepaint()
        {
            // Idle SceneViews stop repainting on their own; keep the overlay live at a low rate.
            if (s_Enabled && EditorApplication.timeSinceStartup >= s_NextRepaintTime)
            {
                s_NextRepaintTime = EditorApplication.timeSinceStartup + 0.25;
                SceneView.RepaintAll();
            }
        }

        [MenuItem(kMenuPath)]
        static void Toggle()
        {
            s_Enabled = !s_Enabled;
            SceneView.RepaintAll();
        }

        [MenuItem(kMenuPath, true)]
        static bool Validate()
        {
            Menu.SetChecked(kMenuPath, s_Enabled);
            return true;
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (!s_Enabled || sceneView.camera == null)
                return;

            if (!DbdrpLightDebugData.byCamera.TryGetValue(sceneView.camera.GetEntityId().GetHashCode(), out var snapshot))
            {
                // Distinguish "the pipeline never built light data for this view" from
                // "built, but no light bounds cover any tile".
                Handles.BeginGUI();
                GUI.Label(new Rect(8, 24, 700, 16),
                    "Dbdrp Tile Light Debug: no GPU light data built for this view's camera yet.",
                    EditorStyles.boldLabel);
                Handles.EndGUI();
                return;
            }
            // Guard against drawing a stale snapshot after this view stops re-rendering.
            if (Time.frameCount - snapshot.frame > 120)
                return;

            var cam = sceneView.camera;
            int camWidth = cam.pixelWidth;
            int camHeight = cam.pixelHeight;
            if (camWidth <= 0 || camHeight <= 0 || snapshot.lightCount == 0)
                return;

            int tilesX = Mathf.CeilToInt(camWidth / (float)kTileSize);
            int tilesY = Mathf.CeilToInt(camHeight / (float)kTileSize);
            if (s_TileCounts.Length < tilesX * tilesY)
                s_TileCounts = new int[tilesX * tilesY];
            else
                System.Array.Clear(s_TileCounts, 0, tilesX * tilesY);

            // Project each light bound (view space) to a screen-space AABB and stamp the tiles
            // it covers. Camera-relative inaccuracies don't matter for a debug readout.
            int maxCount = 0;
            for (int i = 0; i < snapshot.lightCount; i++)
            {
                var b = snapshot.bounds[i];
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                var proj = snapshot.projection;
                bool anyInFront = false;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 v = b.center
                        + ((corner & 1) == 0 ? b.axisX : -b.axisX)
                        + ((corner & 2) == 0 ? b.axisY : -b.axisY)
                        + ((corner & 4) == 0 ? b.axisZ : -b.axisZ);
                    Vector4 clip = proj * new Vector4(v.x, v.y, v.z, 1.0f);
                    if (clip.w <= 0.0f)
                        continue;
                    anyInFront = true;
                    float px = (clip.x / clip.w * 0.5f + 0.5f) * camWidth;
                    // GL-style projection: +y is up, GUI/tile space: +y is down.
                    float py = (0.5f - clip.y / clip.w * 0.5f) * camHeight;
                    minX = Mathf.Min(minX, px); maxX = Mathf.Max(maxX, px);
                    minY = Mathf.Min(minY, py); maxY = Mathf.Max(maxY, py);
                }
                if (!anyInFront)
                    continue;

                int tx0 = Mathf.Clamp((int)(minX / kTileSize), 0, tilesX - 1);
                int tx1 = Mathf.Clamp((int)(maxX / kTileSize), 0, tilesX - 1);
                int ty0 = Mathf.Clamp((int)(minY / kTileSize), 0, tilesY - 1);
                int ty1 = Mathf.Clamp((int)(maxY / kTileSize), 0, tilesY - 1);
                for (int ty = ty0; ty <= ty1; ty++)
                {
                    for (int tx = tx0; tx <= tx1; tx++)
                    {
                        int count = ++s_TileCounts[ty * tilesX + tx];
                        if (count > maxCount)
                            maxCount = count;
                    }
                }
            }

            Handles.BeginGUI();
            // Compute the camera-image rect directly instead of reserving it via GUILayout:
            // early returns above make the layout/repaint control counts diverge, which IMGUI
            // rejects. The image fills the window width and is anchored to the bottom, so the
            // offset above it is whatever toolbars the SceneView draws.
            float scaleX = sceneView.position.width / camWidth;
            float guiImageHeight = camHeight * scaleX;
            float topOffset = sceneView.position.height - guiImageHeight;
            float guiLeft = 0f;

            for (int ty = 0; ty < tilesY; ty++)
            {
                for (int tx = 0; tx < tilesX; tx++)
                {
                    int count = s_TileCounts[ty * tilesX + tx];
                    if (count == 0)
                        continue;

                    var tileRect = new Rect(
                        guiLeft + tx * kTileSize * scaleX,
                        topOffset + ty * kTileSize * scaleX,
                        kTileSize * scaleX,
                        kTileSize * scaleX);

                    float t = maxCount > 1 ? count / (float)maxCount : 0.0f;
                    EditorGUI.DrawRect(tileRect, Color.Lerp(kCold, kHot, t));
                    GUI.Label(tileRect, count.ToString(), tileLabel);
                }
            }

            GUI.Label(new Rect(guiLeft + 8, topOffset + 4, 900, 16),
                $"Tile size {kTileSize}px  |  Lights: {snapshot.lightCount} (punctual {snapshot.punctualLights}, area {snapshot.areaLights}, dir {snapshot.directionalLights})  |  tile max {maxCount}  |  frame {snapshot.frame}",
                EditorStyles.boldLabel);
            Handles.EndGUI();
        }
    }
}
