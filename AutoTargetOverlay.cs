using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ErenshorHealbot
{
    public class AutoTargetOverlay : MonoBehaviour
    {
        private HealbotPlugin plugin;
        private Canvas overlayCanvas;
        private RectTransform panelRect;
        private Text headingText;
        private Text targetNameText;
        private Text healthText;
        private Text spellText;
        private Text statusText;
        private Button healButton;

        private Stats currentTarget;
        private string currentSpell = string.Empty;
        private bool overlayEnabled = true;
        private float suppressedUntil = 0f;
        private Vector2 dragStartPointer;
        private Vector2 dragStartAnchored;
        private bool isDragging;
        private float clickSuppressUntil;

        public void Initialize(HealbotPlugin healbotPlugin)
        {
            plugin = healbotPlugin;
            if (overlayCanvas == null)
            {
                CreateUI();
            }
            ShowIdleState();
        }

        private void CreateUI()
        {
            overlayCanvas = gameObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 1500;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            gameObject.AddComponent<GraphicRaycaster>();

            var panelGO = new GameObject("AutoTargetPanel");
            panelGO.transform.SetParent(transform, false);
            panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.sizeDelta = new Vector2(260f, 110f);
            panelRect.anchoredPosition = new Vector2(-40f, 40f);

            var background = panelGO.AddComponent<Image>();
            background.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            background.raycastTarget = true;

            var dragHandler = panelGO.AddComponent<OverlayDragHandler>();
            dragHandler.Initialize(this);

            healButton = panelGO.AddComponent<Button>();
            healButton.targetGraphic = background;
            var colors = healButton.colors;
            colors.normalColor = background.color;
            colors.highlightedColor = new Color(0.15f, 0.15f, 0.15f, 0.92f);
            colors.pressedColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.08f, 0.08f, 0.08f, 0.4f);
            healButton.colors = colors;
            healButton.onClick.AddListener(OnHealButtonClicked);

            headingText = CreateLabel(panelGO.transform, "Auto Target", new Vector2(12f, -12f), 16, FontStyle.Bold);
            targetNameText = CreateLabel(panelGO.transform, string.Empty, new Vector2(12f, -34f), 18, FontStyle.Bold);
            healthText = CreateLabel(panelGO.transform, string.Empty, new Vector2(12f, -56f), 14, FontStyle.Normal);
            spellText = CreateLabel(panelGO.transform, string.Empty, new Vector2(12f, -76f), 14, FontStyle.Normal);
            statusText = CreateLabel(panelGO.transform, string.Empty, new Vector2(12f, -96f), 12, FontStyle.Italic);

            panelGO.SetActive(false);
        }

        public void SetAnchoredPosition(Vector2 anchoredPosition)
        {
            if (panelRect == null)
                return;

            panelRect.anchoredPosition = anchoredPosition;
        }

        private Text CreateLabel(Transform parent, string initialText, Vector2 offset, int fontSize, FontStyle fontStyle)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(-24f, 20f);

            var text = go.AddComponent<Text>();
            text.text = initialText;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;
            return text;
        }

        private void ShowIdleState()
        {
            if (panelRect == null)
                return;

            var idleSpell = plugin != null ? plugin.GetAutoTargetOverlaySpell() : string.Empty;
            bool hasSpell = overlayEnabled && !string.IsNullOrWhiteSpace(idleSpell) && !idleSpell.Equals("None", StringComparison.OrdinalIgnoreCase);

            headingText.text = "Auto Target";
            targetNameText.text = overlayEnabled ? "Waiting for target" : "Auto-target disabled";
            healthText.text = string.Empty;
            spellText.text = hasSpell ? $"Click to cast {idleSpell}" : (overlayEnabled ? "Monitoring party..." : "Auto-target disabled");
            statusText.text = string.Empty;
            healButton.interactable = hasSpell;

            ShowPanel();
        }

        private void Update()
        {
            if (suppressedUntil > 0f)
            {
                var remaining = suppressedUntil - Time.unscaledTime;
                if (remaining <= 0f)
                {
                    suppressedUntil = 0f;
                    statusText.text = string.Empty;
                    if (currentTarget == null)
                    {
                        ShowIdleState();
                    }
                }
                else
                {
                    statusText.text = $"Waiting {remaining:0.0}s...";
                }
            }
        }

        public void UpdateTarget(Stats target, string displayName, float healthFraction, string spellName)
        {
            suppressedUntil = 0f;
            currentTarget = target;
            currentSpell = spellName ?? string.Empty;

            if (!overlayEnabled || target == null)
            {
                ClearTarget();
                return;
            }

            headingText.text = "Auto Target";
            targetNameText.text = string.IsNullOrWhiteSpace(displayName) ? "Unknown target" : displayName;
            healthText.text = $"Health {Mathf.Clamp01(healthFraction) * 100f:0}%";

            bool hasSpell = !string.IsNullOrWhiteSpace(currentSpell) && !currentSpell.Equals("None", StringComparison.OrdinalIgnoreCase);
            spellText.text = hasSpell ? $"Click to cast {currentSpell}" : "Configure a heal spell for auto-target";
            statusText.text = string.Empty;
            healButton.interactable = hasSpell;

            ShowPanel();
        }

        public void ClearTarget()
        {
            currentTarget = null;
            currentSpell = string.Empty;

            if (panelRect == null)
                return;

            healButton.interactable = false;

            if (suppressedUntil > Time.unscaledTime)
            {
                healthText.text = string.Empty;
                spellText.text = string.Empty;
                ShowPanel();
                return;
            }

            suppressedUntil = 0f;
            ShowIdleState();
        }

        public void ShowSuppression(float seconds, string reason)
        {
            currentTarget = null;
            currentSpell = string.Empty;
            if (!overlayEnabled)
            {
                suppressedUntil = 0f;
                ShowIdleState();
                return;
            }

            suppressedUntil = seconds > 0f ? Time.unscaledTime + seconds : Time.unscaledTime;

            headingText.text = "Auto Target";
            targetNameText.text = string.IsNullOrWhiteSpace(reason) ? "Auto-target paused" : reason;
            healthText.text = string.Empty;
            spellText.text = string.Empty;
            statusText.text = seconds > 0f ? $"Waiting {seconds:0.0}s..." : "Waiting...";
            healButton.interactable = false;

            ShowPanel();
        }

        public void OnAutoTargetToggle(bool enabled)
        {
            overlayEnabled = enabled;
            if (!enabled)
            {
                suppressedUntil = 0f;
            }

            ClearTarget();
        }

        private void OnHealButtonClicked()
        {
            if (!overlayEnabled)
                return;
            if (currentTarget == null)
                return;
            if (Time.unscaledTime < clickSuppressUntil)
                return;
            if (string.IsNullOrWhiteSpace(currentSpell) || currentSpell.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                statusText.text = "No heal spell configured.";
                healButton.interactable = false;
                return;
            }

            plugin?.CastSpellOnTarget(currentTarget, currentSpell);
        }

        private void ShowPanel()
        {
            if (panelRect == null)
                return;

            panelRect.gameObject.SetActive(true);
        }

        private void HandlePointerDown(PointerEventData eventData)
        {
            if (panelRect == null)
                return;

            dragStartPointer = eventData.position;
            dragStartAnchored = panelRect.anchoredPosition;
            isDragging = false;
        }

        private void HandleBeginDrag(PointerEventData eventData)
        {
            HandlePointerDown(eventData);
        }

        private void HandleDrag(PointerEventData eventData)
        {
            if (panelRect == null)
                return;

            float scaleFactor = overlayCanvas != null && !Mathf.Approximately(overlayCanvas.scaleFactor, 0f)
                ? overlayCanvas.scaleFactor
                : 1f;

            panelRect.anchoredPosition += eventData.delta / scaleFactor;

            if (!isDragging)
            {
                var totalDelta = eventData.position - dragStartPointer;
                if (totalDelta.sqrMagnitude >= 4f)
                {
                    isDragging = true;
                }
            }
        }

        private void HandleEndDrag(PointerEventData eventData)
        {
            if (panelRect == null)
                return;

            if (isDragging)
            {
                plugin?.SaveOverlayPos(panelRect.anchoredPosition);
                clickSuppressUntil = Time.unscaledTime + 0.1f;
            }
            else
            {
                panelRect.anchoredPosition = dragStartAnchored;
            }

            isDragging = false;
        }

        private sealed class OverlayDragHandler : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private AutoTargetOverlay owner;

            public void Initialize(AutoTargetOverlay overlay)
            {
                owner = overlay;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                owner?.HandlePointerDown(eventData);
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                owner?.HandleBeginDrag(eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                owner?.HandleDrag(eventData);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                owner?.HandleEndDrag(eventData);
            }
        }

    }
}








