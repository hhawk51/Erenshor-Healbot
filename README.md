# Erenshor Healbot

A BepInEx plugin that adds click‑to‑heal, a simple spell config UI, and smart protections for healing your party in Erenshor.

## Highlights

- Click names/HP bars in the party UI to heal that target (hook stays enabled; no toggle hotkey).
- Draggable “HB” button (top‑right) to open the config; supports `Ctrl+H` fallback.
- Spell picker with search, A–Z sorting, and cached results for fast open.
- Beneficial‑only protection (default): blocks damage spells from being cast via Healbot.
- Cooldown awareness: respects engine cooldowns when available, with a fallback GCD.
- Icon support for the HB button: put `hb.png` next to the DLL or under `plugins/healbot/`.
- Auto‑refreshes the spell cache when you switch characters.

## Requirements

- Erenshor (Steam)
- BepInEx 5.x

## Install (Release Zip)

1) Install BepInEx (5.x) to your Erenshor directory (same folder as `Erenshor.exe`). Run once to generate folders.

2) Download the latest release zip and extract the included `healbot` folder into your plugins directory so you have:

   - `[Erenshor]/BepInEx/plugins/healbot/Hawtin.Erenshor.Healbot.dll`
   - `[Erenshor]/BepInEx/plugins/healbot/hb.png` (optional icon; used by default if present)

   Alternatively, if you place the DLL elsewhere under `plugins/`, put `hb.png` next to the DLL or set `UI.LauncherIcon` to a custom path.

3) Launch the game. You should see Healbot load in the BepInEx console.

## Use

- Click party UI entries to cast the configured spell for that mouse button.
- Open config:
  - Click the draggable “HB” button (top‑right), or
  - Press `Ctrl+H` (fallback).
- In the config window:
  - Assign spells for Left/Right/Middle click.
  - Use “Pick” to open the searchable spell list (A–Z).
  - Press “Refresh Spells” to rebuild the list (cache is used otherwise).

## Keybinds (defaults)

- `Ctrl+H` – Open/close config window (fallback)
- `F1/F2/F3/F4` – Heal player/member 1/2/3 (see config)

Note: F1–F4 currently cast using the Left Click spell binding.

## Config (BepInEx)

- `UI.LauncherIcon` (string) – Optional path to PNG/JPG for the HB icon. If empty, loads `hb.png` next to the DLL or `plugins/healbot/hb.png`.
- `UI.EnablePartyUIHook` (bool) – Enable click‑to‑heal on party UI (default: true; hook stays on).
- `Spells.LeftClick|RightClick|MiddleClick` (string) – Spells bound to mouse buttons.
- `Spells.RestrictToBeneficial` (bool) – Only allow beneficial spells (default: true).
- `Spells.DefaultGCDSeconds` (float) – Fallback minimum time between casts (default: 1.5).
- `Keybinds.HealPlayer|HealMember1|HealMember2|HealMember3` – Manual heal keys.
- `KeybindSpells.HealPlayerSpell|…` – Spells cast by those hotkeys.

## Notes on protection & cooldowns

- Beneficial‑only mode uses conservative name heuristics (heals/buffs allowed; damage‑like names blocked). If any key spell is missing, open an issue with the exact name and we’ll refine.
- Cooldowns: Healbot first asks the game for remaining cooldown; if not available, it enforces a fallback GCD. Tune `Spells.DefaultGCDSeconds` in the config file if needed.

## Troubleshooting

- Can’t click names to heal:
  - Ensure `UI.EnablePartyUIHook=true` (default)
  - Make sure the config window/backdrop is closed
  - If the game UI changed, restart or re‑open the config to rescan
- Picker list slow or empty:
  - Use “Refresh Spells” once per session to rebuild
  - Large lists are cached to speed up later opens
- Damage spells are being blocked:
  - Set `Spells.RestrictToBeneficial=false` if you must allow damage (not recommended)

## Build from source

1) Place these assemblies in `Assemblies/` (from your game install):
   - `Assembly-CSharp.dll`
   - `Unity.TextMeshPro.dll`
   - `UnityEngine.UI.dll`

2) Build: `dotnet build -c Release`

## Disclaimer

This is a third‑party mod not affiliated with the Erenshor devs. Use at your own risk.

## Release Packaging

- Zip name: `healbot-1.0.1.zip`
- Contents:
  - `healbot-1.0.1/`
    - `Hawtin.Erenshor.Healbot.dll`
    - `hb.png` (optional icon)

Install: extract `healbot-1.0.1/` into `...[Erenshor]/BepInEx/plugins/`

Final paths example:
- `...[Erenshor]/BepInEx/plugins/healbot-1.0.1/Hawtin.Erenshor.Healbot.dll`
- `...[Erenshor]/BepInEx/plugins/healbot-1.0.1/hb.png`

Notes:
- No config is required for the icon when placed next to the DLL.
- If you prefer a custom icon location, set `UI.LauncherIcon` in the config.
