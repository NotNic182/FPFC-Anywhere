# FPFC Anywhere

Free cam for Beat Saber 1.45.3 on PC. Normally FPFC drops you in the floor and stops working when a song starts. This keeps it working the whole song, including when you're spectating in multiplayer.

It only changes your desktop view, so nobody else in the lobby needs it. Your fps will drop a bit since the scene gets drawn twice.

## Install

Needs a 1.45.3 copy in BSManager.

Grab the zip from releases, unzip it and run `Install.cmd` with the game closed. If your copy isn't in the default BSManager folder it'll ask where it is. Then launch that copy with FPFC on. `Uninstall.cmd` takes it back out.

## Controls

- WASD to fly
- Mouse to look
- Space and Ctrl for up and down
- Shift to go faster
- G toggles the free cam
- R puts it back at the start (on the solo pause menu this also restarts the song)
- Esc opens the menu while spectating

## Using the menu

Hit Esc while spectating and it takes you to the menu. Aim with the mouse, left click to press stuff, Esc again puts you back where you were. Doesn't work while you're playing a song.

## Scoreboard

In multiplayer there's a live scoreboard in the top left with everyone's name and score, plus how far behind first place they are. An F means they failed. Other players never see it, but it'll be on stream if you're capturing the game window.

## Settings

The first launch makes `UserData/FpfcAnywhere.ini` in your game folder. Open it in Notepad to change the look speed, fly speed, Shift speed, field of view, camera smoothing and the scoreboard. Changes kick in at the next song.

The camera comes set up slow and floaty for a cinematic look. If that's not your thing, turn up `look_speed` or turn down `smoothing` in `UserData/FpfcAnywhere.ini`. `smoothing = 0` turns the glide off. `scoreboard = 0` turns the scoreboard off.

## If something's wrong

A little message pops up in the bottom left if a key can't do anything right now or the free cam has a problem. It shows on stream if you're capturing the game window. There's more detail in `Logs/FpfcAnywhere.log` in your game folder.

## Building

```
dotnet build src/FpfcAnywhere -c Release
```

It looks for the game two folders up from the repo (I keep it in `<game>/ModSource/FpfcAnywhere`). Otherwise set `BEAT_SABER_DIR` to your game folder.

Not affiliated with Beat Games.
