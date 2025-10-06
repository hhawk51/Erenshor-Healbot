# Healbot Debug Build Script

## Summary of Fixes Applied

### 1. EventSystem Management
- **Issue**: Multiple EventSystems being created causing "There can be only one active Event System" warnings
- **Fix**: Implemented proper singleton management in `HealbotPlugin.EnsureEventSystem()`
  - Detects and deactivates any other EventSystems
  - Ensures only one active EventSystem exists
  - Creates EventSystem as root GameObject before `DontDestroyOnLoad()`

### 2. DontDestroyOnLoad Parenting Issues
- **Issue**: "DontDestroyOnLoad only works for root GameObjects" warnings
- **Fix**: Removed unnecessary `transform.SetParent(null)` calls
  - GameObjects are root by default when created
  - Explicit parenting to null was causing issues
  - Fixed order of operations: create GameObject, ensure root, then `DontDestroyOnLoad()`

### 3. Click Detection Problems
- **Issue**: Party member names not clickable, PartyUIhook fallback conflicting with main EventSystem
- **Fix**: Removed all fallback EventSystem creations
  - `PartyUIHook.cs`: Now only looks for existing EventSystem, logs warning if not found
  - `SpellConfigUI.cs`: Same approach, no fallback creation
  - Both components now properly reference the centralized healbot EventSystem

### 4. Ctrl+H Input Handling
- **Issue**: Menu not opening with Ctrl+H keyboard shortcut
- **Fix**:
  - Improved StandaloneInputModule configuration in main EventSystem
  - Enhanced Ctrl+H detection to work with both Left and Right Control keys
  - Added delayed initialization using coroutine to ensure EventSystem is registered before UI components
  - Fixed Canvas creation order to prevent conflicts

### 5. Initialization Order
- **Issue**: Race conditions between EventSystem creation and UI component initialization
- **Fix**:
  - Created `DelayedInitialization()` coroutine that waits one frame before initializing UI
  - Ensures EventSystem is properly registered in Unity before UI components need it
  - Fixed canvas creation to be root GameObjects before `DontDestroyOnLoad()`

## Files Modified
- `HealbotPlugin.cs` - Centralized EventSystem management, initialization order fixes
- `PartyUIHook.cs` - Removed fallback EventSystem creation
- `SpellConfigUI.cs` - Removed fallback EventSystem creation, improved input handling

## Expected Results
- No more "EVENT SYSTEM CACHED SUCCESSFULLY" duplicates
- No "There can be only one active Event System" warnings
- No "DontDestroyOnLoad only works for root GameObjects" warnings
- Party member names should be clickable for healing
- Ctrl+H should open the healbot configuration menu
- Input handling should be responsive without conflicts

## Testing Steps
1. Build the plugin with these fixes
2. Start Erenshor with UnityExplorer running
3. Check log for absence of the previous warning messages
4. Test clicking on party member names to cast heals
5. Test Ctrl+H to open configuration menu
6. Verify UI responsiveness and usability