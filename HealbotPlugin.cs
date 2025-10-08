using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.IO;

namespace ErenshorHealbot
{
    [BepInPlugin("Hawtin.Erenshor.Healbot", "ErenshorHealbot", "1.0.0")]
    public class HealbotPlugin : BaseUnityPlugin
    {
        public static HealbotPlugin Instance { get; private set; }

        private Harmony _harmony;
        private PartyUIHook partyUIHook;
        private SpellConfigUI spellConfigUI;
        private EventSystem eventSystem;
        private AutoTargetOverlay autoTargetOverlay;
        
        // Configuration
        private ConfigEntry<KeyCode> toggleUIKey;
        private ConfigEntry<string> leftClickSpell;
        private ConfigEntry<string> rightClickSpell;
        private ConfigEntry<string> middleClickSpell;
        private ConfigEntry<string> shiftLeftClickSpell;
        private ConfigEntry<string> shiftRightClickSpell;
        private ConfigEntry<string> shiftMiddleClickSpell;
        private ConfigEntry<bool> autoTargetEnabled;
        private ConfigEntry<float> autoTargetGraceSeconds;
        private ConfigEntry<float> healthThreshold;
        private ConfigEntry<bool> enablePartyUIHook;
        private ConfigEntry<bool> restrictToBeneficial;
        private ConfigEntry<float> defaultGCDSeconds;
        private ConfigEntry<string> launcherIconPath;
        private ConfigEntry<float> panelPosX;
        private ConfigEntry<float> panelPosY;
        private ConfigEntry<float> launcherPosX;
        private ConfigEntry<float> launcherPosY;
        private ConfigEntry<KeyCode> healPlayerKey;
        private ConfigEntry<KeyCode> healMember1Key;
        private ConfigEntry<KeyCode> healMember2Key;
        private ConfigEntry<KeyCode> healMember3Key;
        private ConfigEntry<string> healPlayerSpell;
        private ConfigEntry<string> healMember1Spell;
        private ConfigEntry<string> healMember2Spell;
        private ConfigEntry<string> healMember3Spell;
        private ConfigEntry<bool> hideLauncherButton;
        private ConfigEntry<float> overlayPosX;
        private ConfigEntry<float> overlayPosY;

        // Group member tracking for auto-targeting
        private List<GroupMember> groupMembers = new List<GroupMember>();
        private readonly HashSet<Stats> lastGroupComposition = new HashSet<Stats>();
        private float autoTargetSuppressedUntil = 0f;
        private string lastSeenSceneName = string.Empty;
        private float CurrentTime => Time.unscaledTime > 0f ? Time.unscaledTime : Time.time;

        // Character-specific configuration system
        private CharacterConfig currentCharacterConfig;
        private string lastLoadedCharacter = null;

        private void Awake()
        {
            Instance = this;

            // Setup configuration
            toggleUIKey = Config.Bind("Controls", "ToggleUI", KeyCode.H, "Key to toggle healbot UI");
            leftClickSpell = Config.Bind("Spells", "LeftClick", "Minor Healing", "Spell to cast on left click");
            rightClickSpell = Config.Bind("Spells", "RightClick", "Major Healing", "Spell to cast on right click");
            middleClickSpell = Config.Bind("Spells", "MiddleClick", "Group Heal", "Spell to cast on middle click");
            shiftLeftClickSpell = Config.Bind("Spells", "ShiftLeftClick", "", "Spell to cast on Shift+Left click (optional)");
            shiftRightClickSpell = Config.Bind("Spells", "ShiftRightClick", "", "Spell to cast on Shift+Right click (optional)");
            shiftMiddleClickSpell = Config.Bind("Spells", "ShiftMiddleClick", "", "Spell to cast on Shift+Middle click (optional)");
            autoTargetEnabled = Config.Bind("Automation", "AutoTarget", false, "Automatically target low health members");
            autoTargetGraceSeconds = Config.Bind("Automation", "AutoTargetGraceSeconds", 1.0f, "Seconds to delay auto-targeting after scene or party changes");
            healthThreshold = Config.Bind("Automation", "HealthThreshold", 0.5f, "Health percentage to consider 'low' (0.0-1.0)");
            enablePartyUIHook = Config.Bind("UI", "EnablePartyUIHook", true, "Enable click-to-heal on existing party UI");
            restrictToBeneficial = Config.Bind("Spells", "RestrictToBeneficial", true, "Only allow beneficial spells (heals/buffs) when casting via Healbot");
            defaultGCDSeconds = Config.Bind("Spells", "DefaultGCDSeconds", 1.5f, "Fallback minimum time between casts when underlying cooldown info is unavailable");
            launcherIconPath = Config.Bind("UI", "LauncherIcon", "", "Optional path to a PNG/JPG image to use for the HB launcher button (absolute or relative to BepInEx/plugins)");
            panelPosX = Config.Bind("UI", "PanelPosX", 0f, "Saved position X for the config panel (anchored) ");
            panelPosY = Config.Bind("UI", "PanelPosY", 0f, "Saved position Y for the config panel (anchored) ");
            launcherPosX = Config.Bind("UI", "LauncherPosX", -20f, "Saved anchored X for launcher (top-right anchor, negative offsets)");
            launcherPosY = Config.Bind("UI", "LauncherPosY", -20f, "Saved anchored Y for launcher (top-right anchor, negative offsets)");
            healPlayerKey = Config.Bind("Keybinds", "HealPlayer", KeyCode.F1, "Key to heal the player");
            healMember1Key = Config.Bind("Keybinds", "HealMember1", KeyCode.F2, "Key to heal party member 1");
            healMember2Key = Config.Bind("Keybinds", "HealMember2", KeyCode.F3, "Key to heal party member 2");
            healMember3Key = Config.Bind("Keybinds", "HealMember3", KeyCode.F4, "Key to heal party member 3");
            healPlayerSpell = Config.Bind("KeybindSpells", "HealPlayerSpell", "Minor Healing", "Spell to cast when healing the player");
            healMember1Spell = Config.Bind("KeybindSpells", "HealMember1Spell", "Minor Healing", "Spell to cast when healing party member 1");
            healMember2Spell = Config.Bind("KeybindSpells", "HealMember2Spell", "Minor Healing", "Spell to cast when healing party member 2");
            healMember3Spell = Config.Bind("KeybindSpells", "HealMember3Spell", "Minor Healing", "Spell to cast when healing party member 3");
            hideLauncherButton = Config.Bind("UI", "HideLauncherButton", false, "Hide the launcher button (can still use Ctrl+H to open config)");
            overlayPosX = Config.Bind("UI", "OverlayPosX", -40f, "Saved anchored X for auto-target overlay (top-right anchor)");
            overlayPosY = Config.Bind("UI", "OverlayPosY", 40f, "Saved anchored Y for auto-target overlay (top-right anchor)");

            _harmony = new Harmony("Hawtin.Erenshor.Healbot");
            try
            {
                _harmony.PatchAll();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Harmony patching failed: {ex.Message}");
            }

            // CRITICAL: Ensure EventSystem exists before any UI components
            EnsureEventSystem();

            // Add a small delay to ensure EventSystem is properly registered
            StartCoroutine(DelayedInitialization());

            // Prime scene tracking for auto-target suppression
            try
            {
                var activeScene = SceneManager.GetActiveScene();
                lastSeenSceneName = activeScene.IsValid() ? (activeScene.name ?? string.Empty) : string.Empty;
            }
            catch
            {
                lastSeenSceneName = string.Empty;
            }

            SuppressAutoTarget("Initialization");
        }

