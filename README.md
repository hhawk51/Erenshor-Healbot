# Erenshor Healbot

A BepInEx plugin for automatic healing in Erenshor that provides intelligent party member monitoring and healing automation.

## Features

- **Automatic Healing**: Monitors party members' health and automatically casts healing spells when needed
- **Configurable Thresholds**: Set custom health percentage thresholds for healing triggers
- **Party UI Integration**: Seamlessly integrates with the game's party interface
- **Smart Targeting**: Prioritizes healing based on party member health status
- **Cooldown Management**: Respects spell cooldowns and mana costs

## Requirements

- **Erenshor** (Steam version)
- **BepInEx 5.x** installed and configured
- **.NET Framework** compatible with netstandard2.1

## Installation

### Step 1: Install BepInEx

1. Download BepInEx 5.x from [BepInEx releases](https://github.com/BepInEx/BepInEx/releases)
2. Extract BepInEx to your Erenshor game directory (where `Erenshor.exe` is located)
3. Run the game once to generate BepInEx folders
4. Close the game

### Step 2: Install Erenshor Healbot

1. Download the latest release from the [Releases page](https://github.com/hhawk51/Erenshor-Healbot/releases)
2. Extract `Hawtin.Erenshor.Healbot.1.0.dll` to your BepInEx plugins folder:
   ```
   [Erenshor Game Directory]/BepInEx/plugins/
   ```

### Step 3: Launch the Game

1. Start Erenshor
2. The plugin will automatically load and begin monitoring party members
3. Check the BepInEx console for any loading messages

## Usage

Once installed, the Healbot will automatically:

- Monitor all party members' health levels
- Cast healing spells when a party member's health drops below the configured threshold
- Manage spell cooldowns and mana consumption
- Integrate with the existing party UI for seamless operation

## Configuration

The plugin uses default settings that work well for most situations. Configuration options may be added in future versions.

## Compatibility

- **Game Version**: Compatible with current Erenshor versions
- **Other Mods**: Should be compatible with most BepInEx mods
- **Multiplayer**: Designed to work in party-based gameplay

## Building from Source

1. Clone this repository
2. Ensure you have the required Erenshor assemblies in the `Assemblies/` folder:
   - `Assembly-CSharp.dll`
   - `Unity.TextMeshPro.dll`
   - `UnityEngine.UI.dll`
3. Build using your preferred C# IDE or command line:
   ```bash
   dotnet build
   ```

## Troubleshooting

### Plugin Not Loading
- Verify BepInEx is properly installed
- Check that the DLL is in the correct plugins folder
- Review the BepInEx console for error messages

### Healing Not Working
- Ensure you're in a party with other players
- Check that you have healing spells available
- Verify you have sufficient mana

### Performance Issues
- The plugin is designed to be lightweight
- If experiencing issues, check for conflicts with other mods

## Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## License

This project is provided as-is for the Erenshor community. Use at your own risk.

## Disclaimer

This is a third-party modification not affiliated with or endorsed by the developers of Erenshor. Use of this mod may affect your gameplay experience and could potentially cause issues with game saves or online play.