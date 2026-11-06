using System;
using System.Collections.Generic;
using System.IO;
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
        private NoticeOverlay _notices = null!;
        private ScoreboardOverlay _scoreboard = null!;
        private SettingsFile _settingsFile = null!;
        private QualityOverride _quality = null!;
        private Diagnostics? _diagnostics;
        private bool _scenesChanged = true;
        private bool _gameplayLoaded;
        private SessionPhase _lastPhase;
        private bool _lastEnabled = true;
        private float _waitingSince;
        private bool _warnedSlowCamera;
        private bool _lastQualityApplied;
        private bool _lastDrawSkipped;

        private void Awake()
        {
            _camera = new UnityFreeCamera(_settings);
            _pointer = new UnityMenuPointer(_camera);
            _session = new FreeCamSession(_camera, _pointer, _settings);
            _notices = GetComponent<NoticeOverlay>();
            _scoreboard = GetComponent<ScoreboardOverlay>();
            _settingsFile = new SettingsFile(Path.Combine(Path.GetDirectoryName(Application.dataPath), "UserData", SettingsFile.FileName));
            _quality = new QualityOverride(new UnityQualityKnobs(gameObject.AddComponent<FrameLimiter>()), _settings);
            LoadSettings();

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
                _notices.Show(_session.TakeNotice());
                _camera.FollowGameCamera();
                _quality.Update(_session.FreeCamShowing);
                _camera.UpdateGameDraw(_session.FreeCamShowing);
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

            var wasGameplay = _gameplayLoaded;
            _gameplayLoaded = GameplayScenes.IsGameplay(names);
            _diagnostics?.OnScenes(names);

            if (_gameplayLoaded && !wasGameplay)
            {
                LoadSettings();
            }

            _scoreboard.enabled = _settings.ShowScoreboard && GameplayScenes.IsMultiplayer(names);
        }

        // Read at launch so a mistake shows up before a match, and again as each song loads so a
        // change never needs a restart. The camera is built after this, so it gets the new values.
        private void LoadSettings()
        {
            if (!_settingsFile.Refresh(_settings))
            {
                return;
            }

            ModLog.Info("Settings " + SettingsText.Describe(_settings));
            foreach (var problem in _settingsFile.Problems)
            {
                ModLog.Info("Settings problem, " + problem);
            }

            if (_settingsFile.Problems.Count > 0)
            {
                _notices.Show(OperatorNotice.SettingsProblem);
            }
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
                _notices.Show(OperatorNotice.NoGameCamera);
                ModLog.Info("Still no game camera to clone. Add " + Diagnostics.Argument + " to your launch arguments to log every camera in the scene");
            }

            if (_session.FreeCamEnabled != _lastEnabled)
            {
                _lastEnabled = _session.FreeCamEnabled;
                ModLog.Info("Free camera " + (_lastEnabled ? "on" : "off"));
            }

            if (_quality.Applied != _lastQualityApplied)
            {
                _lastQualityApplied = _quality.Applied;
                ModLog.Info(_lastQualityApplied ? "Quality " + _quality.Describe() : "Quality back to the game's own");
            }

            if (_camera.GameDrawSkipped != _lastDrawSkipped)
            {
                _lastDrawSkipped = _camera.GameDrawSkipped;
                ModLog.Info(_lastDrawSkipped ? "Game camera draw skipped" : "Game camera draw back");
            }
        }

        // Any failure hands the normal view straight back. A broken camera must not cost a match.
        private void FailSafe(Exception exception)
        {
            ModLog.Error("Free camera stopped, returning to the normal view", exception);
            enabled = false;
            _notices.Show(OperatorNotice.FreeCamStopped);

            try
            {
                _session.Shutdown();
                _quality.Restore();
            }
            catch (Exception shutdownException)
            {
                ModLog.Error("Cleanup also failed", shutdownException);
            }
        }
    }
}
