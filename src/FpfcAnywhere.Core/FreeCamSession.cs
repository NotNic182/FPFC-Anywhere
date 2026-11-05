namespace FpfcAnywhere.Core
{
    /// <summary>The local desktop camera, as the session sees it. Implemented on top of Unity.</summary>
    public interface IFreeCamera
    {
        bool IsAlive { get; }

        /// <summary>Builds the camera. Returns false until the game's own camera exists.</summary>
        bool TryCreate(out Vec3 sourcePosition, out float sourceYaw);

        /// <summary>
        /// Rebuilds from a better game camera if one has appeared. Returns true after a rebuild,
        /// which leaves a new camera that is visible and has no pose yet.
        /// </summary>
        bool RefreshSource();

        void Apply(FlightPose pose);

        void SetVisible(bool visible);

        void Destroy();
    }

    /// <summary>The game's own menu pointer, driven by the mouse while the operator wants it.</summary>
    public interface IMenuPointer
    {
        bool IsAttached { get; }

        /// <summary>
        /// Takes over the pointer of an open menu and reports where the player stands, so the
        /// camera can go to the menu. Returns false when there is no menu or it is not safe.
        /// </summary>
        bool TryAttach(out Vec3 cameraPosition, out float cameraLocalHeight, out float yaw);

        /// <summary>Reports where the player stands now, which moves when the spectator spot changes.</summary>
        bool TryGetPlayerSpot(out Vec3 position, out float yaw);

        void Aim(FlightPose pose);

        void Release();
    }

    public enum SessionPhase
    {
        Idle,
        WaitingForCamera,
        Active,
    }

    /// <summary>Everything the session needs from one frame.</summary>
    public struct SessionFrame
    {
        public bool GameplayLoaded;
        public bool WindowFocused;
        public bool TogglePressed;
        public bool ResetPressed;
        public bool MenuPressed;
        public float DeltaSeconds;
        public FlightInput Flight;
    }

    /// <summary>Runs the free camera for the length of a song, and only for that long.</summary>
    public sealed class FreeCamSession
    {
        private readonly IFreeCamera _camera;
        private readonly IMenuPointer _pointer;
        private readonly FlightSettings _settings;
        private FlightPose _startPose;
        private FlightPose _poseBeforeMenu;
        private Vec3 _menuSpot;
        private float _menuSpotYaw;
        private bool _hasPose;
        private FlightPose _shown;
        private OperatorNotice _notice;

        public FreeCamSession(IFreeCamera camera, IMenuPointer pointer, FlightSettings settings)
        {
            _camera = camera;
            _pointer = pointer;
            _settings = settings;
        }

        public SessionPhase Phase { get; private set; } = SessionPhase.Idle;

        /// <summary>True while the mouse is driving the game's menu pointer. Off by default.</summary>
        public bool MenuOpen { get; private set; }

        /// <summary>The operator's G toggle. It deliberately survives from one song to the next.</summary>
        public bool FreeCamEnabled { get; private set; } = true;

        /// <summary>Where the controls have the camera.</summary>
        public FlightPose Pose { get; private set; }

        /// <summary>Where the camera really is. It trails <see cref="Pose"/> while smoothing is on.</summary>
        public FlightPose ShownPose => _shown;

        /// <summary>Hands over what the operator should be told, once.</summary>
        public OperatorNotice TakeNotice()
        {
            var notice = _notice;
            _notice = OperatorNotice.None;
            return notice;
        }

        public void Tick(in SessionFrame frame)
        {
            if (!frame.GameplayLoaded)
            {
                Release();
                return;
            }

            if (Phase != SessionPhase.Active || !_camera.IsAlive)
            {
                Phase = SessionPhase.WaitingForCamera;
                TryBegin();
                return;
            }

            Fly(frame);
        }

        /// <summary>Fail-safe exit: hands the view and the cursor back and removes the camera.</summary>
        public void Shutdown()
        {
            if (Phase == SessionPhase.Active)
            {
                _camera.SetVisible(false);
            }

            Release();
        }

        private void TryBegin()
        {
            if (!_camera.TryCreate(out var sourcePosition, out var sourceYaw))
            {
                return;
            }

            // A rebuild mid-song keeps the operator's shot; only a new song goes back to the start.
            if (!_hasPose)
            {
                _startPose = FlightModel.StartFrom(sourcePosition, sourceYaw, _settings);
                Pose = _startPose;
                _shown = Pose;
                _hasPose = true;
            }

            Phase = SessionPhase.Active;
            _camera.SetVisible(FreeCamEnabled);
            _camera.Apply(_shown);
        }

        private void Fly(in SessionFrame frame)
        {
            if (_camera.RefreshSource())
            {
                _camera.SetVisible(FreeCamEnabled);
                _camera.Apply(_shown);
            }

            if (frame.WindowFocused && frame.TogglePressed)
            {
                FreeCamEnabled = !FreeCamEnabled;
                _camera.SetVisible(FreeCamEnabled);
                if (!FreeCamEnabled)
                {
                    CloseMenu();
                }
            }

            if (!FreeCamEnabled)
            {
                if (frame.WindowFocused && frame.MenuPressed)
                {
                    _notice = OperatorNotice.FreeCamIsOff;
                }

                return;
            }

            if (MenuOpen && !_pointer.IsAttached)
            {
                CloseMenu();
            }

            if (frame.WindowFocused && frame.MenuPressed)
            {
                ToggleMenu();
            }

            if (MenuOpen)
            {
                FollowMenuSpot();
            }

            if (frame.WindowFocused && frame.ResetPressed)
            {
                Pose = _startPose;
                _shown = Pose;
            }
            else
            {
                var input = frame.WindowFocused ? frame.Flight : default;
                Pose = FlightModel.Step(Pose, input, frame.DeltaSeconds, _settings);
            }

            // The pointer is the middle of the view, so with the menu open the camera has to be
            // exactly where the pointer is or the buttons would miss.
            _shown = MenuOpen ? Pose : FlightModel.Smooth(_shown, Pose, frame.DeltaSeconds, _settings.SmoothingSeconds);
            _camera.Apply(_shown);

            if (MenuOpen)
            {
                _pointer.Aim(Pose);
            }
        }

        private void ToggleMenu()
        {
            if (MenuOpen)
            {
                CloseMenu();
                return;
            }

            if (!_pointer.TryAttach(out var cameraPosition, out var cameraLocalHeight, out var yaw))
            {
                _notice = OperatorNotice.NoMenuHere;
                return;
            }

            _poseBeforeMenu = Pose;
            _menuSpot = cameraPosition;
            _menuSpotYaw = yaw;
            Pose = FlightModel.MenuView(cameraPosition, cameraLocalHeight, yaw, _settings);
            MenuOpen = true;
        }

        // The spot picker moves the player, and the menu with it, so the camera has to go along.
        private void FollowMenuSpot()
        {
            if (!_pointer.TryGetPlayerSpot(out var spot, out var yaw)
                || (spot - _menuSpot).Length < _settings.SpotJumpMeters)
            {
                return;
            }

            Pose = FlightModel.FollowSpot(Pose, _menuSpot, _menuSpotYaw, spot, yaw);
            _menuSpot = spot;
            _menuSpotYaw = yaw;
        }

        private void CloseMenu()
        {
            if (MenuOpen)
            {
                _pointer.Release();
                Pose = _poseBeforeMenu;
                _shown = Pose;
                MenuOpen = false;
            }
        }

        private void Release()
        {
            CloseMenu();

            if (Phase == SessionPhase.Active)
            {
                _camera.Destroy();
            }

            Phase = SessionPhase.Idle;
            _hasPose = false;
        }
    }
}
