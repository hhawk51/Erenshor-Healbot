using HarmonyLib;
using UnityEngine;
using System.Linq;

namespace ErenshorHealbot
{
    [HarmonyPatch]
    public static class HealbotPatches
    {
        // Patch to detect when spells are learned
        [HarmonyPatch(typeof(CastSpell), "Start")]
        [HarmonyPostfix]
        static void CastSpell_Start_Postfix(CastSpell __instance)
        {
            if (__instance.isPlayer && HealbotPlugin.Instance != null)
            {
                try
                {
                    var names = (__instance.KnownSpells != null)
                        ? string.Join(", ", __instance.KnownSpells.Select(s => s.SpellName))
                        : "(none)";
                    BepInEx.Logging.Logger.CreateLogSource("Healbot").LogInfo($"Player spells: {names}");
                }
                catch { }
            }
        }
    }
}
