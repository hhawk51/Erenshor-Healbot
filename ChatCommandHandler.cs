using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HarmonyLib;
using System.Reflection;

namespace ErenshorHealbot
{
    public class ChatCommandHandler : MonoBehaviour
    {
        private SpellConfigUI spellConfigUI;
        private static ChatCommandHandler instance;

        public static ChatCommandHandler Instance => instance;

        private void Awake()
        {
            instance = this;
        }

        public void Initialize(SpellConfigUI configUI)
        {
            spellConfigUI = configUI;
        }

        // This method will be called by Harmony patch when chat message is processed
        public static void ProcessChatCommand(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            message = message.Trim().ToLower();

            Debug.Log($"[ChatCommandHandler] Processing message: '{message}'");

            if (message == "/healbot" || message == "/healbot config" || message == "/hb")
            {
                Debug.Log("[ChatCommandHandler] Healbot command detected!");
                if (Instance?.spellConfigUI != null)
                {
                    Instance.spellConfigUI.ToggleConfigWindow();
                }
                else
                {
                    Debug.LogWarning("[ChatCommandHandler] SpellConfigUI is null!");
                }
            }
        }
    }

    // Harmony patches to intercept chat messages
    [HarmonyPatch]
    public class ChatMessagePatches
    {
        // Try to patch common chat submission methods
        [HarmonyPatch(typeof(InputField), "OnSubmit")]
        [HarmonyPrefix]
        public static void OnInputFieldSubmit(InputField __instance)
        {
            if (__instance != null && !string.IsNullOrEmpty(__instance.text))
            {
                ChatCommandHandler.ProcessChatCommand(__instance.text);
            }
        }

        // Alternative patch for TMP_InputField
        [HarmonyPatch(typeof(TMP_InputField), "OnSubmit")]
        [HarmonyPrefix]
        public static void OnTMPInputFieldSubmit(TMP_InputField __instance)
        {
            if (__instance != null && !string.IsNullOrEmpty(__instance.text))
            {
                ChatCommandHandler.ProcessChatCommand(__instance.text);
            }
        }

        // Try to find and patch game-specific chat methods
        [HarmonyPatch]
        public static class GenericChatPatch
        {
            static bool Prepare()
            {
                // Try to find chat-related types dynamically
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var types = assembly.GetTypes();
                        foreach (var type in types)
                        {
                            if (type.Name.Contains("Chat") || type.Name.Contains("Message"))
                            {
                                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                                foreach (var method in methods)
                                {
                                    if ((method.Name.Contains("Send") || method.Name.Contains("Submit") || method.Name.Contains("Process")) &&
                                        method.GetParameters().Length > 0 &&
                                        method.GetParameters()[0].ParameterType == typeof(string))
                                    {
                                        // Found a potential chat method
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
                return false;
            }

            static MethodBase TargetMethod()
            {
                // Try to find a suitable chat method to patch
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var types = assembly.GetTypes();
                        foreach (var type in types)
                        {
                            if (type.Name.Contains("Chat") || type.Name.Contains("Message"))
                            {
                                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                                foreach (var method in methods)
                                {
                                    if ((method.Name.Contains("Send") || method.Name.Contains("Submit") || method.Name.Contains("Process")) &&
                                        method.GetParameters().Length > 0 &&
                                        method.GetParameters()[0].ParameterType == typeof(string))
                                    {
                                        return method;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
                return null;
            }

            static void Prefix(string message)
            {
                ChatCommandHandler.ProcessChatCommand(message);
            }
        }
    }

    // Alternative approach: Monitor input fields directly
    public class InputFieldMonitor : MonoBehaviour
    {
        private static InputFieldMonitor instance;
        private InputField currentChatInput;
        private TMP_InputField currentTMPChatInput;

        public static InputFieldMonitor Instance => instance;

        private void Awake()
        {
            instance = this;
        }

        private void Update()
        {
            // Check for input field focus and Enter key
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                CheckForChatCommand();
            }

            // Also check when typing for immediate feedback
            if (Input.inputString.Length > 0)
            {
                CheckForChatCommandImmediate();
            }

            // Periodically scan for chat input fields
            if (Time.frameCount % 60 == 0) // Every second
            {
                ScanForChatInputs();
            }
        }

        private void ScanForChatInputs()
        {
            // Find any InputField that might be a chat input
            var inputFields = FindObjectsOfType<InputField>();
            foreach (var field in inputFields)
            {
                if (field.isFocused && (field.name.ToLower().Contains("chat") ||
                                       field.name.ToLower().Contains("input") ||
                                       field.name.ToLower().Contains("message")))
                {
                    currentChatInput = field;
                    break;
                }
            }

            // Find any TMP_InputField that might be a chat input
            var tmpInputFields = FindObjectsOfType<TMP_InputField>();
            foreach (var field in tmpInputFields)
            {
                if (field.isFocused && (field.name.ToLower().Contains("chat") ||
                                       field.name.ToLower().Contains("input") ||
                                       field.name.ToLower().Contains("message")))
                {
                    currentTMPChatInput = field;
                    break;
                }
            }
        }

        private void CheckForChatCommand()
        {
            string message = null;

            if (currentChatInput != null && currentChatInput.isFocused)
            {
                message = currentChatInput.text;
            }
            else if (currentTMPChatInput != null && currentTMPChatInput.isFocused)
            {
                message = currentTMPChatInput.text;
            }

            if (!string.IsNullOrEmpty(message))
            {
                Debug.Log($"[InputFieldMonitor] Detected chat input: '{message}'");
                ChatCommandHandler.ProcessChatCommand(message);
            }
        }

        private void CheckForChatCommandImmediate()
        {
            // Check all input fields for healbot command
            var allInputFields = FindObjectsOfType<InputField>();
            foreach (var field in allInputFields)
            {
                if (field.isFocused && field.text.ToLower().Contains("/healbot"))
                {
                    Debug.Log($"[InputFieldMonitor] Found /healbot in input field: '{field.text}'");
                    break;
                }
            }

            var allTMPInputFields = FindObjectsOfType<TMP_InputField>();
            foreach (var field in allTMPInputFields)
            {
                if (field.isFocused && field.text.ToLower().Contains("/healbot"))
                {
                    Debug.Log($"[InputFieldMonitor] Found /healbot in TMP input field: '{field.text}'");
                    break;
                }
            }
        }
    }
}