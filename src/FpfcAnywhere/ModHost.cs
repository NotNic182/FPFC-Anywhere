using System;
using System.Collections.Generic;
using System.Linq;
using FpfcAnywhere.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FpfcAnywhere
{
    /// <summary>
    /// Drives the session once per frame, in LateUpdate after the game's own camera updates.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    internal sealed class ModHost : MonoBehaviour
    {
        private const float SlowCameraWarningSeconds = 10f;

        private readonly FlightSettings _settings = new FlightSettings();
        private UnityFreeCamera _camera = null!;
        private UnityMenuPointer _pointer = null!;
        private FreeCamSession _session = null!;
        private Diagnostics? _diagnostics;
        private bool _scenesChanged = true;
        private bool _gameplayLoaded;
        private SessionPhase _lastPhase;
        private bool _lastEnabled = true;
        private float _waitingSince;
        private bool _warnedSlowCamera;

        private void Awake()
        {
            _camera = new UnityFreeCamera(_settings);
            _pointer = new UnityMenuPointer(_camera);
            _session = new FreeCamSession(_camera, _pointer, _settings);

            if (Environment.GetCommandLineArgs().Contains(Diagnostics.Argument))
            {
                _diagnostics = new Diagnostics(_session, _camera);
                ModLog.Info("Diagnostics on");
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            _diagnostics?.Dispose();
        }

        private void Update() => _diagnostics?.OnUpdate();

        private void LateUpdate()
        {
            try
            {
                RefreshScenes();
                _session.Tick(OperatorInput.Read(_gameplayLoaded));
                _camera.FollowGameCamera();
                ReportChanges();
                _diagnostics?.OnLateUpdate();
            }
            catch (Exception exception)
            {
                FailSafe(exception);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => _scenesChanged = true;

        private void HandleSceneUnloaded(Scene scene) => _scenesChanged = true;

        private void RefreshScenes()
        {
            if (!_scenesChanged)
            {
                return;
            }

            _scenesChanged = false;
            var names = new List<string>();
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded)
                {
                    names.Add(scene.name);
                }
            }

            _gameplayLoaded = GameplayScenes.IsGameplay(names);
            _diagnostics?.OnScenes(names);
        }

        private void ReportChanges()
        {
            var phase = _session.Phase;
            if (phase != _lastPhase)
            {
                ModLog.Info($"Phase {_lastPhase} -> {phase}" + (phase == SessionPhase.Active ? $" at {_session.Pose}" : string.Empty));
                _lastPhase = phase;
                _waitingSince = Time.unscaledTime;
                _warnedSlowCamera = false;
            }

            if (phase == SessionPhase.WaitingForCamera && !_warnedSlowCamera && Time.unscaledTime - _waitingSince > SlowCameraWarningSeconds)
            {
                _warnedSlowCamera = true;
                ModLog.Info("Still no game camera to clone. Run with " + Diagnostics.Argument + " and check COMPATIBILITY.md");
            }

            if (_session.FreeCamEnabled != _lastEnabled)
            {
                _lastEnabled = _session.FreeCamEnabled;
                ModLog.Info("Free camera " + (_lastEnabled ? "on" : "off"));
            }
        }

        // Any failure hands the normal view straight back. A broken camera must not cost a match.
        private void FailSafe(Exception exception)
        {
            ModLog.Error("Free camera stopped, returning to the normal view", exception);
            enabled = false;

            try
            {
                _session.Shutdown();
            }
            catch (Exception shutdownException)
            {
                ModLog.Error("Cleanup also failed", shutdownException);
            }
        }
    }
}
