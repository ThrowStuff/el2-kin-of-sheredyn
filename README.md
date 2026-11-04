# Dysphoria: a Kin of Sheredyn mod for ENDLESS Legend 2

Source code for the BepInEx plugin behind the **Dysphoria** Steam Workshop mod: a mostly
cosmetic rework of the Kin of Sheredyn, led by General Bogdan Shaparenko. The plugin
replaces English text, the leader's voice lines and selected portraits at runtime.

This is an unofficial fan mod, not affiliated with or endorsed by Amplitude Studios or SEGA.

## What is in this repository

- `plugin/src`: the plugin (BepInEx 5 / HarmonyX, Unity Mono `netstandard2.1`).
- `plugin/tests`: unit tests.
- `plugin/rules`: text replacements (`text.json`) and data/texture rules.
- `plugin/scripts`: SDK bootstrap and Workshop packaging.
- Licenses: `LICENSE` (GPL-3.0-only) and the MIT notices for BepInEx and HarmonyX.

Not included: game files or anything extracted from them, the voice recordings and artwork
(they ship in the Workshop item), and the working notes used to write the text.

## Requirements to play

Subscribe on Steam Workshop. Required: the Hazardous BepInEx Mod Loader
([3806104364](https://steamcommunity.com/sharedfiles/filedetails/?id=3806104364)) and BepInEx
5.4.23.5. Start a new game after installing; see `dist/DISCLAIMER.txt`.

## Building

You need your own copy of the game: the build references its `Managed` assemblies.

```sh
plugin/scripts/bootstrap.sh            # .NET SDK and BepInEx API into plugin/.tools/
plugin/dotnet.sh build plugin/KinOfSheredyn.sln -p:GameManagedDir="/path/to/ENDLESS Legend 2/ENDLESS Legend 2_Data/Managed"
plugin/dotnet.sh test plugin/KinOfSheredyn.sln -p:GameManagedDir=... --filter 'Category!=GameData'
```

Tests tagged `GameData` check replacements against the game's stock text and need a local
extract that cannot be redistributed. `plugin/scripts/package.sh` stages the Workshop folder
and needs the 57 leader WAVs in `plugin/assets/audio/leader/`. Further technical notes are
in `plugin/README.md`.

## License

GPL-3.0-only (`LICENSE`). BepInEx and HarmonyX are MIT and are provided by the loader, not
bundled here. The recordings and artwork in the Workshop item are covered by their own notices.

Made with AI assistance and some AI-generated assets.
