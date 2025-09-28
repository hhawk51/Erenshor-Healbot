using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;

namespace ErenshorHealbot
{
    public class SpellConfigUI : MonoBehaviour
    {
        private HealbotPlugin plugin;
        private Canvas uiCanvas;
        private GameObject configPanel;
        private bool isWindowVisible = false;
        private GameObject backdrop;
        private RectTransform panelRect;
        private GameObject launcherButton;
        private RectTransform launcherRect;
        private Image launcherImage;
        private Text launcherText;

        // Spell picker UI
        private GameObject spellPickerPanel;
        private InputField spellSearchField;
        private ScrollRect spellScrollRect;
        private RectTransform spellListViewport;
        private RectTransform spellListContent;
        private InputField currentPickerTarget;
        private string pendingSearchFilter = string.Empty;
        private const float SearchDebounceSeconds = 0.08f;

        // UI References
        private InputField leftClickInput;
        private InputField rightClickInput;
        private InputField middleClickInput;
        private Button saveButton;
        private Button closeButton;
        private Button refreshButton;
        private Text statusText;

        // Available spells list
        private List<string> availableSpells = new List<string>();

        public void Initialize(HealbotPlugin healbotPlugin)
        {
            plugin = healbotPlugin;
            

            // Create UI immediately but keep it hidden
            CreateSpellConfigUI();
        }

        private void Update()
        {
            // Fallback keybind to open UI (Ctrl+H)
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.H))
            {
                ToggleConfigWindow();
            }

