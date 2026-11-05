using FpfcAnywhere.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FpfcAnywhere
{
    /// <summary>
    /// Reads the keyboard and mouse the same way the game's stock FPFC does: keys through the
    /// legacy Input class and mouse movement through the Input System. Nothing here writes input.
    /// </summary>
    internal static class OperatorInput
    {
        public static SessionFrame Read(bool gameplayLoaded)
        {
            var mouse = Mouse.current?.delta.ReadValue() ?? Vector2.zero;

            return new SessionFrame
            {
                GameplayLoaded = gameplayLoaded,
                WindowFocused = Application.isFocused,
                TogglePressed = Input.GetKeyDown(KeyCode.G),
                ResetPressed = Input.GetKeyDown(KeyCode.R),
                MenuPressed = Input.GetKeyDown(KeyCode.Escape),

                // Unscaled, so the camera keeps flying while a solo song is paused.
                DeltaSeconds = Time.unscaledDeltaTime,
                Flight = new FlightInput
                {
                    Forward = Input.GetKey(KeyCode.W),
                    Back = Input.GetKey(KeyCode.S),
                    Left = Input.GetKey(KeyCode.A),
                    Right = Input.GetKey(KeyCode.D),
                    Up = Input.GetKey(KeyCode.Space),
                    Down = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
                    Fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                    MouseX = mouse.x,
                    MouseY = mouse.y,
                },
            };
        }
    }
}
