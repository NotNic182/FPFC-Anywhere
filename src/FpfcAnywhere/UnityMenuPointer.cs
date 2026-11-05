using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FpfcAnywhere.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FpfcAnywhere
{
    /// <summary>
    /// Lets the mouse drive the game's own menu pointer, the way stock FPFC does in the main menu:
    /// the two controllers are switched off, set to mouse mode, and posed on the camera. The game's
    /// input module then does the hovering and clicking.
    ///
    /// This is the one place the mod writes to game objects, so it only ever touches controllers
    /// that PointerRules proves are separate from the head and hands sent to other players. If
    /// that cannot be proven it refuses, and every write is undone on release. Game types are
    /// reached by name so the mod still does not link to game code.
    /// </summary>
    internal sealed class UnityMenuPointer : IMenuPointer
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly Type? PointerType = Type.GetType("VRUIControls.VRPointer, VRUI");
        private static readonly Type? ControllerType = Type.GetType("VRController, HMLib");
        private static readonly Type? PlayerTransformsType = Type.GetType("PlayerTransforms, Main");
        private static readonly string[] SentFields = { "_headTransform", "_leftHandTransform", "_rightHandTransform" };

        private readonly UnityFreeCamera _camera;
        private readonly List<Behaviour> _controllers = new List<Behaviour>();
        private Behaviour? _pointer;

        public UnityMenuPointer(UnityFreeCamera camera)
        {
            _camera = camera;
        }

        public bool IsAttached => _pointer != null && _pointer.isActiveAndEnabled && _controllers.All(controller => controller != null);

        public bool TryAttach(out Vec3 cameraPosition, out float cameraLocalHeight, out float yaw)
        {
            cameraPosition = Vec3.Zero;
            cameraLocalHeight = 0f;
            yaw = 0f;

            var game = _camera.GameCamera;
            var mouseMode = ControllerType?.GetProperty("mouseMode");
            if (game == null || PointerType == null || mouseMode == null)
            {
                ModLog.Info("Menu pointer unavailable: the game camera or pointer types were not found");
                return false;
            }

            var sent = SentPaths();
            foreach (var pointer in Object.FindObjectsByType(PointerType, FindObjectsSortMode.None).OfType<Behaviour>())
            {
                var controllers = ControllersOf(pointer);
                if (controllers.Count == 0)
                {
                    continue;
                }

                if (!controllers.All(controller => PointerRules.IsSafe(Diagnostics.PathOf(controller.transform), sent)))
                {
                    ModLog.Info("Menu pointer refused for " + string.Join(", ", controllers.Select(controller => Diagnostics.PathOf(controller.transform))));
                    continue;
                }

                Attach(pointer, controllers, mouseMode);

                var position = game.transform.position;
                cameraPosition = new Vec3(position.x, position.y, position.z);
                cameraLocalHeight = game.transform.localPosition.y;
                yaw = game.transform.eulerAngles.y;
                return true;
            }

            ModLog.Info("Menu pointer: no safe menu is open");
            return false;
        }

        public bool TryGetPlayerSpot(out Vec3 position, out float yaw)
        {
            position = Vec3.Zero;
            yaw = 0f;

            var game = _camera.GameCamera;
            if (game == null)
            {
                return false;
            }

            var spot = game.transform.position;
            position = new Vec3(spot.x, spot.y, spot.z);
            yaw = game.transform.eulerAngles.y;
            return true;
        }

        public void Aim(FlightPose pose)
        {
            var position = new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z);
            var rotation = Quaternion.Euler(pose.Pitch, pose.Yaw, 0f);

            foreach (var controller in _controllers)
            {
                if (controller != null)
                {
                    controller.transform.SetPositionAndRotation(position, rotation);
                }
            }
        }

        public void Release()
        {
            var mouseMode = ControllerType?.GetProperty("mouseMode");

            foreach (var controller in _controllers)
            {
                if (controller != null)
                {
                    mouseMode?.SetValue(controller, false);
                    controller.enabled = true;
                }
            }

            if (_controllers.Count > 0)
            {
                ModLog.Info("Menu pointer off");
            }

            _controllers.Clear();
            _pointer = null;
        }

        private void Attach(Behaviour pointer, List<Behaviour> controllers, PropertyInfo mouseMode)
        {
            foreach (var controller in controllers)
            {
                controller.enabled = false;
                mouseMode.SetValue(controller, true);
                _controllers.Add(controller);
            }

            _pointer = pointer;
            ModLog.Info("Menu pointer on: " + Diagnostics.PathOf(controllers[0].transform));
        }

        private static List<Behaviour> ControllersOf(Behaviour pointer)
        {
            var found = new List<Behaviour>();
            foreach (var name in new[] { "_leftVRController", "_rightVRController" })
            {
                if (PointerType?.GetField(name, Hidden)?.GetValue(pointer) is Behaviour controller && controller != null)
                {
                    found.Add(controller);
                }
            }

            return found;
        }

        // Every player's head and hands. If any cannot be read, this comes back empty and the
        // rules refuse, because safety cannot be shown without knowing what is sent.
        private static List<string> SentPaths()
        {
            var paths = new List<string>();
            if (PlayerTransformsType == null)
            {
                return paths;
            }

            foreach (var players in Object.FindObjectsByType(PlayerTransformsType, FindObjectsSortMode.None))
            {
                foreach (var name in SentFields)
                {
                    if (PlayerTransformsType.GetField(name, Hidden)?.GetValue(players) is Transform sent && sent != null)
                    {
                        paths.Add(Diagnostics.PathOf(sent));
                    }
                    else
                    {
                        return new List<string>();
                    }
                }
            }

            return paths;
        }
    }
}
