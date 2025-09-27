using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace ErenshorHealbot
{
    [BepInPlugin("Hawtin.Erenshor.Healbot", "ErenshorHealbot", "1.0.0")]
    public class HealbotPlugin : BaseUnityPlugin
    {
        public static HealbotPlugin Instance { get; private set; }

        private Harmony _harmony;
        private PartyUIHook partyUIHook;
        private SpellConfigUI spellConfigUI;
        

        // Configuration
        private ConfigEntry<KeyCode> toggleUIKey;
        private ConfigEntry<KeyCode> openConfigKey;
        private ConfigEntry<string> leftClickSpell;
        private ConfigEntry<string> rightClickSpell;
        private ConfigEntry<string> middleClickSpell;
        private ConfigEntry<bool> autoTargetEnabled;
        private ConfigEntry<float> healthThreshold;
        private ConfigEntry<bool> debugOverlay;
        private ConfigEntry<bool> enablePartyUIHook;
        private ConfigEntry<bool> restrictToBeneficial;
        private ConfigEntry<float> defaultGCDSeconds;
        private ConfigEntry<KeyCode> healPlayerKey;
        private ConfigEntry<KeyCode> healMember1Key;
        private ConfigEntry<KeyCode> healMember2Key;
        private ConfigEntry<KeyCode> healMember3Key;
        private ConfigEntry<string> healPlayerSpell;
        private ConfigEntry<string> healMember1Spell;
        private ConfigEntry<string> healMember2Spell;
        private ConfigEntry<string> healMember3Spell;

        // Group member tracking for auto-targeting
        private List<GroupMember> groupMembers = new List<GroupMember>();

        private void Awake()
        {
            Instance = this;

            // Setup configuration
            toggleUIKey = Config.Bind("Controls", "ToggleUI", KeyCode.H, "Key to toggle healbot UI");
            openConfigKey = Config.Bind("Controls", "OpenConfig", KeyCode.F10, "Key to open the healbot spell configuration window");
            leftClickSpell = Config.Bind("Spells", "LeftClick", "Minor Healing", "Spell to cast on left click");
            rightClickSpell = Config.Bind("Spells", "RightClick", "Major Healing", "Spell to cast on right click");
            middleClickSpell = Config.Bind("Spells", "MiddleClick", "Group Heal", "Spell to cast on middle click");
            autoTargetEnabled = Config.Bind("Automation", "AutoTarget", true, "Automatically target low health members");
            healthThreshold = Config.Bind("Automation", "HealthThreshold", 0.5f, "Health percentage to consider 'low' (0.0-1.0)");
            debugOverlay = Config.Bind("Debug", "DebugOverlay", false, "Show a small on-screen debug overlay");
            enablePartyUIHook = Config.Bind("UI", "EnablePartyUIHook", true, "Enable click-to-heal on existing party UI");
            restrictToBeneficial = Config.Bind("Spells", "RestrictToBeneficial", true, "Only allow beneficial spells (heals/buffs) when casting via Healbot");
            defaultGCDSeconds = Config.Bind("Spells", "DefaultGCDSeconds", 1.5f, "Fallback minimum time between casts when underlying cooldown info is unavailable");
            healPlayerKey = Config.Bind("Keybinds", "HealPlayer", KeyCode.F1, "Key to heal the player");
            healMember1Key = Config.Bind("Keybinds", "HealMember1", KeyCode.F2, "Key to heal party member 1");
            healMember2Key = Config.Bind("Keybinds", "HealMember2", KeyCode.F3, "Key to heal party member 2");
            healMember3Key = Config.Bind("Keybinds", "HealMember3", KeyCode.F4, "Key to heal party member 3");
            healPlayerSpell = Config.Bind("KeybindSpells", "HealPlayerSpell", "Minor Healing", "Spell to cast when healing the player");
            healMember1Spell = Config.Bind("KeybindSpells", "HealMember1Spell", "Minor Healing", "Spell to cast when healing party member 1");
            healMember2Spell = Config.Bind("KeybindSpells", "HealMember2Spell", "Minor Healing", "Spell to cast when healing party member 2");
            healMember3Spell = Config.Bind("KeybindSpells", "HealMember3Spell", "Minor Healing", "Spell to cast when healing party member 3");

            _harmony = new Harmony("Hawtin.Erenshor.Healbot");
            try
            {
                _harmony.PatchAll();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Harmony patching failed: {ex.Message}");
            }

            Logger.LogInfo("Erenshor Healbot loaded!");

            // Initialize party UI hook instead of creating duplicate UI
            if (enablePartyUIHook.Value)
            {
                InitializePartyUIHook();
            }

            // Initialize spell configuration UI
            InitializeSpellConfigUI();
        }

        private void InitializePartyUIHook()
        {
            if (partyUIHook == null)
            {
                var hookGO = new GameObject("PartyUIHook");
                DontDestroyOnLoad(hookGO);
                partyUIHook = hookGO.AddComponent<PartyUIHook>();
                partyUIHook.Initialize(this);
                Logger.LogInfo("Party UI hook initialized - click-to-heal enabled on existing party UI");
            }
        }

        private void InitializeSpellConfigUI()
        {
            if (spellConfigUI == null)
            {
                var configUIGO = new GameObject("SpellConfigUI");
                configUIGO.transform.SetParent(null); // Ensure it's a root object
                DontDestroyOnLoad(configUIGO);
                spellConfigUI = configUIGO.AddComponent<SpellConfigUI>();
                spellConfigUI.Initialize(this);
                
                Logger.LogInfo("Spell configuration UI initialized - press the OpenConfig key (default F10) or Ctrl+H to configure spells");
            }
        }

        public string GetSpellForButton(PointerEventData.InputButton button)
        {
            switch (button)
            {
                case PointerEventData.InputButton.Left:
                    return leftClickSpell.Value;
                case PointerEventData.InputButton.Right:
                    return rightClickSpell.Value;
                case PointerEventData.InputButton.Middle:
                    return middleClickSpell.Value;
                default:
                    return null;
            }
        }

        public void UpdateSpellBindings(string leftSpell, string rightSpell, string middleSpell)
        {
            // Update the configuration entries
            if (!string.IsNullOrEmpty(leftSpell))
                leftClickSpell.Value = leftSpell;
            if (!string.IsNullOrEmpty(rightSpell))
                rightClickSpell.Value = rightSpell;
            if (!string.IsNullOrEmpty(middleSpell))
                middleClickSpell.Value = middleSpell;

            Logger.LogInfo($"Spell bindings updated: Left={leftClickSpell.Value}, Right={rightClickSpell.Value}, Middle={middleClickSpell.Value}");
        }

        private void Update()
        {
            // Toggle party UI hook
            if (Input.GetKeyDown(toggleUIKey.Value))
            {
                TogglePartyUIHook();
            }

            // Open configuration window
            if (Input.GetKeyDown(openConfigKey.Value) && spellConfigUI != null)
            {
                spellConfigUI.ToggleConfigWindow();
            }

            // Manual healing keybinds
            CheckHealingKeybinds();

            // Update group members for auto-targeting
            UpdateGroupMembers();

            // Auto-targeting for low health members
            if (autoTargetEnabled.Value)
            {
                CheckAutoTarget();
            }
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
                    Logger.LogInfo($"Healing {memberDesc} ({GameData.PlayerStats.MyName}) with {spellToUse}");
                    CastSpellOnTarget(GameData.PlayerStats, spellToUse);
                }
                else
                {
                    Logger.LogWarning($"Cannot heal {memberDesc} - no player stats");
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
                        Logger.LogInfo($"Healing {memberDesc} ({member.name}) with {spellToUse}");
                        CastSpellOnTarget(member.stats, spellToUse);
                    }
                    else
                    {
                        Logger.LogWarning($"Cannot heal {memberDesc} - no member found");
                    }
                }
                else
                {
                    Logger.LogWarning($"Cannot heal {memberDesc} - not enough group members ({groupMembers.Count})");
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
                Logger.LogInfo("Healbot party UI hook toggled ON");
                InitializePartyUIHook();
            }
            else
            {
                Logger.LogInfo("Healbot party UI hook toggled OFF");
                Destroy(partyUIHook.gameObject);
                partyUIHook = null;
            }
        }

        private int lastLoggedMemberCount = -1;

        private void UpdateGroupMembers()
        {
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
            }

            // Add party members from GameData.GroupMembers
            for (int i = 0; i < GameData.GroupMembers.Length; i++)
            {
                var tracking = GameData.GroupMembers[i];
                if (tracking != null && tracking.MyAvatar != null && tracking.MyAvatar.MyStats != null)
                {
                    var s = tracking.MyAvatar.MyStats;
                    string displayName = !string.IsNullOrEmpty(s.MyName) ? s.MyName : tracking.SimName;
                    var npc = tracking.MyAvatar.GetComponent<NPC>();
                    if (npc != null && !string.IsNullOrEmpty(npc.NPCName))
                        displayName = npc.NPCName;

                    // Fallback if name is still empty
                    if (string.IsNullOrEmpty(displayName))
                        displayName = $"Member{i + 1}";

                    groupMembers.Add(new GroupMember
                    {
                        stats = s,
                        name = displayName,
                        isPlayer = false
                    });
                }
            }

            if (groupMembers.Count != lastLoggedMemberCount)
            {
                lastLoggedMemberCount = groupMembers.Count;
                try
                {
                    var names = string.Join(", ", groupMembers.Select(g => g.name));
                    Logger.LogInfo($"Healbot group detected: {groupMembers.Count} [{names}]");
                }
                catch { }
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

        private void CheckAutoTarget()
        {
            var lowHealthMembers = groupMembers
                .Where(gm => gm.stats != null && gm.stats.CurrentMaxHP > 0 && gm.stats.CurrentHP > 0 && ((float)gm.stats.CurrentHP / (float)gm.stats.CurrentMaxHP) < healthThreshold.Value)
                .OrderBy(gm => (float)gm.stats.CurrentHP / (float)gm.stats.CurrentMaxHP)
                .ToList();

            if (lowHealthMembers.Any())
            {
                // Auto-target the lowest health member
                var target = lowHealthMembers.First();
                if (GameData.PlayerControl != null && target.stats != null)
                {
                    if (GameData.PlayerControl.CurrentTarget != null)
                        GameData.PlayerControl.CurrentTarget.UntargetMe();

                    GameData.PlayerControl.CurrentTarget = target.stats.Myself;
                    GameData.PlayerControl.CurrentTarget.TargetMe();
                }
            }
        }

        public void CastSpellOnTarget(Stats target, string spellName)
        {
            if (target == null || string.IsNullOrEmpty(spellName)) return;

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
                Logger.LogInfo($"'{spell.SpellName}' is on cooldown ({remaining:0.0}s remaining)");
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

        private void OnGUI()
        {
            if (!debugOverlay.Value) return;
            GUI.color = Color.white;
            var rect = new Rect(10, 10, 420, 22);
            int gmCount = groupMembers?.Count ?? 0;
            string playerName = (GameData.PlayerStats != null) ? GameData.PlayerStats.MyName : "(none)";
            string hookStatus = partyUIHook != null ? "Active" : "Inactive";
            GUI.Label(rect, $"Healbot: PartyHook {hookStatus} | Members: {gmCount} | Player: {playerName}");

            // Show detected member names for debugging
            float y = 34f;
            if (gmCount > 0)
            {
                foreach (var gm in groupMembers)
                {
                    var maxHp = Mathf.Max(1, gm.stats.CurrentMaxHP);
                    var hpPct = (int)(100f * gm.stats.CurrentHP / (float)maxHp);
                    GUI.Label(new Rect(10, y, 600, 18), $"- {gm.name} HP {gm.stats.CurrentHP}/{maxHp} ({hpPct}%)");
                    y += 18f;
                }
            }

            // Show party UI hook instructions
            if (partyUIHook != null)
            {
                y += 10f;
                GUI.Label(new Rect(10, y, 600, 18), "Click on party member names/HP bars to heal them!");
                y += 18f;
            }

            // Show keybind instructions
            y += 10f;
            GUI.Label(new Rect(10, y, 600, 18), $"Keybinds: {healPlayerKey.Value}=Player, {healMember1Key.Value}=Member1, {healMember2Key.Value}=Member2, {healMember3Key.Value}=Member3");
            y += 18f;
            GUI.Label(new Rect(10, y, 600, 18), $"Spells: {healPlayerSpell.Value} | {healMember1Spell.Value} | {healMember2Spell.Value} | {healMember3Spell.Value}");
            y += 18f;
            GUI.Label(new Rect(10, y, 600, 18), $"Click Spells: L={leftClickSpell.Value}, R={rightClickSpell.Value}, M={middleClickSpell.Value} | Toggle: {toggleUIKey.Value}");
        }

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

            _harmony?.UnpatchSelf();
        }
    }

    // Data classes
    public class GroupMember
    {
        public Stats stats;
        public string name;
        public bool isPlayer;
    }
}
