using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FpfcAnywhere.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Object = UnityEngine.Object;

namespace FpfcAnywhere
{
    /// <summary>
    /// Off unless the game is launched with --fpfc-anywhere-diag. It only reads: it logs the game
    /// camera and the free camera at three points in each frame, says when something other than
    /// the mod moves them, and records the head pose so the safety claim can be checked in a match.
    /// Game types are looked up by name so the mod itself never links to game code.
    /// </summary>
    internal sealed class Diagnostics : IDisposable
    {
        public const string Argument = "--fpfc-anywhere-diag";

        private const int FramesToLogPerSong = 300;
        private const float RigDumpDelaySeconds = 3f;
        private const float MovedThreshold = 0.0005f;
        private const int StatusLinesAfterSong = 5;

        private static readonly string[] WatchedTypes =
        {
            "FirstPersonFlyingController", "VRCenterAdjust", "PlayerTransforms", "SmoothCamera",
            "MultiplayerLocalActiveClient", "MultiplayerLocalInactiveClient", "MultiplayerSpectatorController",
            "MultiplayerConnectedPlayerFacade",
        };

        private readonly FreeCamSession _session;
        private readonly UnityFreeCamera _camera;
        private readonly Type? _playerTransformsType = FindGameType("PlayerTransforms");
        private Component? _playerTransforms;
        private SessionPhase _lastPhase;
        private float _nextStatus;
        private float _dumpAt = -1f;
        private int _framesLogged;
        private int _statusLinesLeft;
        private int _foreignWrites;
        private Vector3 _gameAtUpdate;
        private Vector3 _gameAtLateUpdate;
        private bool _sampledUpdate;
        private bool _sampledLateUpdate;

        public Diagnostics(FreeCamSession session, UnityFreeCamera camera)
        {
            _session = session;
            _camera = camera;
            Application.onBeforeRender += OnBeforeRender;
        }

        public void Dispose() => Application.onBeforeRender -= OnBeforeRender;

        public void OnScenes(IEnumerable<string> names) => ModLog.Info("Scenes [" + string.Join(", ", names) + "]");

        public void OnUpdate()
        {
            var game = GameCamera();
            _sampledUpdate = game != null;
            if (game != null)
            {
                _gameAtUpdate = game.transform.position;
            }
        }

        public void OnLateUpdate()
        {
            var phase = _session.Phase;
            if (phase != _lastPhase)
            {
                HandlePhaseChange(phase);
            }

            var game = GameCamera();
            _sampledLateUpdate = game != null;
            if (game != null)
            {
                _gameAtLateUpdate = game.transform.position;
            }

            if (_dumpAt > 0f && Time.unscaledTime > _dumpAt)
            {
                _dumpAt = -1f;
                ModLog.Info(DumpRig());
            }

            if (Time.unscaledTime > _nextStatus && (phase != SessionPhase.Idle || _statusLinesLeft > 0))
            {
                _nextStatus = Time.unscaledTime + 1f;
                _statusLinesLeft = phase == SessionPhase.Idle ? _statusLinesLeft - 1 : _statusLinesLeft;
                ModLog.Info(Status(game));
            }
        }

        private void HandlePhaseChange(SessionPhase phase)
        {
            if (phase == SessionPhase.Active)
            {
                _dumpAt = Time.unscaledTime + RigDumpDelaySeconds;
                _framesLogged = 0;
                _foreignWrites = 0;
            }
            else if (phase == SessionPhase.Idle)
            {
                _statusLinesLeft = StatusLinesAfterSong;
                _playerTransforms = null;
                ModLog.Info($"Teardown: {_foreignWrites} foreign writes to the free camera this song");
            }

            _lastPhase = phase;
        }

        // Unity destroys objects at the end of the frame, so this is only meaningful on a later one.
        private static int CountClones() => Resources.FindObjectsOfTypeAll<Camera>().Count(camera => camera.name.StartsWith("FpfcAnywhere", StringComparison.Ordinal));

        // The mod's source camera, which may be the untagged fallback. Camera.main covers the menu.
        private Camera? GameCamera()
        {
            var source = _camera.GameCamera;
            return source != null ? source : Camera.main;
        }

        private void OnBeforeRender()
        {
            var game = GameCamera();
            var free = _camera.Transform;
            var freeMoved = free != null && _session.FreeCamEnabled && (free.position - ToVector(_session.Pose.Position)).magnitude > MovedThreshold;
            if (freeMoved)
            {
                _foreignWrites++;
            }

            if (_session.Phase != SessionPhase.Active || game == null || _framesLogged >= FramesToLogPerSong)
            {
                return;
            }

            _framesLogged++;
            var atRender = game.transform.position;
            var movedInUpdate = _sampledUpdate && _sampledLateUpdate && (_gameAtLateUpdate - _gameAtUpdate).magnitude > MovedThreshold;
            var movedBeforeRender = _sampledLateUpdate && (atRender - _gameAtLateUpdate).magnitude > MovedThreshold;
            var writers = movedInUpdate || movedBeforeRender ? " candidates=[" + Writers(game) + "]" : string.Empty;

            ModLog.Info($"FRAME {Time.frameCount} game@Update={_gameAtUpdate:F3} game@LateUpdate={_gameAtLateUpdate:F3} game@Render={atRender:F3} free={(free != null ? free.position.ToString("F3") : "none")} foreign={freeMoved}{writers}");
        }

        private string Status(Camera? game)
        {
            return $"STATUS phase={_session.Phase} on={_session.FreeCamEnabled} free={_session.Pose} game={(game != null ? game.transform.position.ToString("F2") : "none")} head={HeadPose(game)} hands={HandsPose()} menu={_session.MenuOpen} foreignWrites={_foreignWrites} clones={CountClones()} cursor={Cursor.lockState}/{Cursor.visible} focus={Application.isFocused} fps={1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f):F0}";
        }

