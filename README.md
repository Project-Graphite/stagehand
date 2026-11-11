# Stagehand

Stagehand runs a Lian Li case from backstage. It puts synced, animated lyrics on the 9.2-inch case
screen for whatever Windows is playing, shows a now-watching card for videos, and gives you two
switches: one for every RGB light in the case and one for every screen.

## What it does

### The 9.2-inch screen

- Reads the current track and position from the Windows media session, so any player that shows up
  in the Windows media controls works.
- Fetches time-synced lyrics from [LRCLIB](https://lrclib.net), checks that the title, artist and
  length match, and caches them on disk.
- Scrolls smoothly to the active line, sweeps a glow across each word, and dims and blurs the
  surrounding lines. Each word and line follows its own script's direction, so Arabic and Hebrew
  read right to left alongside English, Spanish, Japanese and Korean.
- Shows a breathing bubble during intros and long instrumental breaks, and an empty state when a
  song has no lyrics or is instrumental.
- Uses the song's cover, blurred and slowly moving, as the background, with the cover, title and an
  optional glass stats panel beside the lyrics.
- Shows a now-watching card with the thumbnail, title, channel and progress for movies, shows and
  videos. A music video with lyrics on LRCLIB still gets lyrics.
- Can add or substitute romanized lyrics for Japanese and Korean. Kanji readings come from a
  bundled table built from JMdict, so no extra Windows language is needed. If Japanese is installed
  in Windows, its phonetic analyzer is used instead.
- Switches to the idle background as soon as the player closes, and after a set time when paused.
- Moves any window that lands on the case screen back to the main display.

### Case switches

- **RGB lighting** turns off everything L-Connect 3 lights: the Lian Li fans and pump, plus the
  motherboard, GPU and RAM through L-Connect's OpenRGB integration. Switching it back on restores the
  exact effect you had.
- **Screens** turns off the cooler's screen, the LCD fan screens and the 9.2-inch screen. The 9.2-inch
  screen leaves Windows display mode while it is off and comes back to the same place afterwards.
- Both switch immediately and stay as you leave them after a restart.

## Requirements

- Windows 10 or 11 with .NET Framework 4.8.
- For the lyrics screen: the case screen running as a Windows display. In L-Connect 3, turn on
  **Secondary screen** for the 9.2-inch screen. Stagehand looks for a 1920 × 464 display.
- For the case switches: the [L-Connect 3 x OpenRGB](https://lian-li.com/l-connect-3-x-openrgb/)
  build of L-Connect 3, running. RGB RAM also needs the PawnIO driver, which L-Connect ships as
  `OpenRGB\PawnIO_setup.exe` in its install folder. Set your lighting once in L-Connect with every
  device selected, because the RGB switch covers the devices in L-Connect's saved lighting.
- To build: Visual Studio Build Tools with the MSBuild component and a Windows 10 or 11 SDK. Nothing
  else is downloaded.

## Install

Download `Stagehand-Setup-<version>.exe` from
[Releases](https://github.com/Project-Graphite/stagehand/releases) and run it. It installs to
`%LOCALAPPDATA%\Programs\Stagehand` for the current user, without administrator rights, and adds
**Stagehand** to the Start menu. Running a newer setup updates the installed copy, and installing over
Graphite Lyrics, the app's earlier name, removes it and carries its settings over. Uninstall it from
**Settings › Apps › Installed apps** like any other app.

Each release is built from its tag by the Release workflow and carries a build provenance
attestation, which `gh attestation verify <file> --repo Project-Graphite/stagehand` checks.

## Using it

- **Stagehand** in the Start menu opens the settings and starts the background agent if it is not
  running.
- The agent can show a tray icon. Double-click it to open the settings, or use **Quit** to stop it.
  With the icon turned off, use the Start menu entry and **Stop Stagehand** instead.
- The **Case** switches at the top act immediately.
- Below them you can choose the idle background and its crop, adjust the lyric timing, choose
  romanization, toggle the stats panel and the tray icon, keep the mouse off the case screen, choose
  how long to wait after pausing and start the agent with Windows. Nothing there changes until you
  press **Save**.

Settings, the switch state, the lyrics cache and the log live in `%LOCALAPPDATA%\Stagehand`.

## Development

```powershell
./build.ps1
./bin/Stagehand.exe --agent --preview
```

`./install.ps1` builds and installs the working copy the same way the setup does, and
`./install.ps1 -Uninstall` removes it. To release, bump the version in `src/Version.cs`, then push a
matching tag such as `v0.3.0`.

`--preview` shows the screen in a window on the main display. For a scripted check that does not
touch any display, render one frame of a demo song to a PNG:

```powershell
./bin/Stagehand.exe --snapshot frame.png 6 --demo "Title" "Artist" 240 cover.jpg 45
```

The arguments after `--demo` are the title, artist, duration in seconds, a cover image and the
starting position in seconds. A cover wider than 5:4 renders the now-watching card instead.

`assets/kanji-readings.gz` is generated from JMdict. To refresh it, download `JMdict_e.gz` from the
[EDRDG](https://www.edrdg.org/wiki/index.php/JMdict-EDICT_Dictionary_Project) and run:

```powershell
./tools/build-kanji-readings.ps1 -JMdict path\to\JMdict_e.gz
```

## Layout

| Path | What it is |
| :--- | :--- |
| `src/NowPlaying.cs` | Windows media session watcher with a smoothed playback clock and video detection |
| `src/Lyrics.cs` | LRCLIB client, LRC parser and word timing |
| `src/Romanizer.cs` | Japanese and Korean romanization |
| `src/LyricsWindow.cs` | The case-screen renderer |
| `src/CaseSwitches.cs` | The RGB and screen switches, and the L-Connect 3 client |
| `src/ScreenPlacement.cs` | Case-screen placement and the mouse and window guards |
| `src/SettingsWindow.cs` | The settings app |
| `src/SetupWindow.cs`, `src/Installer.cs` | The setup: install, update and uninstall |
| `src/GraphiteXaml.cs` | Shared Project Graphite control styles |
| `src/Program.cs` | App, agent and setup entry points |

## License

[MIT](LICENSE). Lyrics come from LRCLIB and belong to their rights holders.

`assets/kanji-readings.gz` is derived from the
[JMdict](https://www.edrdg.org/wiki/index.php/JMdict-EDICT_Dictionary_Project) dictionary files,
the property of the [Electronic Dictionary Research and Development Group](https://www.edrdg.org/),
and is used and shared under the
[Creative Commons Attribution-ShareAlike 4.0](https://creativecommons.org/licenses/by-sa/4.0/)
licence, as is the derived file.
