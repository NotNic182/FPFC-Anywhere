# FPFC Anywhere

Keeps the FPFC free camera working during songs, including while spectating multiplayer.

For Beat Saber 1.45.3 on PC. Only your desktop view changes, and nobody else in the match needs the mod.

## Install

1. Have a 1.45.3 copy in BSManager.
2. Close the game.
3. Download the latest release, unzip it, and run `Install.cmd`.
4. Launch that copy from BSManager with FPFC on.

Run `Uninstall.cmd` to remove it. Game updates remove the mod, so reinstall after updating.

If your copy is not at `%USERPROFILE%\BSManager\BSInstances\1.45.3`:

```
powershell -ExecutionPolicy Bypass -File FpfcAnywhere-Setup.ps1 -GameDir "C:\path\to\game"
```

## Controls

| Key | Action |
| --- | --- |
| `W` `A` `S` `D` | Fly |
| Mouse | Look |
| `Space` / `Ctrl` | Up / down |
| `Shift` | Faster |
| `G` | Toggle free camera |
| `R` | Reset camera |
| `Esc` | Open or close the menu pointer |
| Left click | Click with the pointer |

## Menu

While spectating, press `Esc` to jump to the menu. Aim with the mouse and left click. Press `Esc` again to go back to your camera.

`Esc` does nothing while you are playing a song.

## Known issues

- Frame rate drops because the scene is drawn twice.
- Audio stays at the game camera, not the free camera.
- `R` also restarts the song on the solo pause menu.

## Building

Set `BEAT_SABER_DIR` to your game folder, then run:

```
dotnet build src/FpfcAnywhere -c Release
```
