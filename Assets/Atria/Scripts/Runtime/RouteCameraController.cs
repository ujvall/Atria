using Atria.Core.Navigation;
using UnityEngine;

namespace Atria.App
{
    /// <summary>
    /// First-person camera driven purely by route progress (0..1). No physics, character
    /// controller or simulation: the pose is a direct function of progress along a precomputed
    /// <see cref="RoutePath"/>, so scrubbing forwards and backwards is exact and repeatable.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class RouteCameraController : MonoBehaviour
    {
        [Tooltip("Eye height above the floor, metres.")]
        public float EyeHeight = 1.6f;
        [Tooltip("Downward tilt in degrees, helps show the floor guide line and door signs together.")]
        public float PitchDegrees = 6f;
        [Tooltip("Horizontal field of view in degrees; converted to Unity's vertical FOV for the current aspect.")]
        public float HorizontalFov = 75f;
        [Tooltip("Upper limit for the vertical FOV on very tall/narrow viewports.")]
        public float MaxVerticalFov = 90f;
        [Tooltip("Seconds to ease from the previous pose when a different route is loaded.")]
        public float RouteChangeBlendSeconds = 0.4f;

        private Camera _camera;
        private RoutePath _path;
        private float _progress;
        private float _lastAspect = -1f;
        private Vector3 _blendFromPosition;
        private Quaternion _blendFromRotation;
        private float _blendElapsed = float.MaxValue;

        public RoutePath Path => _path;
        public Camera Camera => _camera != null ? _camera : (_camera = GetComponent<Camera>());

        public float Progress
        {
            get => _progress;
            set
            {
                _progress = RoutePath.Clamp01(value);
                ApplyPose();
            }
        }

        /// <summary>Loads a route. If a route was already showing, the camera eases to the new start instead of jumping.</summary>
        public void SetRoute(RoutePath path, float progress = 0f)
        {
            bool blend = _path != null && RouteChangeBlendSeconds > 0f;
            if (blend)
            {
                _blendFromPosition = transform.position;
                _blendFromRotation = transform.rotation;
                _blendElapsed = 0f;
            }
            _path = path;
            _progress = RoutePath.Clamp01(progress);
            if (!blend) ApplyPose();
        }

        public void GetPose(float progress, out Vector3 position, out Quaternion rotation)
        {
            float p = RoutePath.Clamp01(progress);
            position = RouteVisuals.ToUnity(_path.PositionAtProgress(p)) + Vector3.up * EyeHeight;
            rotation = Quaternion.Euler(PitchDegrees, _path.YawAtProgress(p), 0f);
        }

        private void LateUpdate()
        {
            UpdateFieldOfView();
            if (_path == null) return;
            if (_blendElapsed < RouteChangeBlendSeconds)
            {
                _blendElapsed += Time.unscaledDeltaTime;
                GetPose(_progress, out Vector3 pos, out Quaternion rot);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_blendElapsed / RouteChangeBlendSeconds));
                transform.SetPositionAndRotation(Vector3.Lerp(_blendFromPosition, pos, t), Quaternion.Slerp(_blendFromRotation, rot, t));
            }
            else ApplyPose();
        }

        private void ApplyPose()
        {
            if (_path == null || _blendElapsed < RouteChangeBlendSeconds) return;
            GetPose(_progress, out Vector3 pos, out Quaternion rot);
            transform.SetPositionAndRotation(pos, rot);
        }

        private void UpdateFieldOfView()
        {
            Camera cam = Camera;
            if (Mathf.Approximately(cam.aspect, _lastAspect)) return;
            _lastAspect = cam.aspect;
            float vertical = UnityEngine.Camera.HorizontalToVerticalFieldOfView(HorizontalFov, cam.aspect);
            cam.fieldOfView = Mathf.Min(vertical, MaxVerticalFov);
        }

        /// <summary>Forces a FOV recalculation, e.g. after the viewport rect changes.</summary>
        public void InvalidateFieldOfView() => _lastAspect = -1f;
    }
}