        private void InitializePartyUIHook()
        {
            if (partyUIHook == null)
            {
                var hookGO = new GameObject("PartyUIHook");
                DontDestroyOnLoad(hookGO);
                partyUIHook = hookGO.AddComponent<PartyUIHook>();
                partyUIHook.Initialize(this);
            }
        }

        private void InitializeSpellConfigUI()
        {
            if (spellConfigUI == null)
            {
                var configUIGO = new GameObject("SpellConfigUI");
                // Don't set parent to null - it starts as root by default
                DontDestroyOnLoad(configUIGO);
                spellConfigUI = configUIGO.AddComponent<SpellConfigUI>();
                spellConfigUI.Initialize(this);
            }
        }

        private void InitializeAutoTargetOverlay()

        {

            if (autoTargetOverlay == null)

            {

                var overlayGO = new GameObject("AutoTargetOverlay");

                overlayGO.transform.SetParent(null);

                DontDestroyOnLoad(overlayGO);

                autoTargetOverlay = overlayGO.AddComponent<AutoTargetOverlay>();

                autoTargetOverlay.Initialize(this);

                autoTargetOverlay.SetAnchoredPosition(GetSavedOverlayPos());

            }



            if (autoTargetOverlay == null)

                return;



            bool autoEnabled = autoTargetEnabled != null && autoTargetEnabled.Value;

            autoTargetOverlay.OnAutoTargetToggle(autoEnabled);



            if (autoEnabled && IsAutoTargetSuppressed())

            {

                autoTargetOverlay.ShowSuppression(Mathf.Max(0f, autoTargetSuppressedUntil - CurrentTime), "Auto-target paused");

            }

            else

            {

                UpdateOverlayWithLowestMember();

            }

        }



        public string GetSpellForButton(PointerEventData.InputButton button)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            string spell = null;
            switch (button)
            {
                case PointerEventData.InputButton.Left:
                    spell = shift && !string.IsNullOrWhiteSpace(shiftLeftClickSpell.Value) ? shiftLeftClickSpell.Value : leftClickSpell.Value;
                    break;
                case PointerEventData.InputButton.Right:
                    spell = shift && !string.IsNullOrWhiteSpace(shiftRightClickSpell.Value) ? shiftRightClickSpell.Value : rightClickSpell.Value;
                    break;
                case PointerEventData.InputButton.Middle:
                    spell = shift && !string.IsNullOrWhiteSpace(shiftMiddleClickSpell.Value) ? shiftMiddleClickSpell.Value : middleClickSpell.Value;
                    break;
                default:
                    spell = null;
                    break;
            }

            return spell;
        }

        public string GetAutoTargetOverlaySpell()

        {

            var spell = healPlayerSpell != null ? healPlayerSpell.Value : string.Empty;

            if (string.IsNullOrWhiteSpace(spell) || spell.Equals("None", StringComparison.OrdinalIgnoreCase))

            {

                spell = leftClickSpell != null ? leftClickSpell.Value : string.Empty;

            }

            return spell ?? string.Empty;

        }

        public void UpdateSpellBindings(string leftSpell, string rightSpell, string middleSpell)
        {
            // Check if left-click spell is changing
            bool leftSpellChanged = !string.Equals(leftClickSpell.Value, leftSpell ?? "", StringComparison.OrdinalIgnoreCase);

            // Update the configuration entries
            leftClickSpell.Value = leftSpell ?? "";
            rightClickSpell.Value = rightSpell ?? "";
            middleClickSpell.Value = middleSpell ?? "";

            // Update character config object first, before saving
            if (currentCharacterConfig != null)
            {
                currentCharacterConfig.LeftClickSpell = leftSpell ?? "";
                currentCharacterConfig.RightClickSpell = rightSpell ?? "";
                currentCharacterConfig.MiddleClickSpell = middleSpell ?? "";
            }

            // If left-click spell changed, update keybind spells to match
            if (leftSpellChanged && !string.IsNullOrEmpty(leftSpell))
            {
                healPlayerSpell.Value = leftSpell;
                healMember1Spell.Value = leftSpell;
                healMember2Spell.Value = leftSpell;
                healMember3Spell.Value = leftSpell;

                // Also update the character config object so it saves correctly
                if (currentCharacterConfig != null)
                {
                    currentCharacterConfig.HealPlayerSpell = leftSpell;
                    currentCharacterConfig.HealMember1Spell = leftSpell;
                    currentCharacterConfig.HealMember2Spell = leftSpell;
                    currentCharacterConfig.HealMember3Spell = leftSpell;
                }
            }

            // Save character config when settings change
            if (!string.IsNullOrEmpty(lastPlayerIdentity))
                SaveCharacterConfig(lastPlayerIdentity);
        }

