# Graphite Lyrics

Synced, animated lyrics for a Lian Li 9.2-inch case screen. It follows whatever Windows is playing,
from Spotify to a browser tab, and shows the song's lyrics over a moving cover background. When
nothing is playing, the screen falls back to an idle video or image of your choice.

## What it does

- Reads the current track and position from the Windows media session, so any player that shows up
  in the Windows media controls works.
- Fetches time-synced lyrics from [LRCLIB](https://lrclib.net) and caches them on disk.
- Scrolls smoothly to the active line, sweeps a glow across each word, and dims and blurs the
  surrounding lines.
- Shows a breathing bubble during intros and long instrumental breaks, and an empty state when a
  song has no lyrics or is instrumental.
- Uses the song's cover, blurred and slowly moving, as the background, with the cover, title and an
  optional stats panel beside the lyrics.
- Can add or substitute romanized lyrics for Japanese and Korean. Kanji readings come from the
  Windows Japanese language support, so add Japanese in Windows Settings for full readings.
- Switches to the idle background as soon as the player closes, and after a set time when paused.

## Requirements

- Windows 10 or 11 with .NET Framework 4.8.
- The case screen running as a Windows display. In L-Connect 3, turn on **Secondary screen** for the
  9.2-inch screen. The app looks for a 1920 × 464 display.
- To build: Visual Studio Build Tools with the MSBuild component and a Windows 10 or 11 SDK. Nothing
  else is downloaded.

## Install

Download `GraphiteLyrics-Setup-<version>.exe` from
[Releases](https://github.com/Project-Graphite/graphite-lyrics/releases) and run it. It installs to
`%LOCALAPPDATA%\Programs\Graphite Lyrics` for the current user, without administrator rights, and adds
**Graphite Lyrics** to the Start menu. Running a newer setup updates the installed copy. Uninstall it
from **Settings › Apps › Installed apps** like any other app.

Each release is built from its tag by the Release workflow and carries a build provenance
attestation, which `gh attestation verify <file> --repo Project-Graphite/graphite-lyrics` checks.

## Using it

- **Graphite Lyrics** in the Start menu opens the settings and starts the background agent if it is
  not running.
- The agent can show a tray icon. Double-click it to open the settings, or use **Quit** to stop it.
  With the icon turned off, use the Start menu entry and **Stop lyrics screen** instead.
- In the settings you can choose the idle background and its crop, adjust the lyric timing,
  choose romanization, toggle the stats panel and the tray icon, keep the mouse off the case screen,
  choose how long to wait after pausing and start the agent with Windows. Nothing changes until you
  press **Save**.

Settings, the lyrics cache and the log live in `%LOCALAPPDATA%\GraphiteLyrics`.

## Development

```powershell
./build.ps1
./bin/GraphiteLyrics.exe --agent --preview
```

`./install.ps1` builds and installs the working copy the same way the setup does, and
`./install.ps1 -Uninstall` removes it. To release, bump the version in `src/Version.cs`, then push a
matching tag such as `v0.2.0`.

`--preview` shows the screen in a window on the main display. For a scripted check that does not
touch any display, render one frame of a demo song to a PNG:

```powershell
./bin/GraphiteLyrics.exe --snapshot frame.png 6 --demo "Title" "Artist" 240 cover.jpg 45
```

The arguments after `--demo` are the title, artist, duration in seconds, a cover image and the
starting position in seconds.

## Layout

| Path | What it is |
| :--- | :--- |
| `src/NowPlaying.cs` | Windows media session watcher with a smoothed playback clock |
| `src/Lyrics.cs` | LRCLIB client, LRC parser and word timing |
| `src/Romanizer.cs` | Japanese and Korean romanization |
| `src/LyricsWindow.cs` | The case-screen renderer |
| `src/SettingsWindow.cs` | The settings app |
| `src/SetupWindow.cs`, `src/Installer.cs` | The setup: install, update and uninstall |
| `src/GraphiteXaml.cs` | Shared Project Graphite control styles |
| `src/Program.cs` | App, agent and setup entry points |

## License

[MIT](LICENSE). Lyrics come from LRCLIB and belong to their rights holders.
