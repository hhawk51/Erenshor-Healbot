using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ErenshorHealbot
{
    public class AutoTargetOverlay : MonoBehaviour
    {
        private HealbotPlugin plugin;
        private Canvas overlayCanvas;
        private RectTransform panelRect;
        private Image backgroundImage;
        private Button interactButton;
        private Text titleText;
        private Text subtitleText;
        private Text healthText;
        private Text statusText;

        private Stats currentTarget;
        private string currentSpell = string.Empty;
        private bool overlayEnabled = true;
        private bool isVisible = true;
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

            currentSpell = SanitizeSpell(plugin?.GetAutoTargetOverlaySpell());
            RefreshBindings();
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
            panelRect.sizeDelta = new Vector2(210f, 76f);
            panelRect.anchoredPosition = new Vector2(-40f, 40f);

            backgroundImage = panelGO.AddComponent<Image>();
            backgroundImage.color = new Color(0.33f, 0.41f, 0.19f, 0.94f);
            backgroundImage.raycastTarget = true;

            interactButton = panelGO.AddComponent<Button>();
            interactButton.targetGraphic = backgroundImage;
            var colors = interactButton.colors;
            colors.normalColor = backgroundImage.color;
            colors.highlightedColor = new Color(0.47f, 0.57f, 0.30f, 0.97f);
            colors.pressedColor = new Color(0.27f, 0.35f, 0.17f, 0.99f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.23f, 0.31f, 0.15f, 0.55f);
            interactButton.colors = colors;

            var interactionHandler = panelGO.AddComponent<OverlayInteractionHandler>();
            interactionHandler.Initialize(this);

            titleText = CreateLabel(panelGO.transform, string.Empty, new Vector2(12f, -12f), 18, FontStyle.Bold);
            subtitleText = CreateLabel(panelGO.transform, string.Empty, new Vector2(12f, -34f), 13, FontStyle.Normal);
            subtitleText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            healthText = CreateLabel(panelGO.transform, string.Empty, new Vector2(12f, -46f), 13, FontStyle.Normal);
            statusText = CreateLabel(panelGO.transform, string.Empty, new Vector2(12f, -58f), 11, FontStyle.Italic);
            statusText.color = new Color(0.75f, 0.75f, 0.75f, 1f);

            panelGO.SetActive(false);
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

        public void SetAnchoredPosition(Vector2 anchoredPosition)
        {
            if (panelRect == null)
                return;

            panelRect.anchoredPosition = anchoredPosition;
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            if (overlayCanvas != null)
                overlayCanvas.enabled = visible;
            if (panelRect != null)
                panelRect.gameObject.SetActive(visible);

            if (visible)
            {
                ShowPanel();
            }
        }

        public void UpdateTarget(Stats target, string displayName, float healthFraction, string spellName)
        {
            suppressedUntil = 0f;
            currentTarget = target;
            currentSpell = SanitizeSpell(spellName);

            if (!overlayEnabled || target == null)
            {
                ClearTarget();
                return;
            }

            subtitleText.text = !string.IsNullOrWhiteSpace(displayName) ? displayName : "Party member";
            titleText.text = "Lowest HP target";
            healthText.text = $"Health {Mathf.Clamp01(healthFraction) * 100f:0}%";
            statusText.text = string.Empty;

            RefreshBindings();
            ShowPanel();
        }

        public void ClearTarget()
        {
            currentTarget = null;
            currentSpell = SanitizeSpell(plugin?.GetAutoTargetOverlaySpell());

            if (panelRect == null)
                return;

            healthText.text = string.Empty;

            if (suppressedUntil > Time.unscaledTime)
            {
                RefreshBindings();
                ShowPanel();
                return;
            }

            suppressedUntil = 0f;
            ShowIdleState();
        }

        public void ShowSuppression(float seconds, string reason)
        {
            currentTarget = null;
            currentSpell = SanitizeSpell(plugin?.GetAutoTargetOverlaySpell());
            if (!overlayEnabled)
            {
                suppressedUntil = 0f;
                ShowIdleState();
                return;
            }

            suppressedUntil = seconds > 0f ? Time.unscaledTime + seconds : Time.unscaledTime;

            titleText.text = string.IsNullOrWhiteSpace(reason) ? "Auto-target paused" : reason;
            subtitleText.text = string.Empty;
            healthText.text = string.Empty;
            statusText.text = seconds > 0f ? $"Waiting {seconds:0.0}s..." : "Waiting...";
            UpdateInteractableState();

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
            RefreshBindings();
        }

        public void RefreshBindings()
        {
            UpdateInteractableState();
        }

        private void ShowIdleState()
        {
            if (panelRect == null)
                return;

            if (string.IsNullOrEmpty(currentSpell))
            {
                currentSpell = SanitizeSpell(plugin?.GetAutoTargetOverlaySpell());
            }

            if (!overlayEnabled)
            {
                titleText.text = "Auto-target disabled";
                subtitleText.text = string.Empty;
                healthText.text = string.Empty;
                statusText.text = string.Empty;
            }
            else
            {
                titleText.text = "Waiting for target";
                subtitleText.text = string.Empty;
                healthText.text = string.Empty;
                statusText.text = string.Empty;
            }

            RefreshBindings();
            ShowPanel();
        }

        private void UpdateInteractableState()
        {
            if (interactButton != null)
            {
                interactButton.interactable = overlayEnabled && HasAnyAvailableSpell();
            }
        }

        private bool HasAnyAvailableSpell()
        {
            if (!string.IsNullOrEmpty(SanitizeSpell(currentSpell)))
                return true;

            if (plugin == null)
                return false;

            var left = plugin.GetOverlayBinding(PointerEventData.InputButton.Left);
            if (!string.IsNullOrEmpty(SanitizeSpell(left.primary)) || !string.IsNullOrEmpty(SanitizeSpell(left.shift)))
                return true;

            var right = plugin.GetOverlayBinding(PointerEventData.InputButton.Right);
            if (!string.IsNullOrEmpty(SanitizeSpell(right.primary)) || !string.IsNullOrEmpty(SanitizeSpell(right.shift)))
                return true;

            var middle = plugin.GetOverlayBinding(PointerEventData.InputButton.Middle);
            if (!string.IsNullOrEmpty(SanitizeSpell(middle.primary)) || !string.IsNullOrEmpty(SanitizeSpell(middle.shift)))
                return true;

            return false;
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

        private void HandlePointerClick(PointerEventData eventData)
        {
            if (!overlayEnabled)
                return;

            if (plugin == null || currentTarget == null)
            {
                statusText.text = "No target to heal.";
                return;
            }

            if (Time.unscaledTime < clickSuppressUntil)
                return;

            if (interactButton != null && !interactButton.interactable)
            {
                statusText.text = "Configure overlay spells in Healbot settings.";
                return;
            }

            string spellName = SanitizeSpell(plugin.GetSpellForButton(eventData.button));
            if (string.IsNullOrEmpty(spellName))
            {
                spellName = SanitizeSpell(currentSpell);
            }

            if (string.IsNullOrEmpty(spellName))
            {
                statusText.text = $"No spell bound for {GetButtonLabel(eventData.button)}.";
                return;
            }

            statusText.text = string.Empty;
            plugin.CastSpellOnTarget(currentTarget, spellName);
        }

        private static string GetButtonLabel(PointerEventData.InputButton button)
        {
            switch (button)
            {
                case PointerEventData.InputButton.Left:
                    return "LMB";
                case PointerEventData.InputButton.Right:
                    return "RMB";
                case PointerEventData.InputButton.Middle:
                    return "MMB";
                default:
                    return button.ToString();
            }
        }

        private static string SanitizeSpell(string spell)
        {
            if (string.IsNullOrWhiteSpace(spell))
                return string.Empty;

            var trimmed = spell.Trim();
            return trimmed.Equals("None", StringComparison.OrdinalIgnoreCase) ? string.Empty : trimmed;
        }

        private void ShowPanel()
        {
            if (panelRect == null)
                return;
            if (!isVisible)
                return;

            panelRect.gameObject.SetActive(true);
        }

        private sealed class OverlayInteractionHandler : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
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

            public void OnPointerClick(PointerEventData eventData)
            {
                owner?.HandlePointerClick(eventData);
            }
        }
    }
}