            // Allow closing with Escape when visible
            if (isWindowVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseWindow();
            }
        }

        public void ToggleConfigWindow()
        {
            try
            {
                

                if (configPanel == null)
                {
                    
                    CreateSpellConfigUI();
                }

                isWindowVisible = !isWindowVisible;
                configPanel.SetActive(isWindowVisible);
                if (backdrop != null) backdrop.SetActive(isWindowVisible);
                if (launcherButton != null) launcherButton.SetActive(!isWindowVisible);

                

                if (isWindowVisible)
                {
                    // Only build the spell cache if empty; avoid rescanning every open
                    if (availableSpells == null || availableSpells.Count == 0)
                    {
                        RefreshAvailableSpells();
                    }
                    UpdateInputFields();
                    LoadCurrentSettings();
                    UpdateStatusText("Ready");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SpellConfigUI] Error in ToggleConfigWindow: {ex.Message}");
            }
        }

        // Called by plugin when character switches; rebuilds cache if UI is open
        public void InvalidateSpellCache()
        {
            if (availableSpells != null) availableSpells.Clear();
            if (isWindowVisible)
            {
                try
                {
                    RefreshAvailableSpells();
                    UpdateInputFields();
                    LoadCurrentSettings();
                    if (spellPickerPanel != null && spellPickerPanel.activeSelf)
                    {
                        var filter = spellSearchField != null ? spellSearchField.text : "";
                        PopulateSpellList(filter);
                    }
                }
                catch { }
            }
        }

        private void CreateSpellConfigUI()
        {
            try
            {
                

                if (configPanel != null)
                {
                    
                    return;
                }

                // Ensure an EventSystem exists so inputs are interactable
                EnsureEventSystem();

                // Create canvas
                var canvasGO = new GameObject("SpellConfigCanvas");
                DontDestroyOnLoad(canvasGO);

                uiCanvas = canvasGO.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                uiCanvas.sortingOrder = 1000;

                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasGO.AddComponent<GraphicRaycaster>();

                // Backdrop to dim game UI and block clicks
                backdrop = new GameObject("Backdrop");
                backdrop.transform.SetParent(canvasGO.transform, false);
                var backdropRect = backdrop.AddComponent<RectTransform>();
                backdropRect.anchorMin = Vector2.zero;
                backdropRect.anchorMax = Vector2.one;
                backdropRect.offsetMin = Vector2.zero;
                backdropRect.offsetMax = Vector2.zero;
                var backdropImage = backdrop.AddComponent<Image>();
                backdropImage.color = new Color(0, 0, 0, 0.5f);
                backdropImage.raycastTarget = true;
                var backdropButton = backdrop.AddComponent<Button>();
                backdropButton.transition = Selectable.Transition.None;
                backdropButton.onClick.AddListener(CloseWindow);

                // Create main panel
                configPanel = new GameObject("ConfigPanel");
                configPanel.transform.SetParent(canvasGO.transform, false);

                panelRect = configPanel.AddComponent<RectTransform>();
                panelRect.sizeDelta = new Vector2(500, 400);
                panelRect.anchoredPosition = Vector2.zero;

                var panelImage = configPanel.AddComponent<Image>();
                panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

                CreateSimpleUI();
                CreateLauncherButton(canvasGO.transform);
                CreateSpellPickerUI();

                configPanel.SetActive(false);
                if (backdrop != null) backdrop.SetActive(false);
                if (launcherButton != null) launcherButton.SetActive(true);
                
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SpellConfigUI] Error creating UI: {ex.Message}");
            }
        }

        private void CreateSimpleUI()
        {
            // Title
            CreateLabel("Spell Configuration", new Vector2(0, 150), 22, FontStyle.Bold);

            // Draggable area (title bar)
            var dragGO = new GameObject("DragHandle");
            dragGO.transform.SetParent(configPanel.transform, false);
            var dragRect = dragGO.AddComponent<RectTransform>();
            dragRect.sizeDelta = new Vector2(480, 36);
            dragRect.anchoredPosition = new Vector2(0, 150);
            var dragImg = dragGO.AddComponent<Image>();
            dragImg.color = new Color(1, 1, 1, 0.001f); // nearly invisible but raycastable
            var dragger = dragGO.AddComponent<PanelDragHandler>();
            dragger.Initialize(panelRect);

            // Status text
            var statusGO = new GameObject("Status");
            statusGO.transform.SetParent(configPanel.transform, false);
            var statusRect = statusGO.AddComponent<RectTransform>();
            statusRect.sizeDelta = new Vector2(450, 30);
            statusRect.anchoredPosition = new Vector2(0, 110);
            statusText = statusGO.AddComponent<Text>();
            statusText.text = "Initializing...";
            statusText.fontSize = 14;
            statusText.color = Color.yellow;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Spell input fields with pick buttons
            CreateSpellInput("Left Click Spell:", new Vector2(0, 70), out leftClickInput);
            CreatePickButton(new Vector2(190, 70), leftClickInput);

            CreateSpellInput("Right Click Spell:", new Vector2(0, 30), out rightClickInput);
            CreatePickButton(new Vector2(190, 30), rightClickInput);

            CreateSpellInput("Middle Click Spell:", new Vector2(0, -10), out middleClickInput);
            CreatePickButton(new Vector2(190, -10), middleClickInput);

            // Buttons
            refreshButton = CreateButton("Refresh Spells", new Vector2(0, -60), new Color(0.2f, 0.4f, 0.8f, 1f), RefreshSpells);
            saveButton = CreateButton("Save Settings", new Vector2(-100, -110), new Color(0.2f, 0.8f, 0.2f, 1f), SaveSettings);
            closeButton = CreateButton("Close", new Vector2(100, -110), new Color(0.8f, 0.2f, 0.2f, 1f), CloseWindow);

            // Instructions
            CreateLabel("Use Ctrl+H to open this window\nType spell names for each mouse button", new Vector2(0, -160), 12, FontStyle.Normal);
        }

        private void CreateLabel(string text, Vector2 position, int fontSize, FontStyle fontStyle)
        {
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(configPanel.transform, false);

            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(450, fontSize == 20 ? 30 : 50);
            labelRect.anchoredPosition = position;

            var label = labelGO.AddComponent<Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = fontStyle;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void CreateSpellInput(string labelText, Vector2 position, out InputField inputField)
        {
            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(configPanel.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(150, 25);
            labelRect.anchoredPosition = new Vector2(-150, position.y);

            var label = labelGO.AddComponent<Text>();
            label.text = labelText;
            label.fontSize = 14;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Input Field
            var inputGO = new GameObject("InputField");
            inputGO.transform.SetParent(configPanel.transform, false);

            var inputRect = inputGO.AddComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(250, 25);
            inputRect.anchoredPosition = new Vector2(50, position.y);

            var inputImage = inputGO.AddComponent<Image>();
            inputImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            inputField = inputGO.AddComponent<InputField>();

            // Text area for the input
            var textAreaGO = new GameObject("Text Area");
            textAreaGO.transform.SetParent(inputGO.transform, false);
            var textAreaRect = textAreaGO.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 0);
            textAreaRect.offsetMax = new Vector2(-10, 0);

            // Use RectMask2D for proper clipping without requiring a Graphic
            var rectMask = textAreaGO.AddComponent<RectMask2D>();

            // Input text component
            var inputTextGO = new GameObject("Text");
            inputTextGO.transform.SetParent(textAreaGO.transform, false);
            var inputTextRect = inputTextGO.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.sizeDelta = Vector2.zero;
            inputTextRect.anchoredPosition = Vector2.zero;

            var inputText = inputTextGO.AddComponent<Text>();
            inputText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            inputText.fontSize = 14;
            inputText.color = Color.white;
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.supportRichText = false;

            // Placeholder text
            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(textAreaGO.transform, false);
            var placeholderRect = placeholderGO.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = Vector2.zero;
            placeholderRect.anchoredPosition = Vector2.zero;

            var placeholderText = placeholderGO.AddComponent<Text>();
            placeholderText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            placeholderText.fontSize = 14;
            placeholderText.color = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.text = "Enter spell name...";

            // Assign text components to input field
            inputField.textComponent = inputText;
            inputField.placeholder = placeholderText;
            inputField.targetGraphic = inputImage;

            
        }

        private void CreatePickButton(Vector2 position, InputField targetField)
        {
            var buttonGO = new GameObject("PickButton");
            buttonGO.transform.SetParent(configPanel.transform, false);

            var buttonRect = buttonGO.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(60, 25);
            buttonRect.anchoredPosition = position;

            var image = buttonGO.AddComponent<Image>();
            image.color = new Color(0.35f, 0.35f, 0.35f, 1f);

            var button = buttonGO.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => OpenSpellPickerFor(targetField));

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var txt = textGO.AddComponent<Text>();
            txt.text = "Pick";
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 12;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }

        private void CreateSpellPickerUI()
        {
            // Panel container
            spellPickerPanel = new GameObject("SpellPickerPanel");
            spellPickerPanel.transform.SetParent(configPanel.transform, false);
            var panel = spellPickerPanel.AddComponent<RectTransform>();
            panel.sizeDelta = new Vector2(420, 300);
            panel.anchoredPosition = new Vector2(0, 0);
            var bg = spellPickerPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            // Title
            CreateChildLabel(spellPickerPanel.transform, "Pick a Spell", new Vector2(0, 130), 16, FontStyle.Bold);

            // Close button
            var closeBtn = CreateChildButton(spellPickerPanel.transform, "Close", new Vector2(170, -130), new Vector2(80, 26), new Color(0.6f, 0.2f, 0.2f, 1f), CloseSpellPicker);

            // Search label
            CreateChildLabel(spellPickerPanel.transform, "Search:", new Vector2(-170, 95), 12, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(80, 20));

            // Search input
            var searchGO = new GameObject("SearchInput");
            searchGO.transform.SetParent(spellPickerPanel.transform, false);
            var searchRect = searchGO.AddComponent<RectTransform>();
            searchRect.sizeDelta = new Vector2(280, 24);
            searchRect.anchoredPosition = new Vector2(40, 95);
            var searchImg = searchGO.AddComponent<Image>();
            searchImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            spellSearchField = searchGO.AddComponent<InputField>();

            var saGO = new GameObject("Text Area");
            saGO.transform.SetParent(searchGO.transform, false);
            var saRect = saGO.AddComponent<RectTransform>();
            saRect.anchorMin = Vector2.zero;
            saRect.anchorMax = Vector2.one;
            saRect.offsetMin = new Vector2(8, 0);
            saRect.offsetMax = new Vector2(-8, 0);
            saGO.AddComponent<RectMask2D>();

            var sTextGO = new GameObject("Text");
            sTextGO.transform.SetParent(saGO.transform, false);
            var sTextRect = sTextGO.AddComponent<RectTransform>();
            sTextRect.anchorMin = Vector2.zero;
            sTextRect.anchorMax = Vector2.one;
            sTextRect.sizeDelta = Vector2.zero;
            var sText = sTextGO.AddComponent<Text>();
            sText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            sText.fontSize = 12;
            sText.color = Color.white;
            sText.alignment = TextAnchor.MiddleLeft;

            var sPhGO = new GameObject("Placeholder");
            sPhGO.transform.SetParent(saGO.transform, false);
            var sPhRect = sPhGO.AddComponent<RectTransform>();
            sPhRect.anchorMin = Vector2.zero;
            sPhRect.anchorMax = Vector2.one;
            sPhRect.sizeDelta = Vector2.zero;
            var sPh = sPhGO.AddComponent<Text>();
            sPh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            sPh.fontSize = 12;
            sPh.color = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            sPh.alignment = TextAnchor.MiddleLeft;
            sPh.text = "Type to filter...";
            spellSearchField.textComponent = sText;
            spellSearchField.placeholder = sPh;
            spellSearchField.targetGraphic = searchImg;

            // Scroll area
            var scrollGO = new GameObject("SpellScroll");
            scrollGO.transform.SetParent(spellPickerPanel.transform, false);
            var scrollRect = scrollGO.AddComponent<RectTransform>();
            scrollRect.sizeDelta = new Vector2(380, 220);
            scrollRect.anchoredPosition = new Vector2(0, -10);
            spellScrollRect = scrollGO.AddComponent<ScrollRect>();
            spellScrollRect.horizontal = false;
            spellScrollRect.vertical = true;
            spellScrollRect.movementType = ScrollRect.MovementType.Clamped;
            spellScrollRect.inertia = true;
            spellScrollRect.decelerationRate = 0.05f; // snappy stop
            spellScrollRect.scrollSensitivity = 80f; // faster mouse wheel
            var scrollImg = scrollGO.AddComponent<Image>();
            scrollImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);

            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            spellListViewport = viewportGO.AddComponent<RectTransform>();
            spellListViewport.anchorMin = new Vector2(0, 0);
            spellListViewport.anchorMax = new Vector2(1, 1);
            spellListViewport.offsetMin = new Vector2(0, 0);
            spellListViewport.offsetMax = new Vector2(0, 0);
            viewportGO.AddComponent<RectMask2D>();

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            spellListContent = contentGO.AddComponent<RectTransform>();
            spellListContent.anchorMin = new Vector2(0, 1);
            spellListContent.anchorMax = new Vector2(1, 1);
            spellListContent.pivot = new Vector2(0.5f, 1f);
            spellListContent.offsetMin = new Vector2(0, 0);
            spellListContent.offsetMax = new Vector2(0, 0);
            var layout = contentGO.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            spellScrollRect.viewport = spellListViewport;
            spellScrollRect.content = spellListContent;

            spellPickerPanel.SetActive(false);
            spellSearchField.onValueChanged.AddListener(OnSearchChanged);
        }

        private void OnSearchChanged(string value)
        {
            pendingSearchFilter = value ?? string.Empty;
            CancelInvoke(nameof(ApplySearchFilter));
            Invoke(nameof(ApplySearchFilter), SearchDebounceSeconds);
        }

        private void ApplySearchFilter()
        {
            PopulateSpellList(pendingSearchFilter);
        }

        private void PopulateSpellList(string filter)
        {
            foreach (Transform child in spellListContent)
            {
                Destroy(child.gameObject);
            }

            // Start from all non-empty entries
            IEnumerable<string> spells = availableSpells.Where(s => !string.IsNullOrEmpty(s));

            // If plugin enforces beneficial-only, filter here to present only allowed spells
            if (plugin != null && plugin.RestrictToBeneficialEnabled)
            {
                spells = spells.Where(s => plugin.IsBeneficialSpellName(s));
            }

            bool hasFilter = !string.IsNullOrEmpty(filter);
            if (hasFilter)
            {
                var f = filter.Trim().ToLowerInvariant();
                spells = spells.Where(s => s.ToLowerInvariant().Contains(f));
                // Sort filtered results A-Z (case-insensitive)
                spells = spells.OrderBy(s => s, System.StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // When no filter, keep "None" at the top if present, then A-Z for the rest
                var noneFirst = spells.Any(s => s.Equals("None", System.StringComparison.OrdinalIgnoreCase));
                var sorted = spells
                    .Where(s => !s.Equals("None", System.StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s, System.StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (noneFirst)
                {
                    // Prepend "None"
                    sorted.Insert(0, availableSpells.First(s => s.Equals("None", System.StringComparison.OrdinalIgnoreCase)));
                }
                spells = sorted;
            }

            foreach (var spell in spells)
            {
                var rowGO = new GameObject("SpellRow");
                rowGO.transform.SetParent(spellListContent, false);
                var rowRect = rowGO.AddComponent<RectTransform>();
                rowRect.sizeDelta = new Vector2(0, 28);
                var layoutEl = rowGO.AddComponent<LayoutElement>();
                layoutEl.minHeight = 28f;
                layoutEl.preferredHeight = 28f;
                layoutEl.flexibleWidth = 1f;
                var img = rowGO.AddComponent<Image>();
                img.color = new Color(0.22f, 0.22f, 0.22f, 1f);
                var btn = rowGO.AddComponent<Button>();
                btn.targetGraphic = img;
                string captured = spell;
                btn.onClick.AddListener(() => OnPickSpell(captured));

                var textGO = new GameObject("Text");
                textGO.transform.SetParent(rowGO.transform, false);
                var tRect = textGO.AddComponent<RectTransform>();
                tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
                tRect.offsetMin = new Vector2(8, 0); tRect.offsetMax = new Vector2(-8, 0);
                var t = textGO.AddComponent<Text>();
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                t.fontSize = 12;
                t.color = Color.white;
                t.alignment = TextAnchor.MiddleLeft;
                t.text = string.IsNullOrEmpty(spell) ? "(Empty)" : spell;
            }
        }

        private void OpenSpellPickerFor(InputField target)
        {
            if (target == null) return;
            currentPickerTarget = target;
            // Use cached list; only refresh if empty or not built yet
            if (availableSpells == null || availableSpells.Count == 0)
            {
                RefreshAvailableSpells();
            }
            if (spellSearchField != null) spellSearchField.text = string.Empty;
            PopulateSpellList("");
            spellPickerPanel.SetActive(true);
            spellPickerPanel.transform.SetAsLastSibling();
            if (spellScrollRect != null) spellScrollRect.verticalNormalizedPosition = 1f; // start at top
        }

        private void OnPickSpell(string spell)
        {
            if (currentPickerTarget != null)
            {
                currentPickerTarget.text = spell == "None" ? string.Empty : spell;
            }
            CloseSpellPicker();
        }

        private void CloseSpellPicker()
        {
            if (spellPickerPanel != null)
            {
                spellPickerPanel.SetActive(false);
            }
            currentPickerTarget = null;
        }

        private Button CreateButton(string text, Vector2 position, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var buttonGO = new GameObject("Button");
            buttonGO.transform.SetParent(configPanel.transform, false);

            var buttonRect = buttonGO.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(120, 30);
            buttonRect.anchoredPosition = position;

            var buttonImage = buttonGO.AddComponent<Image>();
            buttonImage.color = color;

            var button = buttonGO.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(onClick);

            // Button text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var buttonText = textGO.AddComponent<Text>();
            buttonText.text = text;
            buttonText.fontSize = 12;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            
            return button;
        }

        private void RefreshAvailableSpells()
        {
            availableSpells.Clear();
            availableSpells.Add("None");
            try
            {
                // Include spells the player currently knows
                var playerCaster = GameData.PlayerControl?.GetComponent<CastSpell>();
                if (playerCaster?.KnownSpells != null)
                {
                    foreach (var spell in playerCaster.KnownSpells)
                    {
                        if (spell != null && !string.IsNullOrEmpty(spell.SpellName))
                        {
                            if (!availableSpells.Contains(spell.SpellName))
                            {
                                availableSpells.Add(spell.SpellName);
                            }
                        }
                    }
                }

                // Also include any loaded Spell assets found in resources
                var allSpells = Resources.FindObjectsOfTypeAll<Spell>();
                foreach (var spell in allSpells)
                {
                    if (spell != null && !string.IsNullOrEmpty(spell.SpellName))
                    {
                        if (!availableSpells.Contains(spell.SpellName))
                        {
                            availableSpells.Add(spell.SpellName);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SpellConfigUI] Error refreshing spells: {ex.Message}");
            }

            // Ensure we have at least some options
            if (availableSpells.Count <= 1)
            {
                availableSpells.AddRange(new[] { "Minor Healing", "Major Healing", "Group Heal" });
            }
        }

        private void UpdateInputFields()
        {
            
            // Input fields don't need option updates like dropdowns
            // Users can type spell names directly
        }

        private void LoadCurrentSettings()
        {
            if (plugin == null) return;

            try
            {
                var leftSpell = plugin.GetSpellForButton(UnityEngine.EventSystems.PointerEventData.InputButton.Left);
                var rightSpell = plugin.GetSpellForButton(UnityEngine.EventSystems.PointerEventData.InputButton.Right);
                var middleSpell = plugin.GetSpellForButton(UnityEngine.EventSystems.PointerEventData.InputButton.Middle);

                SetInputFieldValue(leftClickInput, leftSpell);
                SetInputFieldValue(rightClickInput, rightSpell);
                SetInputFieldValue(middleClickInput, middleSpell);

                
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SpellConfigUI] Error loading settings: {ex.Message}");
            }
        }

        private void SetInputFieldValue(InputField inputField, string value)
        {
            if (inputField == null) return;

            inputField.text = string.IsNullOrEmpty(value) ? "" : value;
            
        }

        private void RefreshSpells()
        {
            
            RefreshAvailableSpells();
            UpdateInputFields();
            UpdateStatusText("Spells refreshed");
        }

        private void SaveSettings()
        {
            try
            {
                string leftSpell = leftClickInput != null ? leftClickInput.text.Trim() : "";
                string rightSpell = rightClickInput != null ? rightClickInput.text.Trim() : "";
                string middleSpell = middleClickInput != null ? middleClickInput.text.Trim() : "";

                // Validate spell names against available spells
                leftSpell = ValidateSpellName(leftSpell);
                rightSpell = ValidateSpellName(rightSpell);
                middleSpell = ValidateSpellName(middleSpell);

                plugin.UpdateSpellBindings(leftSpell, rightSpell, middleSpell);
                UpdateStatusText("Settings saved successfully!");

                
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SpellConfigUI] Error saving: {ex.Message}");
                UpdateStatusText("Error saving settings!");
            }
        }

        private string ValidateSpellName(string spellName)
        {
            if (string.IsNullOrEmpty(spellName))
                return "";

            // Check if the spell exists in our available spells list (case-insensitive)
            foreach (var spell in availableSpells)
            {
                if (spell.Equals(spellName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return spell; // Return the correctly cased version
                }
            }

            
            return spellName; // Return as-is if not found, let the game handle it
        }

        private void CloseWindow()
        {
            isWindowVisible = false;
            if (configPanel != null)
            {
                configPanel.SetActive(false);
            }
            if (backdrop != null)
            {
                backdrop.SetActive(false);
            }
            if (launcherButton != null)
            {
                launcherButton.SetActive(true);
            }
            CloseSpellPicker();
        }

        private void UpdateStatusText(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = message.Contains("Error") ? Color.red :
                                 message.Contains("success") ? Color.green : Color.yellow;
            }
        }

        private void OnDestroy()
        {
            if (uiCanvas != null)
            {
                Destroy(uiCanvas.gameObject);
            }
        }

        private void CreateLauncherButton(Transform parent)
        {
            var go = new GameObject("HealbotLauncherButton");
            go.transform.SetParent(parent, false);
            launcherRect = go.AddComponent<RectTransform>();
            launcherRect.sizeDelta = new Vector2(72, 72);
            launcherRect.anchorMin = new Vector2(1f, 1f); // top-right
            launcherRect.anchorMax = new Vector2(1f, 1f);
            launcherRect.pivot = new Vector2(1f, 1f);
            launcherRect.anchoredPosition = new Vector2(-20f, -20f);

            launcherImage = go.AddComponent<Image>();
            launcherImage.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            launcherImage.raycastTarget = true;
            launcherImage.preserveAspect = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = launcherImage;
            btn.onClick.AddListener(() =>
            {
                ToggleConfigWindow();
            });

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var tRect = textGO.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero; tRect.offsetMax = Vector2.zero;
            launcherText = textGO.AddComponent<Text>();
            launcherText.text = "HB";
            launcherText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            launcherText.fontSize = 14;
            launcherText.alignment = TextAnchor.MiddleCenter;
            launcherText.color = Color.white;

            var drag = go.AddComponent<PanelDragHandler>();
            drag.Initialize(launcherRect);

            launcherButton = go;
            launcherButton.transform.SetAsLastSibling();

            TryApplyLauncherIcon();
        }

        private void TryApplyLauncherIcon()
        {
            if (plugin == null || launcherImage == null) return;
            var path = plugin.GetLauncherIconPath();

            // If no explicit path configured, try defaults:
            // 1) hb.png next to the plugin DLL
            // 2) BepInEx/plugins/healbot/hb.png
            if (string.IsNullOrEmpty(path))
            {
                try
                {
                    var pluginDir = plugin.GetPluginDirectory();
                    if (!string.IsNullOrEmpty(pluginDir))
                    {
                        var sibling = System.IO.Path.Combine(pluginDir, "hb.png");
                        if (System.IO.File.Exists(sibling))
                        {
                            path = sibling;
                        }
                    }
                    if (string.IsNullOrEmpty(path))
                    {
                        var defaultPath = System.IO.Path.Combine(BepInEx.Paths.PluginPath, "healbot", "hb.png");
                        if (System.IO.File.Exists(defaultPath))
                        {
                            path = defaultPath;
                        }
                    }
                }
                catch { }
            }
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                // Resolve relative paths against BepInEx plugins directory
                if (!System.IO.Path.IsPathRooted(path))
                {
                    var baseDir = BepInEx.Paths.PluginPath;
                    path = System.IO.Path.Combine(baseDir, path);
                }
                if (!System.IO.File.Exists(path)) return;

                byte[] data = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(data))
                {
                    var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    launcherImage.sprite = spr;
                    launcherImage.color = Color.white;
                    launcherImage.type = Image.Type.Simple;
                    launcherImage.preserveAspect = true;
                    if (launcherText != null) launcherText.enabled = false; // hide HB text when using an icon
                }
            }
            catch { }
        }

        // Helpers for child UI elements on the picker
        private void CreateChildLabel(Transform parent, string text, Vector2 position, int fontSize, FontStyle fontStyle, TextAnchor anchor = TextAnchor.MiddleCenter, Vector2? size = null)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size ?? new Vector2(380, fontSize == 16 ? 24 : 18);
            rect.anchoredPosition = position;
            var lbl = go.AddComponent<Text>();
            lbl.text = text;
            lbl.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            lbl.fontSize = fontSize;
            lbl.fontStyle = fontStyle;
            lbl.color = Color.white;
            lbl.alignment = anchor;
        }

        private Button CreateChildButton(Transform parent, string text, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var tRect = textGO.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero; tRect.offsetMax = Vector2.zero;
            var t = textGO.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = 12;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;

            return btn;
        }

        private void EnsureEventSystem()
        {
            var es = FindObjectOfType<EventSystem>();
            if (es == null)
            {
                var esGO = new GameObject("HealbotEventSystem");
                es = esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
                // Persist to ensure UI remains usable even if scenes change or game lacks one
                DontDestroyOnLoad(esGO);
            }
        }

        // Simple panel drag handler
        private class PanelDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
        {
            private RectTransform target;
            private Vector2 startPointerLocalPos;
            private Vector2 startAnchoredPos;

            public void Initialize(RectTransform targetRect)
            {
                target = targetRect;
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (target == null) return;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    target.parent as RectTransform,
                    eventData.position,
                    null,
                    out startPointerLocalPos);
                startAnchoredPos = target.anchoredPosition;
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (target == null) return;
                Vector2 currentPointerLocalPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    target.parent as RectTransform,
                    eventData.position,
                    null,
                    out currentPointerLocalPos);
                var offset = currentPointerLocalPos - startPointerLocalPos;
                target.anchoredPosition = startAnchoredPos + offset;
            }
        }
    }
}
