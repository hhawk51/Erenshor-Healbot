# Erenshor Healbot

A BepInEx plugin that adds click‑to‑heal, a simple spell config UI, and helpful automation for healing your party in Erenshor.

## Highlights

- Click names/HP bars in the party UI to heal that target.
- Draggable “HB” button in the top‑right to open the config; also supports `F10` and `Ctrl+H`.
- Spell picker with search, A–Z sorting, and cached results for fast open.
- Beneficial‑only protection (default): blocks damage spells from being cast via Healbot.
- Cooldown awareness: respects engine cooldowns when available and applies a fallback GCD.
 

## Requirements

- Erenshor (Steam)
- BepInEx 5.x
- .NET SDK to build from source (netstandard2.1)

## Install

1) Install BepInEx (5.x) to your Erenshor directory (same folder as `Erenshor.exe`). Run once to generate folders.

2) Download the latest DLL from Releases and copy to:

   `[Erenshor]/BepInEx/plugins/Hawtin.Erenshor.Healbot.dll`

3) Launch the game. You should see Healbot messages in the BepInEx console.

## Use

- Click party UI entries to cast the configured spell for that mouse button.
- Open config:
  - Click the draggable “HB” button (top‑right), or
  - Press `F10` (configurable), or `Ctrl+H`.
- In the config window:
  - Assign spells for Left/Right/Middle click.
  - Use “Pick” to open the searchable spell list (A–Z).
  - Press “Refresh Spells” to rebuild the list (cache is used otherwise).

## Keybinds (defaults)

- `F10` – Open/close config window
- `Ctrl+H` – Open/close config window
- `H` – Toggle party click‑to‑heal hook
- `F1/F2/F3/F4` – Heal player/member 1/2/3 (see config)

Note: F1–F4 currently cast using the Left Click spell binding.

## Config (BepInEx)

- `Controls.OpenConfig` (KeyCode) – Open config window (default: F10)
- `Controls.ToggleUI` (KeyCode) – Toggle party UI hook (default: H)
- `UI.EnablePartyUIHook` (bool) – Enable click‑to‑heal on party UI (default: true)
- `Spells.LeftClick|RightClick|MiddleClick` (string) – Spells bound to mouse buttons
- `Spells.RestrictToBeneficial` (bool) – Only allow beneficial spells (default: true)
- `Spells.DefaultGCDSeconds` (float) – Fallback minimum time between casts (default: 1.5)
- `Keybinds.HealPlayer|HealMember1|HealMember2|HealMember3` – Manual heal keys
- `KeybindSpells.HealPlayerSpell|…` – Spells cast by those hotkeys
 

## Notes on protection & cooldowns

- Beneficial‑only mode uses conservative name heuristics (heals/buffs allowed; damage‑like names blocked). If any key spell is missing, tell us the exact name and we’ll refine the allowlist.
- Cooldowns: Healbot first asks the game for remaining cooldown; if not available, it enforces a fallback GCD. You can tune `Spells.DefaultGCDSeconds` in the config file.

## Build from source

1) Place these assemblies in `Assemblies/` (from your game install):
   - `Assembly-CSharp.dll`
   - `Unity.TextMeshPro.dll`
   - `UnityEngine.UI.dll`

2) Build: `dotnet build -c Release`

## Troubleshooting

- Can’t click names to heal:
  - Ensure `UI.EnablePartyUIHook=true`
  - Make sure the config window/backdrop is closed
  - Toggle the hook with `H` if the UI changed
- Picker list slow or empty:
  - Use “Refresh Spells” once per session to rebuild
  - Very large lists are cached to speed up subsequent opens
- Damage spells are being blocked:
  - Set `Spells.RestrictToBeneficial=false` if you need to allow damage (not recommended)

## Disclaimer

This is a third‑party mod not affiliated with the Erenshor devs. Use at your own risk.
