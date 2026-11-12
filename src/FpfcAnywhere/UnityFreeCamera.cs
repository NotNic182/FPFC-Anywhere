using System.Collections.Generic;
using FpfcAnywhere.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace FpfcAnywhere
{
    /// <summary>
    /// A private clone of the game's camera that exists only on this PC.
    ///
    /// This is the core of the safety design. In 1.45.3 and 1.46.0 PlayerTransforms reads the game's
    /// MainCamera as the player's head, and MultiplayerLocal(In)activeClient sends that value to
    /// other players. So the game's camera is never moved here. The one thing written on it is
    /// the draw type on its URP data, Overlay while the free camera shows so the scene is not
    /// drawn twice, and Base again the moment it is not; position, rotation, enabled and tracking
    /// stay the game's. Flying this clone instead changes what the desktop shows and nothing
    /// else, and because nothing else knows the clone exists, nothing can move it back into the
    /// floor.
    /// </summary>
    internal sealed class UnityFreeCamera : IFreeCamera
    {
        // XRMirrorViewBlitMode "none", the value the game's own SmoothCamera uses.
        private const int MirrorBlitNone = -6;

        // How often to look for a better camera while the current one still works.
        private const float SourceCheckSeconds = 0.25f;

        // The object EnvironmentShaderWarmup hangs under the game camera for the one frame it draws.
        private const string WarmupName = "ShaderWarmup";

        private readonly FlightSettings _settings;
        private readonly HiddenDraw _hiddenDraw;
        private readonly List<XRDisplaySubsystem> _displays = new List<XRDisplaySubsystem>();
        private readonly List<CameraCandidate> _candidates = new List<CameraCandidate>();
        private GameObject? _holder;
        private Camera? _camera;
        private Camera? _source;
        private XRDisplaySubsystem? _mirroredDisplay;
        private int _originalMirrorMode;
        private float _nextSourceCheck;
        private float _builtAt;
        private bool _sourceSkipped;

        public UnityFreeCamera(FlightSettings settings)
        {
            _settings = settings;
            _hiddenDraw = new HiddenDraw(settings);
        }

        public bool IsAlive => _camera != null;

        public Transform? Transform => _camera != null ? _camera.transform : null;

        public Camera? GameCamera => _source;

        /// <summary>True while the game camera's own draw is switched off.</summary>
        public bool GameDrawSkipped => _sourceSkipped;

        public bool TryCreate(out Vec3 sourcePosition, out float sourceYaw)
        {
            sourcePosition = Vec3.Zero;
            sourceYaw = 0f;

            var source = FindGameCamera(null);
            if (source == null)
            {
                return false;
            }

            var position = source.transform.position;
            sourcePosition = new Vec3(position.x, position.y, position.z);
            sourceYaw = source.transform.eulerAngles.y;

            Build(source);
            return true;
        }

        public bool RefreshSource()
        {
            if (_camera == null)
            {
                return false;
            }

            var sourceRunning = _source != null && _source.isActiveAndEnabled;
            if (sourceRunning && Time.unscaledTime < _nextSourceCheck)
            {
                return false;
            }

            _nextSourceCheck = Time.unscaledTime + SourceCheckSeconds;

            var picked = FindGameCamera(sourceRunning ? _source : null);
            if (picked == null || picked == _source)
            {
                return false;
            }

            var previous = _holder;
            SetSourceSkipped(false);
            Build(picked);
            Object.Destroy(previous);
            return true;
        }

        public void Apply(FlightPose pose)
        {
            if (_camera == null)
            {
                return;
            }

            var position = new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z);
            _camera.transform.SetPositionAndRotation(position, Quaternion.Euler(pose.Pitch, pose.Yaw, 0f));
        }

        public void SetVisible(bool visible)
        {
            if (_camera != null)
            {
                _camera.enabled = visible;
            }

            Cursor.lockState = visible ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !visible;
            SetDesktopMirrorOff(visible);
        }

        /// <summary>
        /// Leaves the cursor alone on purpose. The menu's stock FPFC locks it again when it wakes
        /// up, and that can happen before or after this runs, so touching it here could undo that.
        /// </summary>
        public void Destroy()
        {
            SetSourceSkipped(false);
            SetDesktopMirrorOff(false);

            if (_holder != null)
            {
                Object.Destroy(_holder);
            }

            _holder = null;
            _camera = null;
            _source = null;
        }

        /// <summary>Copies what the camera should draw from the game's camera.</summary>
        public void FollowGameCamera()
        {
            if (_camera != null && _source != null)
            {
                _camera.cullingMask = _source.cullingMask;
            }
        }

        /// <summary>
        /// Skips the game camera's own draw while the rules allow it and hands it back otherwise.
        /// Called once a frame. See the class comment for why this is the only write to that camera.
        /// </summary>
        public void UpdateGameDraw(bool freeCamShowing)
        {
            var skip = _camera != null
                && _source != null
                && _hiddenDraw.CanSkip(freeCamShowing, Time.unscaledTime - _builtAt, _source.transform.Find(WarmupName) != null, XRSettings.isDeviceActive);

            SetSourceSkipped(skip);
        }

        private Camera? FindGameCamera(Camera? current)
        {
            var cameras = Camera.allCameras;
            var currentIndex = GameCameraPicker.None;

            _candidates.Clear();
            foreach (var camera in cameras)
            {
                if (camera == current)
                {
                    currentIndex = _candidates.Count;
                }

                _candidates.Add(new CameraCandidate(
                    camera.CompareTag("MainCamera"),
                    camera.gameObject.scene.name,
                    camera.targetTexture == null,
                    camera.stereoTargetEye != StereoTargetEyeMask.None));
            }

            var index = GameCameraPicker.PickSource(_candidates, currentIndex);
            return index == GameCameraPicker.None ? null : cameras[index];
        }

        private void Build(Camera source)
        {
            // Cloning under an inactive parent means no script on the clone gets to run before it
            // is stripped, so head tracking and injected game logic never wake up on it.
            _holder = new GameObject("FpfcAnywhere.Holder");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);

            var clone = Object.Instantiate(source.gameObject, _holder.transform);
            clone.name = "FpfcAnywhere.Camera";
            clone.tag = "Untagged";
            Strip(clone);

            var camera = clone.GetComponent<Camera>();
            camera.enabled = true;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.targetDisplay = 0;
            camera.targetTexture = null;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.depth = source.depth + 1f;
            camera.fieldOfView = _settings.FieldOfView;
            camera.depthTextureMode = source.depthTextureMode;
            camera.cullingMask = source.cullingMask;
            camera.ResetAspect();
            camera.GetUniversalAdditionalCameraData().allowXRRendering = false;

            _camera = camera;
            _source = source;
            _builtAt = Time.unscaledTime;
            _holder.SetActive(true);

            ModLog.Info($"Cloned '{source.name}' (tag {source.tag}, mask 0x{source.cullingMask:X8}) from scene '{source.gameObject.scene.name}'");
        }

        private static void Strip(GameObject clone)
        {
            for (var index = clone.transform.childCount - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(clone.transform.GetChild(index).gameObject);
            }

            foreach (var component in clone.GetComponents<Component>())
            {
                if (!CloneRules.Keep(component.GetType().FullName))
                {
                    Object.DestroyImmediate(component);
                }
            }
        }

        // Takes over a camera that draws as Base only, and gives it back only while it is still
        // Overlay, so a draw type the game set itself is left alone either way. A source that has
        // already gone with its scene has nothing to put back.
        private void SetSourceSkipped(bool skip)
        {
            if (skip == _sourceSkipped)
            {
                return;
            }

            if (_source == null)
            {
                _sourceSkipped = false;
                return;
            }

            var data = _source.GetUniversalAdditionalCameraData();
            if (skip)
            {
                if (data.renderType == CameraRenderType.Base)
                {
                    data.renderType = CameraRenderType.Overlay;
                    _sourceSkipped = true;
                }
            }
            else
            {
                if (data.renderType == CameraRenderType.Overlay)
                {
                    data.renderType = CameraRenderType.Base;
                }

                _sourceSkipped = false;
            }
        }

        // With a headset running, Unity would otherwise blit the headset view over the desktop.
        // This is the same switch the game's own SmoothCamera uses; without XR it does nothing.
        private void SetDesktopMirrorOff(bool off)
        {
            if (off && _mirroredDisplay == null)
            {
                _displays.Clear();
                SubsystemManager.GetSubsystems(_displays);
                foreach (var display in _displays)
                {
                    if (display.running)
                    {
                        _mirroredDisplay = display;
                        _originalMirrorMode = display.GetPreferredMirrorBlitMode();
                        display.SetPreferredMirrorBlitMode(MirrorBlitNone);
                        break;
                    }
                }
            }
            else if (!off && _mirroredDisplay != null)
            {
                _mirroredDisplay.SetPreferredMirrorBlitMode(_originalMirrorMode);
                _mirroredDisplay = null;
            }
        }
    }
}