        // Reads the same property MultiplayerLocal(In)activeClient sends to other players.
        private string HeadPose(Camera? game)
        {
            if (_playerTransformsType == null)
            {
                return "type missing";
            }

            if (game == null)
            {
                return "none";
            }

            if (_playerTransforms == null || HeadOf(_playerTransforms) != game.transform)
            {
                _playerTransforms = FindLocalPlayerTransforms(game);
            }

            if (_playerTransforms == null)
            {
                return "none";
            }

            var property = _playerTransformsType.GetProperty("rawHeadPseudoLocalPos");
            return property != null ? ((Vector3)property.GetValue(_playerTransforms)).ToString("F3") : "property missing";
        }

        // The hands other players receive. These must not move when the menu pointer does.
        private string HandsPose()
        {
            if (_playerTransforms == null || _playerTransformsType == null)
            {
                return "none";
            }

            var left = _playerTransformsType.GetProperty("leftHandPseudoLocalPos")?.GetValue(_playerTransforms);
            var right = _playerTransformsType.GetProperty("rightHandPseudoLocalPos")?.GetValue(_playerTransforms);
            return left is Vector3 leftHand && right is Vector3 rightHand ? leftHand.ToString("F3") + rightHand.ToString("F3") : "property missing";
        }

        // Multiplayer has one PlayerTransforms per player, so any other match would be someone else's
        // head. The local one is the one whose head is the camera the mod is tracking.
        private Component? FindLocalPlayerTransforms(Camera game)
        {
            foreach (var item in Object.FindObjectsByType(_playerTransformsType!, FindObjectsSortMode.None).OfType<Component>())
            {
                if (HeadOf(item) == game.transform)
                {
                    ModLog.Info("Reading local head from " + PathOf(item.transform));
                    return item;
                }
            }

            return null;
        }

        private Transform? HeadOf(Component playerTransforms)
        {
            var field = _playerTransformsType?.GetField("_headTransform", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(playerTransforms) as Transform;
        }

        private static string Writers(Camera game)
        {
            var names = new List<string>();
            for (var current = game.transform; current != null; current = current.parent)
            {
                names.AddRange(current.GetComponents<Behaviour>().Where(behaviour => behaviour != null && behaviour.isActiveAndEnabled).Select(behaviour => current.name + "/" + behaviour.GetType().Name));
            }

            return string.Join(", ", names);
        }

        private static string DumpRig()
        {
            var text = new StringBuilder("--- RIG DUMP ---\n");
            text.Append($"XR loaded='{XRSettings.loadedDeviceName}' active={XRSettings.isDeviceActive} screen={Screen.width}x{Screen.height} runInBackground={Application.runInBackground}\n");

            foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>().Where(camera => camera.gameObject.scene.IsValid()))
            {
                text.Append($"CAMERA {PathOf(camera.transform)} active={camera.gameObject.activeInHierarchy} enabled={camera.enabled} depth={camera.depth} tag={camera.tag} eye={camera.stereoTargetEye} mask=0x{camera.cullingMask:X8} fov={camera.fieldOfView:F1} world={camera.transform.position:F2} local={camera.transform.localPosition:F2}\n");
                foreach (var component in camera.GetComponents<Component>())
                {
                    text.Append("    " + (component != null ? component.GetType().FullName : "missing script") + (component is Behaviour behaviour ? $" enabled={behaviour.enabled}" : string.Empty) + "\n");
                }
            }

            AppendPointers(text);

            foreach (var name in WatchedTypes)
            {
                var type = FindGameType(name);
                if (type == null)
                {
                    text.Append($"TYPE {name} NOT FOUND\n");
                    continue;
                }

                var found = Resources.FindObjectsOfTypeAll(type).OfType<Component>().Where(item => item.gameObject.scene.IsValid()).ToArray();
                text.Append($"TYPE {name} count={found.Length}\n");
                foreach (var item in found)
                {
                    text.Append($"    {PathOf(item.transform)} active={item.gameObject.activeInHierarchy}\n");
                    if (name == "PlayerTransforms")
                    {
                        AppendReferences(text, item);
                    }
                }
            }

            return text.ToString();
        }

        // Which controllers each menu pointer uses, to compare against the PlayerTransforms paths.
        private static void AppendPointers(StringBuilder text)
        {
            var type = Type.GetType("VRUIControls.VRPointer, VRUI");
            if (type == null)
            {
                text.Append("TYPE VRPointer NOT FOUND\n");
                return;
            }

            foreach (var pointer in Resources.FindObjectsOfTypeAll(type).OfType<Component>().Where(item => item.gameObject.scene.IsValid()))
            {
                text.Append($"POINTER {PathOf(pointer.transform)} active={pointer.gameObject.activeInHierarchy}\n");
                AppendReferences(text, pointer);
            }
        }

        private static void AppendReferences(StringBuilder text, Component item)
        {
            var fields = item.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => typeof(Component).IsAssignableFrom(field.FieldType));

            foreach (var field in fields)
            {
                var value = field.GetValue(item) as Component;
                text.Append($"      {field.Name} -> {(value != null ? PathOf(value.transform) : "null")}\n");
            }
        }

        internal static string PathOf(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return transform.gameObject.scene.name + ":" + path;
        }

        private static Type? FindGameType(string name) => Type.GetType(name + ", Main");

        private static Vector3 ToVector(Vec3 value) => new Vector3(value.X, value.Y, value.Z);
    }
}
