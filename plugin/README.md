# Dysphoria: Kin of Sheredyn

An Endless Legend 2 mod distributed as one Workshop item.

## Features

- English text replacements from `rules/text.json`, including when another game language is selected.
- Rule-based pronoun and voice-field changes from `rules/rules.json`.
- Leader voice replacement: 57 lines, enabled by default. Missing or invalid audio keeps the stock voice.
- PNG portrait replacement for selected UI image slots through `rules/rules.json`.
  The shipped texture rule list is empty, so portraits remain unchanged until rules and PNGs are supplied.
  SVG images and 3D models are outside its scope. Missing or invalid PNGs keep the stock image.

Unit grunt replacement is not included in this release.
Leader audio follows the game's voice settings and preserves dialogue timing logic.
Replacement clips do not retime subtitles or lip sync. Audio mixing may differ from stock.

## Required items

Install both before using this mod:

- [Hazardous BepInEx Mod Loader, Workshop item 3806104364](https://steamcommunity.com/sharedfiles/filedetails/?id=3806104364).
- BepInEx **5.4.23.5**, Windows x64 archive (`BepInEx_win_x64_5.4.23.5.zip`).

Subscription alone does not complete the initial loader installation.

## Installation

**Windows**

1. Close the game. Subscribe to this mod and Hazardous BepInEx Mod Loader.
2. Extract the BepInEx archive beside `Endless Legend 2.exe`. Run the game once, then quit.
3. Run Hazardous's `install_this.bat`, or copy its `manually_install_this/BepInEx`
   directory into the game directory.
4. Restart. Check Content Management and `BepInEx/LogOutput.log` for mod discovery and errors.

**Proton (experimental; not independently tested on Endless Legend 2)**

1. Use the same subscriptions and Windows x64 BepInEx archive.
2. Copy the BepInEx files beside the Windows game executable, then copy Hazardous's
   `manually_install_this/BepInEx` into the game directory. Its batch installer is for Windows.
3. Set the Steam launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
4. Restart and check Content Management and `BepInEx/LogOutput.log`.

For manual installation, copy this package's `BepInEx/plugins/KinOfSheredyn`
folder into the game's `BepInEx/plugins/` after installing the loaders.
To disable the mod, close the game, unsubscribe or remove the manually installed folder, then restart.

## Configuration

First launch creates `BepInEx/config/org.el2.kinofsheredyn.cfg`.
Restart after editing configuration or replacement assets.

| Key (section.setting) | Default | Purpose |
| --- | --- | --- |
| `Features.Localization` | `true` | Enable English text replacements. |
| `Features.DataFields` | `true` | Enable named pronoun and voice-field rules. |
| `Features.Textures` | `true` | Enable rule-selected PNG portraits. |
| `Audio.ReplaceLeaderVoice` | `true` | Enable leader dialogue replacement. |
| `Audio.ReplaceUnitGrunts` | `false` | Reserved; enabling it does not add grunt replacement. |
| `Audio.LogAudioReplacements` | `false` | Log successful leader voice replacements. |

Confirm this mod's activation in Content Management.
Game assembly hash mismatches disable text, field and portrait overrides; check the log after game updates.

## Achievements, multiplayer and saves

Do not expect achievements with active data mods. Achievement eligibility has not
been tested in play; no achievement bypass is included.
Use identical mod versions and loader/plugin sets on every multiplayer peer.
Multiplayer behavior and plugin checksum coverage remain unverified.
Try single-player first, then test a matching lobby and save before relying on multiplayer compatibility.

Read the packaged `DISCLAIMER.txt` and the save compatibility notice at the end of
the packaged README before using existing saves. The same notice is logged at startup.

## Licenses

This plugin is **GPL-3.0-only**; see `LICENSE`.
BepInEx and HarmonyX retain their MIT notices in the package's
`licenses/BepInEx-MIT.txt` and `licenses/HarmonyX-MIT.txt`.
The source copies are `LICENSE.bepinex.txt` and `LICENSE.harmonyx.txt`.
Hazardous is not redistributed; its license is the author's.
Newtonsoft.Json comes from the game and is not redistributed.
No game DLLs, loader DLLs or Hazardous code are included.

## Building from source

See the [repository-root README](../README.md) for the build workflow.
Set `GameManagedDir` to the game's Managed directory and `BepInExCoreDir` to the
installed loader's core directory when building against your local installation:

```sh
plugin/dotnet.sh build plugin/KinOfSheredyn.sln -p:GameManagedDir=/path/to/Managed -p:BepInExCoreDir=/path/to/BepInEx/core
plugin/dotnet.sh test plugin/KinOfSheredyn.sln --filter 'Category!=GameData' -p:GameManagedDir=/path/to/Managed -p:BepInExCoreDir=/path/to/BepInEx/core
```

`plugin/scripts/package.sh` stages the item under `plugin/dist/KinOfSheredyn/`.
It requires all 57 leader WAVs under `plugin/assets/audio/leader/`, named by the
leader event table in `plugin/src/AudioCore.cs`, as 48 kHz mono PCM16.
It validates the recordings and generates `audio/events.json`.