        public bool IsAutoTargetEnabled => autoTargetEnabled != null && autoTargetEnabled.Value;

        public void SetAutoTargetEnabled(bool enabled)
        {
            if (autoTargetEnabled == null)
                return;

            bool wasEnabled = autoTargetEnabled.Value;
            autoTargetEnabled.Value = enabled;

            autoTargetOverlay?.OnAutoTargetToggle(enabled);

            if (wasEnabled == enabled)
            {
                if (spellConfigUI != null)
                {
                    spellConfigUI.SetAutoTargetToggleState(enabled);
                }
                return;
            }

            if (currentCharacterConfig != null)
            {
                currentCharacterConfig.AutoTargetEnabled = enabled;
            }

            if (enabled && !wasEnabled)
            {
                SuppressAutoTarget("Auto-target toggled on");
            }
            else if (!enabled)
            {
                autoTargetOverlay?.ClearTarget();
            }

            SaveCurrentCharacterConfig();

            if (spellConfigUI != null)
            {
                spellConfigUI.SetAutoTargetToggleState(enabled);
            }

            UpdateOverlayWithLowestMember();
        }

        public void UpdateShiftSpellBindings(string shiftLeft, string shiftRight, string shiftMiddle)
        {
            shiftLeftClickSpell.Value = shiftLeft ?? "";
            shiftRightClickSpell.Value = shiftRight ?? "";
            shiftMiddleClickSpell.Value = shiftMiddle ?? "";

            // Update character config object first, before saving
            if (currentCharacterConfig != null)
            {
                currentCharacterConfig.ShiftLeftClickSpell = shiftLeft ?? "";
                currentCharacterConfig.ShiftRightClickSpell = shiftRight ?? "";
                currentCharacterConfig.ShiftMiddleClickSpell = shiftMiddle ?? "";
            }

            // Save character config when settings change
            if (!string.IsNullOrEmpty(lastPlayerIdentity))
                SaveCharacterConfig(lastPlayerIdentity);
        }

        public string GetLauncherIconPath() => launcherIconPath?.Value ?? string.Empty;

