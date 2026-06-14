// Runtime box-projected reflection probes — the single biggest fidelity win:
// without them every smoothness value reads as grey plastic; with them the
// polished marble floors mirror the walls, columns and gilt. The museum is
// static, so each probe is rendered ONCE (ViaScripting refresh) and then costs
// nothing per frame. Room probes are created/rendered with the distance-gated
// room contents; atrium/corridor probes are rendered once after lighting is set.
using UnityEngine;

namespace CameraObscura
{
    public static class ReflectionProbes
    {
        public static ReflectionProbe Create(Transform parent, Vector3 center, Vector3 size, int resolution, bool renderNow)
        {
            var go = new GameObject("reflection-probe");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            p.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            p.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            p.resolution = resolution;
            p.hdr = true;
            p.boxProjection = true;
            p.size = size;
            p.center = Vector3.zero;
            p.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
            p.cullingMask = ~0;
            p.shadowDistance = 30f;
            p.importance = 1;
            p.intensity = 1f;
            p.nearClipPlane = 0.1f;
            p.farClipPlane = 60f;
            if (renderNow) p.RenderProbe();
            return p;
        }
    }
}
