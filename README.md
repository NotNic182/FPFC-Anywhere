# FPFC Anywhere

Free cam for Beat Saber 1.45.3 on PC. Normally FPFC drops you in the floor and stops working when a song starts. This keeps it working the whole song, including when you're spectating in multiplayer.

It only changes your desktop view, so nobody else in the lobby needs it.

## Install

Needs a 1.45.3 copy in BSManager.

Grab the zip from releases, unzip it and run `Install.cmd` with the game closed. Then launch that copy with FPFC on. `Uninstall.cmd` takes it back out.

## Controls

- WASD to fly
- Mouse to look
- Space and Ctrl for up and down
- Shift to go faster
- G toggles the free cam
- R resets it
- Esc opens the menu pointer

## Using the menu

Hit Esc while spectating and it takes you to the menu. Left click to press stuff, Esc again puts you back where you were. Doesn't work while you're playing a song.

Your fps will drop a bit since the scene gets drawn twice.

## Building

```
dotnet build src/FpfcAnywhere -c Release
```

It looks for the game two folders up from the repo (I keep it in `<game>/ModSource/FpfcAnywhere`). Otherwise set `BEAT_SABER_DIR` to your game folder.

Not affiliated with Beat Games.