        public string GetPluginDirectory()
        {
            try
            {
                return Path.GetDirectoryName(Info?.Location) ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        public Vector2 GetSavedPanelPos() => new Vector2(panelPosX.Value, panelPosY.Value);
        public void SavePanelPos(Vector2 pos)
        {
            panelPosX.Value = pos.x;
            panelPosY.Value = pos.y;
        }

        public Vector2 GetSavedLauncherPos() => new Vector2(launcherPosX.Value, launcherPosY.Value);
        public void SaveLauncherPos(Vector2 pos)
        {
            launcherPosX.Value = pos.x;
            launcherPosY.Value = pos.y;
        }

        public Vector2 GetSavedOverlayPos() => new Vector2(overlayPosX.Value, overlayPosY.Value);
        public void SaveOverlayPos(Vector2 pos)
        {
            overlayPosX.Value = pos.x;
            overlayPosY.Value = pos.y;
        }

        private void Update()
        {
            // UI hook stays enabled; disable toggle hotkey

            // Opening the configuration window is available via the on-screen HB button or Ctrl+H fallback.

            // Manual healing keybinds
            CheckHealingKeybinds();

            // Update group members for auto-targeting
            UpdateGroupMembers();

            // Delay auto-targeting while scenes/party members settle
            UpdateAutoTargetSuppression();

            UpdateOverlayWithLowestMember();

            if (ShouldAutoTarget())
            {
                CheckAutoTarget();
            }

            // Detect character switch and invalidate spell cache for UI
            RefreshSpellCacheOnCharacterSwitch();
        }

        private void UpdateAutoTargetSuppression()
        {
            try
            {
                var activeScene = SceneManager.GetActiveScene();
                var sceneName = activeScene.IsValid() ? (activeScene.name ?? string.Empty) : string.Empty;
                if (!string.Equals(sceneName, lastSeenSceneName, StringComparison.Ordinal))
                {
                    lastSeenSceneName = sceneName;
                    SuppressAutoTarget("Scene changed");
                }
            }
            catch { }
        }

        private bool ShouldAutoTarget()
        {
            if (!autoTargetEnabled.Value)
                return false;
            if (IsAutoTargetSuppressed())
                return false;
            if (!IsCharacterLoggedIn())
                return false;
            return true;
        }

        private bool IsAutoTargetSuppressed()
        {
            return CurrentTime < autoTargetSuppressedUntil;
        }

        private void SuppressAutoTarget(string reason, float? durationOverrideSeconds = null)
        {
            if (reason == null) { } // reason reserved for future debug logging

            string overlayMessage = "Auto-target paused";
            if (!string.IsNullOrWhiteSpace(reason))
            {
                switch (reason)
                {
                    case "Initialization":
                        overlayMessage = "Waiting for character data";
                        break;
                    case "Scene changed":
                        overlayMessage = "Syncing after scene change";
                        break;
                    case "Party composition changed":
                        overlayMessage = "Updating party roster";
                        break;
                    case "Target missing character":
                        overlayMessage = "Target not ready";
                        break;
                    case "Auto-target toggled on":
                        overlayMessage = "Auto-target starting";
                        break;
                    case "Targeting failed":
                        overlayMessage = "Targeting failed";
                        break;
                    default:
                        overlayMessage = reason;
                        break;
                }
            }

            float grace = durationOverrideSeconds ?? (autoTargetGraceSeconds != null ? Mathf.Max(0f, autoTargetGraceSeconds.Value) : 0f);
            if (grace <= 0f)
            {
                autoTargetSuppressedUntil = CurrentTime;
                autoTargetOverlay?.ShowSuppression(0f, overlayMessage);
                return;
            }

            float resumeAt = CurrentTime + grace;
            if (resumeAt > autoTargetSuppressedUntil)
            {
                autoTargetSuppressedUntil = resumeAt;
            }

            autoTargetOverlay?.ShowSuppression(Mathf.Max(0f, autoTargetSuppressedUntil - CurrentTime), overlayMessage);
        }

        private string lastPlayerIdentity = null;
        private void RefreshSpellCacheOnCharacterSwitch()
        {
            try
            {
                var current = GameData.PlayerStats != null ? (GameData.PlayerStats.MyName ?? "") : "";
                if (lastPlayerIdentity == null)
                {
                    lastPlayerIdentity = current;
                    if (!string.IsNullOrEmpty(current))
                    {
                        LoadCharacterConfig(current);
                    }
                    return;
                }
                if (!string.Equals(lastPlayerIdentity, current))
                {
                    // Save config for previous character before switching
                    if (!string.IsNullOrEmpty(lastPlayerIdentity))
                    {
                        SaveCharacterConfig(lastPlayerIdentity);
                    }

                    lastPlayerIdentity = current;

                    // Load config for new character
                    if (!string.IsNullOrEmpty(current))
                    {
                        LoadCharacterConfig(current);
                    }

                    if (spellConfigUI != null)
                    {
                        spellConfigUI.InvalidateSpellCache();
                    }

                    // Reinitialize party UI hooks for new character
                    if (partyUIHook != null && !string.IsNullOrEmpty(current))
                    {
                        partyUIHook.ForceRescanForCharacterSwitch();
                    }
                }
            }
            catch { }
        }

        private void CheckHealingKeybinds()
        {
            // Heal player
            if (Input.GetKeyDown(healPlayerKey.Value))
            {
                HealTarget(0, "Player");
            }

            // Heal party members
            if (Input.GetKeyDown(healMember1Key.Value))
            {
                HealTarget(1, "Member 1");
            }

            if (Input.GetKeyDown(healMember2Key.Value))
            {
                HealTarget(2, "Member 2");
            }

            if (Input.GetKeyDown(healMember3Key.Value))
            {
                HealTarget(3, "Member 3");
            }
        }

        private void HealTarget(int memberIndex, string memberDesc)
        {
            string spellToUse = GetSpellForMember(memberIndex);

            if (memberIndex == 0)
            {
                // Heal player
                if (GameData.PlayerStats != null)
                {
                    CastSpellOnTarget(GameData.PlayerStats, spellToUse);
                }
            }
            else
            {
                // Heal party member
                var actualIndex = memberIndex - 1; // Convert to 0-based
                if (actualIndex < groupMembers.Count)
                {
                    var member = groupMembers.Skip(1).ElementAtOrDefault(actualIndex); // Skip player
                    if (member != null && member.stats != null)
                    {
                        CastSpellOnTarget(member.stats, spellToUse);
                    }
                }
            }
        }

        private string GetSpellForMember(int memberIndex)
        {
            switch (memberIndex)
            {
                case 0: return healPlayerSpell.Value;
                case 1: return healMember1Spell.Value;
                case 2: return healMember2Spell.Value;
                case 3: return healMember3Spell.Value;
                default: return healPlayerSpell.Value; // Fallback
            }
        }

        private void TogglePartyUIHook()
        {
            if (partyUIHook == null)
            {
                InitializePartyUIHook();
            }
            else
            {
                Destroy(partyUIHook.gameObject);
                partyUIHook = null;
            }
        }

        private void UpdateGroupMembers()
        {
            var previousComposition = new HashSet<Stats>(lastGroupComposition);
            lastGroupComposition.Clear();
            groupMembers.Clear();

            // Add player
            if (GameData.PlayerStats != null)
            {
                string playerName = GameData.PlayerStats.MyName;
                if (string.IsNullOrEmpty(playerName))
                    playerName = "Player"; // Fallback name

                groupMembers.Add(new GroupMember
                {
                    stats = GameData.PlayerStats,
                    name = playerName,
                    isPlayer = true
                });

                lastGroupComposition.Add(GameData.PlayerStats);
            }

            // Add party members from GameData.GroupMembers
            var members = GameData.GroupMembers;
            if (members != null)
            {
                for (int i = 0; i < members.Length; i++)
                {
                    var tracking = members[i];
                    var stats = tracking?.MyAvatar?.MyStats;
                    if (stats == null)
                        continue;

                    string displayName = !string.IsNullOrEmpty(stats.MyName) ? stats.MyName : tracking.SimName;
                    var npc = tracking.MyAvatar.GetComponent<NPC>();
                    if (npc != null && !string.IsNullOrEmpty(npc.NPCName))
                        displayName = npc.NPCName;

                    // Fallback if name is still empty
                    if (string.IsNullOrEmpty(displayName))
                        displayName = $"Member{i + 1}";

                    groupMembers.Add(new GroupMember
                    {
                        stats = stats,
                        name = displayName,
                        isPlayer = false
                    });

                    lastGroupComposition.Add(stats);
                }
            }

            lastGroupComposition.RemoveWhere(s => s == null);

            if (!previousComposition.SetEquals(lastGroupComposition))
            {
                SuppressAutoTarget("Party composition changed");
            }
        }

        private Stats FindTargetByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string norm(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            var wanted = norm(name);

            // Player
            if (GameData.PlayerStats != null && !string.IsNullOrEmpty(GameData.PlayerStats.MyName))
                if (norm(GameData.PlayerStats.MyName) == wanted) return GameData.PlayerStats;

            // Group members by NPCName, SimName, or Stats.MyName
            for (int i = 0; i < GameData.GroupMembers.Length; i++)
            {
                var tracking = GameData.GroupMembers[i];
                if (tracking?.MyAvatar?.MyStats == null) continue;
                var s = tracking.MyAvatar.MyStats;
                var npc = tracking.MyAvatar.GetComponent<NPC>();
                var candidates = new[]
                {
                    s.MyName,
                    tracking.SimName,
                    npc != null ? npc.NPCName : null
                };
                if (candidates.Any(c => !string.IsNullOrEmpty(c) && norm(c) == wanted))
                    return s;
            }
            return null;
        }



        private GroupMember GetLowestHealthMember(out float healthFraction)

        {

            healthFraction = 0f;

            var candidates = groupMembers

                .Where(gm => gm != null && gm.stats != null && gm.stats.CurrentMaxHP > 0)

                .Select(gm => new

                {

                    Member = gm,

                    Fraction = Mathf.Clamp01((float)gm.stats.CurrentHP / (float)gm.stats.CurrentMaxHP)

                })

                .OrderBy(x => x.Fraction)

                .ToList();



            if (!candidates.Any())

                return null;



            var best = candidates.First();

            healthFraction = best.Fraction;

            return best.Member;

        }



        private string GetDisplayNameForMember(GroupMember member)

        {

            if (member == null)

                return "Unknown";



            if (!string.IsNullOrEmpty(member.name))

                return member.name;



            var statsName = member.stats?.MyName;

            if (!string.IsNullOrEmpty(statsName))

                return statsName;



            return member.isPlayer ? "Player" : "Party member";

        }



        private void UpdateOverlayWithLowestMember()

        {

            if (autoTargetOverlay == null)

                return;



            if (IsAutoTargetSuppressed())

                return;



            var lowest = GetLowestHealthMember(out var fraction);

            if (lowest?.stats == null)

            {

                autoTargetOverlay.ClearTarget();

                return;

            }



            var displayName = GetDisplayNameForMember(lowest);

            autoTargetOverlay.UpdateTarget(lowest.stats, displayName, fraction, GetAutoTargetOverlaySpell());

        }



                private void CheckAutoTarget()
        {
            if (!ShouldAutoTarget())
                return;

            var targetMember = GetLowestHealthMember(out var healthFraction);
            if (targetMember?.stats == null)
                return;

            var targetStats = targetMember.stats;
            var targetCharacter = targetStats.Myself;
            if (targetCharacter == null)
            {
                SuppressAutoTarget("Target missing character", 0.5f);
                return;
            }

            float threshold = Mathf.Clamp01(healthThreshold.Value);
            if (threshold <= 0f)
                return;

            if (healthFraction >= threshold)
                return;

            var playerControl = GameData.PlayerControl;
            if (playerControl == null)
                return;

            if (playerControl.CurrentTarget == targetCharacter)
                return;

            try
            {
                if (playerControl.CurrentTarget != null)
                {
                    playerControl.CurrentTarget.UntargetMe();
                }

                playerControl.CurrentTarget = targetCharacter;
                targetCharacter.TargetMe();
            }
            catch
            {
                SuppressAutoTarget("Targeting failed", 0.5f);
            }
        }

        public void CastSpellOnTarget(Stats target, string spellName)
        {
            if (target == null || string.IsNullOrEmpty(spellName)) return;

            // Skip casting if "None" is selected
            if (spellName.Equals("None", StringComparison.OrdinalIgnoreCase))
                return;

            var playerCaster = GameData.PlayerControl?.GetComponent<CastSpell>();
            if (playerCaster == null) return;

            // Find the spell
            var spell = FindSpellByName(spellName);
            if (spell == null)
            {
                Logger.LogWarning($"Spell '{spellName}' not found!");
                return;
            }

            // Restrict to beneficial spells if configured
            if (restrictToBeneficial.Value && !IsBeneficialSpell(spell))
            {
                Logger.LogWarning($"Blocked non-beneficial spell '{spell.SpellName}' from Healbot casting (RestrictToBeneficial=true)");
                return;
            }

            // Cooldown checks: prefer engine-provided cooldown, else plugin fallback
            if (IsSpellOnCooldown(playerCaster, spell, out var remaining))
            {
                return;
            }
            if (IsLocallyThrottled(spell))
            {
                return;
            }

            // Set target and cast spell
            if (GameData.PlayerControl != null)
            {
                if (GameData.PlayerControl.CurrentTarget != null)
                    GameData.PlayerControl.CurrentTarget.UntargetMe();

                GameData.PlayerControl.CurrentTarget = target.Myself;
                GameData.PlayerControl.CurrentTarget.TargetMe();
            }

            playerCaster.StartSpell(spell, target);

            // After attempting to cast, set local cooldown window to avoid back-to-back spam
            SetLocalCooldown(spell);
        }

        private Spell FindSpellByName(string spellName)
        {
            var playerCaster = GameData.PlayerControl?.GetComponent<CastSpell>();
            if (playerCaster?.KnownSpells != null)
            {
                // 1) Exact (ignore case)
                var exact = playerCaster.KnownSpells.FirstOrDefault(s =>
                    s.SpellName.Equals(spellName, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    if (!restrictToBeneficial.Value || IsBeneficialSpell(exact)) return exact;
                }

                // 2) Normalized contains (handle Minor Heal vs Minor Healing, hyphens, spaces)
                string Norm(string x) => new string(x.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
                var wanted = Norm(spellName);
                var partialSeq = playerCaster.KnownSpells
                    .Where(s => Norm(s.SpellName).Contains(wanted) || wanted.Contains(Norm(s.SpellName)));
                var partial = restrictToBeneficial.Value ? partialSeq.FirstOrDefault(IsBeneficialSpell) : partialSeq.FirstOrDefault();
                if (partial != null) return partial;

                // 3) Heuristic heal picks if the word 'heal' is present
                if (wanted.Contains("heal"))
                {
                    // Prefer minor/major variants
                    if (wanted.Contains("minor"))
                    {
                        var seq = playerCaster.KnownSpells.Where(s => s.SpellName.IndexOf("minor", StringComparison.OrdinalIgnoreCase) >= 0 && s.SpellName.IndexOf("heal", StringComparison.OrdinalIgnoreCase) >= 0)
                            .Concat(playerCaster.KnownSpells.Where(s => s.SpellName.IndexOf("healing", StringComparison.OrdinalIgnoreCase) >= 0));
                        return restrictToBeneficial.Value ? seq.FirstOrDefault(IsBeneficialSpell) : seq.FirstOrDefault();
                    }
                    if (wanted.Contains("major"))
                    {
                        var seq = playerCaster.KnownSpells.Where(s => s.SpellName.IndexOf("major", StringComparison.OrdinalIgnoreCase) >= 0 && s.SpellName.IndexOf("heal", StringComparison.OrdinalIgnoreCase) >= 0)
                            .Concat(playerCaster.KnownSpells.Where(s => s.SpellName.IndexOf("healing", StringComparison.OrdinalIgnoreCase) >= 0));
                        return restrictToBeneficial.Value ? seq.FirstOrDefault(IsBeneficialSpell) : seq.FirstOrDefault();
                    }
                    // Generic heal fallback
                    var seqHeal = playerCaster.KnownSpells.Where(s => s.SpellName.IndexOf("heal", StringComparison.OrdinalIgnoreCase) >= 0 || s.SpellName.IndexOf("healing", StringComparison.OrdinalIgnoreCase) >= 0);
                    return restrictToBeneficial.Value ? seqHeal.FirstOrDefault(IsBeneficialSpell) : seqHeal.FirstOrDefault();
                }
            }

            // Fallback: search all spells in game data
            var allSpells = Resources.FindObjectsOfTypeAll<Spell>();
            var exactAll = allSpells.FirstOrDefault(s => s.SpellName.Equals(spellName, StringComparison.OrdinalIgnoreCase));
            if (exactAll != null)
            {
                if (!restrictToBeneficial.Value || IsBeneficialSpell(exactAll)) return exactAll;
            }
            // Normalized contains across all loaded assets
            string NormAll(string x) => new string(x.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            var wantedAll = NormAll(spellName);
            var allSeq = allSpells.Where(s =>
                NormAll(s.SpellName).Contains(wantedAll) || wantedAll.Contains(NormAll(s.SpellName)));
            return restrictToBeneficial.Value ? allSeq.FirstOrDefault(IsBeneficialSpell) : allSeq.FirstOrDefault();
        }

        // --- Cooldown helpers ---
        private readonly System.Collections.Generic.Dictionary<string, float> localCooldownUntil = new System.Collections.Generic.Dictionary<string, float>();

        private System.Collections.IEnumerator DelayedInitialization()
        {
            // Wait one frame to ensure EventSystem is properly registered
            yield return null;

            // Initialize party UI hook instead of creating duplicate UI
            if (enablePartyUIHook.Value)
            {
                InitializePartyUIHook();
            }

            // Initialize spell configuration UI
            InitializeSpellConfigUI();

            // Initialize auto-target overlay
            InitializeAutoTargetOverlay();
        }

        private bool IsSpellOnCooldown(CastSpell caster, Spell spell, out float remainingSeconds)
        {
            remainingSeconds = 0f;
            if (caster == null || spell == null) return false;

            // Try common method names on CastSpell
            var t = caster.GetType();
            var methodNames = new[]
            {
                "GetCooldownRemaining", "CooldownRemaining", "GetSpellCooldownRemaining", "GetRemainingCooldown", "GetRecastRemaining"
            };
            foreach (var name in methodNames)
            {
                try
                {
                    var mi = t.GetMethod(name, new System.Type[] { typeof(Spell) });
                    if (mi != null && mi.ReturnType == typeof(float))
                    {
                        var val = (float)mi.Invoke(caster, new object[] { spell });
                        if (val > 0.001f) { remainingSeconds = val; return true; }
                    }
                }
                catch { }
            }

            // Try property/dictionary patterns on caster
            try
            {
                var pi = t.GetProperty("GlobalCooldownRemaining");
                if (pi != null && (pi.PropertyType == typeof(float) || pi.PropertyType == typeof(double)))
                {
                    var v = System.Convert.ToSingle(pi.GetValue(caster, null));
                    if (v > 0.001f) { remainingSeconds = v; return true; }
                }
            }
            catch { }

            // Try using spell data for cooldown and track last cast locally
            if (TryGetSpellCooldownSeconds(spell, out var cd))
            {
                if (localCooldownUntil.TryGetValue(spell.SpellName, out var until))
                {
                    var now = Time.time;
                    if (until > now)
                    {
                        remainingSeconds = until - now;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetSpellCooldownSeconds(Spell spell, out float seconds)
        {
            seconds = 0f;
            if (spell == null) return false;
            var t = spell.GetType();
            // Candidate fields/properties that might hold cooldown (seconds)
            var names = new[] { "Cooldown", "CooldownTime", "Recast", "RecastTime", "ReuseTime", "CoolDown", "CoolDownTime", "CooldownSeconds" };
            foreach (var n in names)
            {
                try
                {
                    var pi = t.GetProperty(n);
                    if (pi != null && (pi.PropertyType == typeof(int) || pi.PropertyType == typeof(float) || pi.PropertyType == typeof(double)))
                    {
                        var v = System.Convert.ToSingle(pi.GetValue(spell, null));
                        if (v > 0.001f) { seconds = NormalizeCooldownValue(n, v); return true; }
                    }
                    var fi = t.GetField(n);
                    if (fi != null && (fi.FieldType == typeof(int) || fi.FieldType == typeof(float) || fi.FieldType == typeof(double)))
                    {
                        var v = System.Convert.ToSingle(fi.GetValue(spell));
                        if (v > 0.001f) { seconds = NormalizeCooldownValue(n, v); return true; }
                    }
                }
                catch { }
            }
            return false;
        }

        private float NormalizeCooldownValue(string name, float raw)
        {
            // Heuristic: if property hints ms or value is large, convert to seconds
            var lower = name.ToLowerInvariant();
            if (lower.Contains("ms") || raw > 120f)
                return raw / 1000f;
            return raw;
        }

        private bool IsLocallyThrottled(Spell spell)
        {
            if (spell == null) return false;
            if (localCooldownUntil.TryGetValue(spell.SpellName, out var until))
            {
                if (until > Time.time) return true;
            }
            return false;
        }

        private void SetLocalCooldown(Spell spell)
        {
            if (spell == null) return;
            float cd = defaultGCDSeconds.Value;
            if (TryGetSpellCooldownSeconds(spell, out var s)) cd = Mathf.Max(s, defaultGCDSeconds.Value);
            localCooldownUntil[spell.SpellName] = Time.time + Mathf.Max(0.05f, cd);
        }

        // Heuristic: treat only clearly positive spells as beneficial
        public bool IsBeneficialSpellName(string spellName)
        {
            if (string.IsNullOrEmpty(spellName)) return false;
            var n = spellName.ToLowerInvariant();

            // Positive keywords
            string[] good = new[]
            {
                "heal", "healing", "regrowth", "regenerate", "regeneration", "revitalize", "revive", "resurrect",
                "protection", "protect", "shield", "barrier", "ward", "bless", "blessing", "aura", "renew",
                "cure", "antidote", "cleanse", "invigor", "vital", "vigor", "fortify", "haste", "buff", "group heal",
                "presence", "blessing of", "gift", "greater heal", "supreme heal"
            };
            if (good.Any(k => n.Contains(k))) return true;

            // Negative keywords (explicitly exclude damagey names)
            string[] bad = new[]
            {
                "bolt", "blast", "shock", "strike", "smite", "quake", "fang", "thorn", "poison", "rot", "decay",
                "death", "void", "inferno", "immolation", "storm", "lightning", "ice", "fire", "lava", "venom",
                "anihil", "annihil", "devour", "doom", "blast", "sting", "fury", "rage", "wrath", "bite", "bleed",
            };
            if (bad.Any(k => n.Contains(k))) return false;

            // Default to false to be safe
            return false;
        }

        public bool IsBeneficialSpell(Spell s) => s != null && IsBeneficialSpellName(s.SpellName);

        public bool RestrictToBeneficialEnabled => restrictToBeneficial.Value;

        public bool IsLauncherButtonHidden => hideLauncherButton.Value;

        public void SetLauncherButtonHidden(bool hidden)
        {
            hideLauncherButton.Value = hidden;
        }

        public void SaveCurrentCharacterConfig()
        {
            if (!string.IsNullOrEmpty(lastPlayerIdentity))
            {
                SaveCharacterConfig(lastPlayerIdentity);
            }
        }

        public bool IsCharacterLoggedIn()
        {
            // Check multiple indicators to ensure we're actually in-game, not just at character selection
            try
            {
                // First check scene - avoid character selection scenes
                var activeScene = SceneManager.GetActiveScene();
                string currentSceneName = activeScene.IsValid() ? activeScene.name : "Unknown";

                if (!string.IsNullOrEmpty(currentSceneName))
                {
                    var sceneName = currentSceneName.ToLowerInvariant();
                    // Exclude character selection scenes - specifically LoadScene and other common names
                    if (sceneName.Contains("loadscene") || sceneName.Contains("character") ||
                        sceneName.Contains("select") || sceneName.Contains("login") ||
                        sceneName.Contains("menu") || sceneName.Contains("lobby") ||
                        sceneName.Contains("title") || sceneName == "loadscene")
                    {
                        return false;
                    }
                }

                if (GameData.PlayerStats == null || string.IsNullOrEmpty(GameData.PlayerStats.MyName))
                    return false;

                if (GameData.PlayerControl == null)
                    return false;

                // Additional check: ensure player has meaningful health values (not just initialized)
                if (GameData.PlayerStats.CurrentHP <= 0 || GameData.PlayerStats.CurrentMaxHP <= 0)
                    return false;

                // Check if player can cast spells (only available in-game)
                var caster = GameData.PlayerControl.GetComponent<CastSpell>();
                if (caster == null)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Character-specific configuration management
        private void LoadCharacterConfig(string characterName)
        {
            try
            {
                if (string.IsNullOrEmpty(characterName))
                    return;

                var configPath = GetCharacterConfigPath(characterName);
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    currentCharacterConfig = JsonUtility.FromJson<CharacterConfig>(json);
                    if (currentCharacterConfig == null)
                    {
                        currentCharacterConfig = new CharacterConfig();
                    }

                    if (!string.IsNullOrEmpty(json) && !json.Contains("\"AutoTargetEnabled\""))
                    {
                        currentCharacterConfig.AutoTargetEnabled = autoTargetEnabled.Value;
                    }

                    // Apply loaded config to current settings
                    ApplyCharacterConfig();
                }
                else
                {
                    // Create new config with current global settings
                    // For new characters, default keybind spells to left-click spell
                    var defaultKeybindSpell = leftClickSpell.Value;
                    currentCharacterConfig = new CharacterConfig
                    {
                        CharacterName = characterName,
                        LeftClickSpell = leftClickSpell.Value,
                        RightClickSpell = rightClickSpell.Value,
                        MiddleClickSpell = middleClickSpell.Value,
                        ShiftLeftClickSpell = shiftLeftClickSpell.Value,
                        ShiftRightClickSpell = shiftRightClickSpell.Value,
                        ShiftMiddleClickSpell = shiftMiddleClickSpell.Value,
                        HealPlayerSpell = defaultKeybindSpell,
                        HealMember1Spell = defaultKeybindSpell,
                        HealMember2Spell = defaultKeybindSpell,
                        HealMember3Spell = defaultKeybindSpell,
                        HideLauncherButton = hideLauncherButton.Value,
                        KnownOnlySpellPicker = false, // Default to false for new characters
                        AutoTargetEnabled = autoTargetEnabled.Value
                    };

                    SaveCharacterConfig(characterName);
                }

                lastLoadedCharacter = characterName;
            }
            catch
            {
                currentCharacterConfig = null;
            }
        }

        private void SaveCharacterConfig(string characterName)
        {
            try
            {
                if (string.IsNullOrEmpty(characterName) || currentCharacterConfig == null)
                    return;

                // Update config with current spell settings
                currentCharacterConfig.CharacterName = characterName;
                currentCharacterConfig.LeftClickSpell = leftClickSpell.Value;
                currentCharacterConfig.RightClickSpell = rightClickSpell.Value;
                currentCharacterConfig.MiddleClickSpell = middleClickSpell.Value;
                currentCharacterConfig.ShiftLeftClickSpell = shiftLeftClickSpell.Value;
                currentCharacterConfig.ShiftRightClickSpell = shiftRightClickSpell.Value;
                currentCharacterConfig.ShiftMiddleClickSpell = shiftMiddleClickSpell.Value;
                currentCharacterConfig.HealPlayerSpell = healPlayerSpell.Value;
                currentCharacterConfig.HealMember1Spell = healMember1Spell.Value;
                currentCharacterConfig.HealMember2Spell = healMember2Spell.Value;
                currentCharacterConfig.HealMember3Spell = healMember3Spell.Value;
                currentCharacterConfig.HideLauncherButton = hideLauncherButton.Value;
                currentCharacterConfig.AutoTargetEnabled = autoTargetEnabled.Value;

                // Get known-only setting from UI if available
                if (spellConfigUI != null)
                {
                    currentCharacterConfig.KnownOnlySpellPicker = spellConfigUI.GetKnownOnlyToggleState();
                }

                var configPath = GetCharacterConfigPath(characterName);
                var configDir = Path.GetDirectoryName(configPath);

                if (!Directory.Exists(configDir))
                    Directory.CreateDirectory(configDir);

                var json = JsonUtility.ToJson(currentCharacterConfig, true);
                File.WriteAllText(configPath, json);
            }
            catch { }
        }

        private void ApplyCharacterConfig()
        {
            if (currentCharacterConfig == null)
                return;

            try
            {
                // Apply spell settings from character config
                leftClickSpell.Value = currentCharacterConfig.LeftClickSpell ?? "Minor Healing";
                rightClickSpell.Value = currentCharacterConfig.RightClickSpell ?? "Major Healing";
                middleClickSpell.Value = currentCharacterConfig.MiddleClickSpell ?? "Group Heal";
                shiftLeftClickSpell.Value = currentCharacterConfig.ShiftLeftClickSpell ?? "";
                shiftRightClickSpell.Value = currentCharacterConfig.ShiftRightClickSpell ?? "";
                shiftMiddleClickSpell.Value = currentCharacterConfig.ShiftMiddleClickSpell ?? "";

                // For keybind spells, default to left-click spell if not set
                var keybindDefault = currentCharacterConfig.LeftClickSpell ?? "Minor Healing";
                healPlayerSpell.Value = currentCharacterConfig.HealPlayerSpell ?? keybindDefault;
                healMember1Spell.Value = currentCharacterConfig.HealMember1Spell ?? keybindDefault;
                healMember2Spell.Value = currentCharacterConfig.HealMember2Spell ?? keybindDefault;
                healMember3Spell.Value = currentCharacterConfig.HealMember3Spell ?? keybindDefault;

                // Apply UI settings
                hideLauncherButton.Value = currentCharacterConfig.HideLauncherButton;
                autoTargetEnabled.Value = currentCharacterConfig.AutoTargetEnabled;

                // Apply known-only setting to UI if available
                if (spellConfigUI != null)
                {
                    spellConfigUI.SetKnownOnlyToggleState(currentCharacterConfig.KnownOnlySpellPicker);
                    spellConfigUI.SetAutoTargetToggleState(autoTargetEnabled.Value);
                }

            }
            catch { }
        }

        private string GetCharacterConfigPath(string characterName)
        {
            // Sanitize character name for file system
            var safeName = string.Join("_", characterName.Split(Path.GetInvalidFileNameChars()));
            var configDir = Path.Combine(GetPluginDirectory(), "Characters");
            return Path.Combine(configDir, $"{safeName}.json");
        }

        // Debug overlay removed

        private void OnDestroy()
        {
            if (partyUIHook != null)
            {
                Destroy(partyUIHook.gameObject);
            }

            if (spellConfigUI != null)
            {
                Destroy(spellConfigUI.gameObject);
            }

            if (autoTargetOverlay != null)
            {
                Destroy(autoTargetOverlay.gameObject);
                autoTargetOverlay = null;
            }

            _harmony?.UnpatchSelf();
        }

        private void EnsureEventSystem()
        {
            if (eventSystem != null)
            {
                // Ensure our EventSystem is still the active one
                if (FindObjectsOfType<EventSystem>().Length > 1)
                {
                    // Deactivate any other EventSystems
                    var otherSystems = FindObjectsOfType<EventSystem>();
                    foreach (var es in otherSystems)
                    {
                        if (es != eventSystem)
                        {
                            es.enabled = false;
                        }
                    }
                }
                return;
            }

            eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                // Create a dedicated EventSystem GameObject as root
                var eventSystemGO = new GameObject("HealbotEventSystem");
                // Make sure it's at root level before DontDestroyOnLoad
                eventSystemGO.transform.SetParent(null);
                DontDestroyOnLoad(eventSystemGO);

                eventSystem = eventSystemGO.AddComponent<EventSystem>();
                var inputModule = eventSystemGO.AddComponent<StandaloneInputModule>();

                // Configure input module for proper global input handling
                inputModule.horizontalAxis = "Horizontal";
                inputModule.verticalAxis = "Vertical";
                inputModule.submitButton = "Submit";
                inputModule.cancelButton = "Cancel";

                // Allow background input for global hotkeys like Ctrl+H
                inputModule.allowActivationOnMobileDevice = true;

                Logger.LogInfo("Created centralized EventSystem for Healbot");
            }
            else
            {
                Logger.LogInfo("Using existing EventSystem for Healbot");
            }
        }
    }

    // Data classes
    public class GroupMember
    {
        public Stats stats;
        public string name;
        public bool isPlayer;
    }

    [System.Serializable]
    public class CharacterConfig
    {
        public string CharacterName;
        public string LeftClickSpell;
        public string RightClickSpell;
        public string MiddleClickSpell;
        public string ShiftLeftClickSpell;
        public string ShiftRightClickSpell;
        public string ShiftMiddleClickSpell;
        public string HealPlayerSpell;
        public string HealMember1Spell;
        public string HealMember2Spell;
        public string HealMember3Spell;
        public bool HideLauncherButton;
        public bool KnownOnlySpellPicker;
        public bool AutoTargetEnabled;
    }
}




