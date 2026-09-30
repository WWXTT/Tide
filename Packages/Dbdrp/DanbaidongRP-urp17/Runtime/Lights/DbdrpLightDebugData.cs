using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Per-camera snapshots of the last-built GPU light bounds for the editor
    /// "Dbdrp Tile Light Debug" overlay. Bounds are in the build-time (Z-flipped) view
    /// space of the camera that produced them; readers must use the paired matrices to
    /// project back to screen. Kept per camera so concurrent Game/SceneView rendering
    /// cannot overwrite each other's data.
    /// </summary>
    public static class DbdrpLightDebugData
    {
        public struct DebugLightBound
        {
            public Vector3 center;  // view space
            public Vector3 axisX;   // half-extent scaled axes
            public Vector3 axisY;
            public Vector3 axisZ;
            public float radius;    // circumscribed sphere in view space
        }

        public struct Snapshot
        {
            public int cameraKey;           // GetEntityId().GetHashCode() of the source camera
            public int frame;
            public int lightCount;          // valid entries in bounds
            public DebugLightBound[] bounds;
            public Matrix4x4 worldToView;   // build-time matrix (Z-flipped)
            public Matrix4x4 projection;    // GL-style projection premultiplied by the Z flip
            public int punctualLights;      // Point + Spot
            public int areaLights;          // Rectangle + Disc + Tube
            public int directionalLights;
        }

        public static readonly Dictionary<int, Snapshot> byCamera = new Dictionary<int, Snapshot>();

        /// <summary>Drops entries from cameras that stopped rendering (e.g. closed views).</summary>
        internal static void Prune(int currentFrame, int maxAgeFrames = 300)
        {
            List<int> stale = null;
            foreach (var kv in byCamera)
            {
                if (currentFrame - kv.Value.frame > maxAgeFrames)
                {
                    if (stale == null) stale = new List<int>();
                    stale.Add(kv.Key);
                }
            }
            if (stale != null)
            {
                foreach (var key in stale)
                    byCamera.Remove(key);
            }
        }
    }
}
