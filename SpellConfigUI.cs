using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace ErenshorHealbot
{
    public class SpellConfigUI : MonoBehaviour
    {
        private HealbotPlugin plugin;
        private GameObject configWindow;
        private bool isWindowVisible = false;

        // UI References
        private TMP_Dropdown leftClickDropdown;
        private TMP_Dropdown rightClickDropdown;
        private TMP_Dropdown middleClickDropdown;
        private Button saveButton;
        private Button cancelButton;
        private Button closeButton;

        // Available spells list
        private List<string> availableSpells = new List<string>();

        // Temporary settings before saving
        private string tempLeftClickSpell;
        private string tempRightClickSpell;
        private string tempMiddleClickSpell;

        public void Initialize(HealbotPlugin healbotPlugin)
        {
            plugin = healbotPlugin;
            RefreshAvailableSpells();
            CreateConfigUI();
        }

        private void Update()
        {
            // Fallback keybind to open UI (Ctrl+H)
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.H))
            {
                ToggleConfigWindow();
            }

            // Refresh spells periodically in case player learns new ones
            if (Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
            {
                RefreshAvailableSpells();
                UpdateDropdownOptions();
            }
        }

        public void ToggleConfigWindow()
        {
            if (configWindow == null)
            {
                CreateConfigUI();
            }

            isWindowVisible = !isWindowVisible;
            configWindow.SetActive(isWindowVisible);

            if (isWindowVisible)
            {
                LoadCurrentSettings();
                RefreshAvailableSpells();
                UpdateDropdownOptions();
            }
        }

        private void RefreshAvailableSpells()
        {
            availableSpells.Clear();
            availableSpells.Add("None"); // Option to disable a click

            var playerCaster = GameData.PlayerControl?.GetComponent<CastSpell>();
            if (playerCaster?.KnownSpells != null)
            {
                var spellNames = playerCaster.KnownSpells
                    .Where(s => s != null && !string.IsNullOrEmpty(s.SpellName))
                    .Select(s => s.SpellName)
                    .OrderBy(name => name)
                    .ToList();

                availableSpells.AddRange(spellNames);
            }

            // If no spells found, add some common defaults
            if (availableSpells.Count <= 1)
            {
                availableSpells.AddRange(new[] { "Minor Healing", "Major Healing", "Group Heal", "Heal" });
            }
        }

        private void CreateConfigUI()
        {
            // Create main canvas if it doesn't exist
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("SpellConfigCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000; // High order to appear on top
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
                DontDestroyOnLoad(canvasGO);
            }

            // Create main window
            configWindow = new GameObject("SpellConfigWindow");
            configWindow.transform.SetParent(canvas.transform, false);

            var windowRect = configWindow.AddComponent<RectTransform>();
            windowRect.sizeDelta = new Vector2(400, 300);
            windowRect.anchoredPosition = Vector2.zero;

            // Window background
            var windowImage = configWindow.AddComponent<Image>();
            windowImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            // Window border
            var outline = configWindow.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(2, 2);

            CreateWindowContent();

            configWindow.SetActive(false);
        }

        private void CreateWindowContent()
        {
            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(configWindow.transform, false);
            var titleRect = titleGO.AddComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(380, 30);
            titleRect.anchoredPosition = new Vector2(0, 120);

            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "Spell Configuration";
            titleText.fontSize = 18;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;

            // Left Click Section
            CreateSpellSelector("Left Click:", new Vector2(0, 70), out leftClickDropdown);

            // Right Click Section
            CreateSpellSelector("Right Click:", new Vector2(0, 30), out rightClickDropdown);

            // Middle Click Section
            CreateSpellSelector("Middle Click:", new Vector2(0, -10), out middleClickDropdown);

            // Buttons
            CreateButtons();

            // Instructions
            CreateInstructions();
        }

        private void CreateSpellSelector(string labelText, Vector2 position, out TMP_Dropdown dropdown)
        {
            // Label
            var labelGO = new GameObject($"{labelText}Label");
            labelGO.transform.SetParent(configWindow.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(120, 25);
            labelRect.anchoredPosition = new Vector2(-120, position.y);

            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 14;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;

            // Dropdown
            var dropdownGO = new GameObject($"{labelText}Dropdown");
            dropdownGO.transform.SetParent(configWindow.transform, false);
            var dropdownRect = dropdownGO.AddComponent<RectTransform>();
            dropdownRect.sizeDelta = new Vector2(200, 25);
            dropdownRect.anchoredPosition = new Vector2(40, position.y);

            var dropdownImage = dropdownGO.AddComponent<Image>();
            dropdownImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            dropdown = dropdownGO.AddComponent<TMP_Dropdown>();

            // Create dropdown template
            var template = CreateDropdownTemplate(dropdownGO);
            dropdown.template = template;

            // Dropdown label (selected option display)
            var labelGO2 = new GameObject("Label");
            labelGO2.transform.SetParent(dropdownGO.transform, false);
            var labelRect2 = labelGO2.AddComponent<RectTransform>();
            labelRect2.sizeDelta = new Vector2(-25, 0);
            labelRect2.anchoredPosition = new Vector2(-2.5f, 0);
            labelRect2.anchorMin = Vector2.zero;
            labelRect2.anchorMax = Vector2.one;

            var labelText2 = labelGO2.AddComponent<TextMeshProUGUI>();
            labelText2.fontSize = 12;
            labelText2.color = Color.white;
            labelText2.alignment = TextAlignmentOptions.MidlineLeft;
            dropdown.captionText = labelText2;

            // Arrow
            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(dropdownGO.transform, false);
            var arrowRect = arrowGO.AddComponent<RectTransform>();
            arrowRect.sizeDelta = new Vector2(20, 20);
            arrowRect.anchoredPosition = new Vector2(-10, 0);
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);

            var arrowImage = arrowGO.AddComponent<Image>();
            arrowImage.color = Color.white;
            // You might want to load a proper arrow sprite here
        }

        private RectTransform CreateDropdownTemplate(GameObject dropdownParent)
        {
            var templateGO = new GameObject("Template");
            templateGO.transform.SetParent(dropdownParent.transform, false);
            var templateRect = templateGO.AddComponent<RectTransform>();
            templateRect.sizeDelta = new Vector2(0, 150);
            templateRect.anchoredPosition = new Vector2(0, -75);
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);

            templateGO.SetActive(false);

            var templateImage = templateGO.AddComponent<Image>();
            templateImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(templateGO.transform, false);
            var viewportRect = viewportGO.AddComponent<RectTransform>();
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;

            var viewportMask = viewportGO.AddComponent<Mask>();
            var viewportImage = viewportGO.AddComponent<Image>();
            viewportImage.color = Color.clear;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(0, 150);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.anchoredPosition = new Vector2(0, 0);

            // Item
            var itemGO = new GameObject("Item");
            itemGO.transform.SetParent(contentGO.transform, false);
            var itemRect = itemGO.AddComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(0, 20);
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);

            var itemToggle = itemGO.AddComponent<Toggle>();
            var itemImage = itemGO.AddComponent<Image>();
            itemImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Item Label
            var itemLabelGO = new GameObject("Item Label");
            itemLabelGO.transform.SetParent(itemGO.transform, false);
            var itemLabelRect = itemLabelGO.AddComponent<RectTransform>();
            itemLabelRect.sizeDelta = Vector2.zero;
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;

            var itemLabelText = itemLabelGO.AddComponent<TextMeshProUGUI>();
            itemLabelText.fontSize = 12;
            itemLabelText.color = Color.white;
            itemLabelText.alignment = TextAlignmentOptions.MidlineLeft;
            itemLabelText.margin = new Vector4(10, 0, 10, 0);

            itemToggle.targetGraphic = itemImage;
            itemToggle.graphic = null;

            // ScrollRect for template
            var scrollRect = templateGO.AddComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;

            return templateRect;
        }

        private void CreateButtons()
        {
            // Save Button
            saveButton = CreateButton("Save", new Vector2(-60, -70), new Color(0.2f, 0.6f, 0.2f, 1f), SaveSettings);

            // Cancel Button
            cancelButton = CreateButton("Cancel", new Vector2(0, -70), new Color(0.6f, 0.6f, 0.2f, 1f), CancelSettings);

            // Close Button
            closeButton = CreateButton("Close", new Vector2(60, -70), new Color(0.6f, 0.2f, 0.2f, 1f), CloseWindow);
        }

        private Button CreateButton(string text, Vector2 position, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var buttonGO = new GameObject($"{text}Button");
            buttonGO.transform.SetParent(configWindow.transform, false);
            var buttonRect = buttonGO.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(80, 30);
            buttonRect.anchoredPosition = position;

            var buttonImage = buttonGO.AddComponent<Image>();
            buttonImage.color = color;

            var button = buttonGO.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(onClick);

            // Button Text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.sizeDelta = Vector2.zero;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;

            var buttonText = textGO.AddComponent<TextMeshProUGUI>();
            buttonText.text = text;
            buttonText.fontSize = 12;
            buttonText.color = Color.white;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.fontStyle = FontStyles.Bold;

            return button;
        }

        private void CreateInstructions()
        {
            var instructionsGO = new GameObject("Instructions");
            instructionsGO.transform.SetParent(configWindow.transform, false);
            var instructionsRect = instructionsGO.AddComponent<RectTransform>();
            instructionsRect.sizeDelta = new Vector2(380, 40);
            instructionsRect.anchoredPosition = new Vector2(0, -120);

            var instructionsText = instructionsGO.AddComponent<TextMeshProUGUI>();
            instructionsText.text = "Configure which spells to cast when clicking on party members.\nType '/healbot' in chat or press Ctrl+H to open this window.";
            instructionsText.fontSize = 10;
            instructionsText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            instructionsText.alignment = TextAlignmentOptions.Center;
        }

        private void UpdateDropdownOptions()
        {
            if (leftClickDropdown != null)
            {
                leftClickDropdown.options.Clear();
                foreach (var spell in availableSpells)
                {
                    leftClickDropdown.options.Add(new TMP_Dropdown.OptionData(spell));
                }
                leftClickDropdown.RefreshShownValue();
            }

            if (rightClickDropdown != null)
            {
                rightClickDropdown.options.Clear();
                foreach (var spell in availableSpells)
                {
                    rightClickDropdown.options.Add(new TMP_Dropdown.OptionData(spell));
                }
                rightClickDropdown.RefreshShownValue();
            }

            if (middleClickDropdown != null)
            {
                middleClickDropdown.options.Clear();
                foreach (var spell in availableSpells)
                {
                    middleClickDropdown.options.Add(new TMP_Dropdown.OptionData(spell));
                }
                middleClickDropdown.RefreshShownValue();
            }
        }

        private void LoadCurrentSettings()
        {
            if (plugin == null) return;

            // Get current spell settings from plugin
            tempLeftClickSpell = plugin.GetSpellForButton(UnityEngine.EventSystems.PointerEventData.InputButton.Left);
            tempRightClickSpell = plugin.GetSpellForButton(UnityEngine.EventSystems.PointerEventData.InputButton.Right);
            tempMiddleClickSpell = plugin.GetSpellForButton(UnityEngine.EventSystems.PointerEventData.InputButton.Middle);

            // Set dropdown values
            SetDropdownValue(leftClickDropdown, tempLeftClickSpell);
            SetDropdownValue(rightClickDropdown, tempRightClickSpell);
            SetDropdownValue(middleClickDropdown, tempMiddleClickSpell);
        }

        private void SetDropdownValue(TMP_Dropdown dropdown, string value)
        {
            if (dropdown == null || string.IsNullOrEmpty(value)) return;

            for (int i = 0; i < dropdown.options.Count; i++)
            {
                if (dropdown.options[i].text.Equals(value, System.StringComparison.OrdinalIgnoreCase))
                {
                    dropdown.value = i;
                    break;
                }
            }
        }

        private void SaveSettings()
        {
            if (plugin == null) return;

            // Get selected values
            string newLeftSpell = leftClickDropdown.value < availableSpells.Count ? availableSpells[leftClickDropdown.value] : "None";
            string newRightSpell = rightClickDropdown.value < availableSpells.Count ? availableSpells[rightClickDropdown.value] : "None";
            string newMiddleSpell = middleClickDropdown.value < availableSpells.Count ? availableSpells[middleClickDropdown.value] : "None";

            // Convert "None" to empty string for config
            if (newLeftSpell == "None") newLeftSpell = "";
            if (newRightSpell == "None") newRightSpell = "";
            if (newMiddleSpell == "None") newMiddleSpell = "";

            // Save to plugin configuration
            plugin.UpdateSpellBindings(newLeftSpell, newRightSpell, newMiddleSpell);

            CloseWindow();
        }

        private void CancelSettings()
        {
            // Reload current settings to discard changes
            LoadCurrentSettings();
        }

        private void CloseWindow()
        {
            isWindowVisible = false;
            if (configWindow != null)
            {
                configWindow.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (configWindow != null)
            {
                Destroy(configWindow);
            }
        }
    }
}