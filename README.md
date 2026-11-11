# Stagehand

Stagehand puts synced lyrics for whatever Windows is playing on a second screen, such as a case
screen. It can also turn every RGB light and screen in a Lian Li case off and back on with one
switch each.

## Requirements

- Windows 10 or 11.
- A second display for the lyrics. Ultra-wide case screens are picked automatically; any other
  display can be chosen in the settings.
- Optional, for the case switches: [L-Connect 3 x OpenRGB](https://lian-li.com/l-connect-3-x-openrgb/),
  running. RGB RAM also needs the PawnIO driver, which L-Connect ships as
  `OpenRGB\PawnIO_setup.exe` in its install folder.

## Getting started

1. Download `Stagehand-Setup-<version>.exe` from
   [Releases](https://github.com/Project-Graphite/stagehand/releases) and run it. It installs for
   the current user without administrator rights.
2. Open **Stagehand** from the Start menu. The settings window opens and the lyrics screen starts.
3. Play something. Any player that shows up in the Windows media controls works, from Spotify to a
   browser tab.
4. If the lyrics are on the wrong display, pick the right one under **Look › Lyrics screen**.
5. For the case switches, set your lighting once in L-Connect with every device selected, then use
   the **Case** tab. It only appears while L-Connect 3 is running.

Most settings apply when you press **Save**. The case switches and the per-song sync nudge apply
immediately. To use the hover controls (play, seek, volume) and click a lyric to jump to it, turn
off **General › Keep the mouse off the lyrics screen**.

Settings and cached lyrics live in `%LOCALAPPDATA%\Stagehand`. Uninstall from
**Settings › Apps › Installed apps**.

## Building

Needs Visual Studio Build Tools with the MSBuild component and a Windows 10 or 11 SDK.

```powershell
./build.ps1      # builds bin\Stagehand.exe
./install.ps1    # builds and installs this copy
```

To release, bump the version in `src/Version.cs` and push a matching tag such as `v0.3.0`.

## License

[MIT](LICENSE). Lyrics come from [LRCLIB](https://lrclib.net) and belong to their rights holders.

`assets/kanji-readings.gz` is derived from the
[JMdict](https://www.edrdg.org/wiki/index.php/JMdict-EDICT_Dictionary_Project) dictionary files,
the property of the [Electronic Dictionary Research and Development Group](https://www.edrdg.org/),
and is used and shared under the
[Creative Commons Attribution-ShareAlike 4.0](https://creativecommons.org/licenses/by-sa/4.0/)
licence, as is the derived file. `tools/build-kanji-readings.ps1` regenerates it from `JMdict_e.gz`.
