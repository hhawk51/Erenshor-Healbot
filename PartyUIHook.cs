using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace ErenshorHealbot
{
    public class PartyUIHook : MonoBehaviour
    {
        private HealbotPlugin plugin;
        private Canvas targetCanvas;
        private EventSystem eventSystem;
        private List<PartyRowHandler> partyRowHandlers = new List<PartyRowHandler>();

        private float scanTimer = 0f;
        private const float SCAN_INTERVAL = 5f; // Scan for party UI every 5 seconds
        private bool hasFoundUI = false;

        // Global click detection for player UI
        private GameObject playerOverlay;
        private RectTransform playerOverlayRect;

        public void Initialize(HealbotPlugin healbotPlugin)
        {
            plugin = healbotPlugin;

            // Ensure EventSystem exists
            EnsureEventSystem();

            // Start scanning for party UI
            ScanForPartyUI();
        }

        private void Update()
        {
            scanTimer += Time.deltaTime;
            if (scanTimer >= SCAN_INTERVAL && !hasFoundUI)
            {
                scanTimer = 0f;
                ScanForPartyUI();
            }

            // Global click detection for player UI
            if (playerOverlay != null && playerOverlayRect != null)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                {
                    Vector3 mousePos = Input.mousePosition;
                    var canvas = playerOverlay.GetComponentInParent<Canvas>();
                    Camera camera = null;
                    if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    {
                        camera = canvas.worldCamera;
                    }

                    bool isOver = RectTransformUtility.RectangleContainsScreenPoint(playerOverlayRect, mousePos, camera);
                    if (isOver)
                    {
                        PointerEventData.InputButton button = PointerEventData.InputButton.Left;
                        if (Input.GetMouseButtonDown(1)) button = PointerEventData.InputButton.Right;
                        else if (Input.GetMouseButtonDown(2)) button = PointerEventData.InputButton.Middle;

                        // Manually trigger the healing
                        string spellName = plugin.GetSpellForButton(button);
                        if (!string.IsNullOrEmpty(spellName) && GameData.PlayerStats != null)
                        {
                            plugin.CastSpellOnTarget(GameData.PlayerStats, spellName);
                        }
                    }
                }
            }
        }

        private void EnsureEventSystem()
        {
            eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                var esGO = new GameObject("HealbotEventSystem");
                eventSystem = esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }
        }

        private void ScanForPartyUI()
        {
            try
            {
                bool foundAnyUI = false;

                // Look for the PlayerLifePar UI element (player row)
                var playerLifePar = GameObject.Find("UI/UIElements/PlayerLifePar");

                // If not found, try alternative searches
                if (playerLifePar == null)
                {
                    playerLifePar = GameObject.Find("PlayerLifePar");

                    if (playerLifePar == null)
                    {
                        var uiRoot = GameObject.Find("UI");
                        if (uiRoot != null)
                        {
                            playerLifePar = FindPlayerLifeParRecursive(uiRoot.transform);
                        }
                    }
                }

                if (playerLifePar != null)
                {
                    SetupPartyUIHooks(playerLifePar);
                    foundAnyUI = true;
                }

                // Look for the NewGroupPar UI element (party member rows)
                var newGroupPar = GameObject.Find("UI/UIElements/NewGroupPar");
                if (newGroupPar != null)
                {
                    SetupPartyUIHooks(newGroupPar);
                    foundAnyUI = true;
                }

                // If neither found, try recursive search
                if (!foundAnyUI)
                {
                    var uiRoot = GameObject.Find("UI");
                    if (uiRoot != null)
                    {
                        var foundElement = FindPlayerLifeParRecursive(uiRoot.transform);
                        if (foundElement != null)
                        {
                            SetupPartyUIHooks(foundElement);
                            foundAnyUI = true;
                        }
                    }
                }

                if (foundAnyUI)
                {
                    hasFoundUI = true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"HealbotPlugin: Error in ScanForPartyUI: {ex.Message}");
            }
        }

        private GameObject FindPlayerLifeParRecursive(Transform parent)
        {
            // Search for PlayerLifePar, NewGroupPar, MemberPar or similar names
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var name = child.name.ToLower();

                if (name.Contains("playerlife") || name.Contains("party") || name.Contains("life") ||
                    name.Contains("newgroup") || name.Contains("member") || name.Contains("group"))
                {
                    return child.gameObject;
                }

                // Recursively search children
                var result = FindPlayerLifeParRecursive(child);
                if (result != null) return result;
            }
            return null;
        }

        private void SetupPartyUIHooks(GameObject playerLifePar)
        {
            // Clear existing handlers
            ClearExistingHandlers();

            // Find all LifeBG elements (party member rows)
            var lifeBGs = FindLifeBGElements(playerLifePar.transform);


            foreach (var lifeBG in lifeBGs)
            {
                SetupPartyRowHook(lifeBG);
            }

            // Ensure Canvas has GraphicRaycaster
            EnsureCanvasRaycaster(playerLifePar);
        }

        private List<GameObject> FindLifeBGElements(Transform parent)
        {
            var lifeBGs = new List<GameObject>();

            // Search recursively for LifeBG or similar elements
            SearchForLifeBG(parent, lifeBGs);

            // Also try looking for specific known patterns
            var playerLifePar = parent.Find("PlayerLifePar");
            if (playerLifePar != null)
            {
                SearchForLifeBG(playerLifePar, lifeBGs);
            }

            // Look for party UI patterns
            var partyPanel = parent.Find("PartyPanel");
            if (partyPanel != null)
            {
                SearchForLifeBG(partyPanel, lifeBGs);
            }

            // Look for NewGroupPar patterns (Member1Par, Member2Par, etc.)
            for (int i = 1; i <= 4; i++)
            {
                var memberPar = parent.Find($"Member{i}Par");
                if (memberPar != null)
                {
                    SearchForLifeBG(memberPar, lifeBGs);
                }
            }

            return lifeBGs;
        }

        private void SearchForLifeBG(Transform parent, List<GameObject> results)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var name = child.name;

                // Be very specific about which elements to target
                // Only target the main LifeBG elements, not nested ones
                if (name == "LifeBG" && HasPartyMemberComponents(child))
                {
                    // Check if this is a primary LifeBG (not a nested duplicate)
                    var path = GetFullPath(child);
                    if (!path.Contains("LifeBG/LifeBG") && !results.Any(r => GetFullPath(r.transform).StartsWith(path)))
                    {
                        results.Add(child.gameObject);
                    }
                }
                // Also check for Member containers that might be direct targets
                else if ((name.StartsWith("Member") && name.Contains("Par")) || name == "PlayerLifePar")
                {
                    if (HasPartyMemberComponents(child))
                    {
                        results.Add(child.gameObject);
                    }
                }

                // Continue searching children
                SearchForLifeBG(child, results);
            }
        }

        private string GetFullPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        private bool HasPartyMemberComponents(Transform element)
        {
            // Check if this element has typical party member UI components
            var hasText = element.GetComponentInChildren<TextMeshProUGUI>() != null ||
                         element.GetComponentInChildren<Text>() != null;
            var hasImage = element.GetComponent<Image>() != null ||
                          element.GetComponentInChildren<Image>() != null;

            return hasText && hasImage;
        }

        private void SetupPartyRowHook(GameObject lifeBG)
        {
            try
            {
                var path = GetFullPath(lifeBG.transform);

                // Skip if this element already has our handler
                var existingLifeBGHandler = lifeBG.GetComponent<PartyRowHandler>();
                if (existingLifeBGHandler != null)
                {
                    return;
                }

                // Use the same overlay approach for both player and party members
                var clickOverlay = new GameObject("HealbotClickOverlay");
                clickOverlay.transform.SetParent(lifeBG.transform, false);

                var overlayRect = clickOverlay.AddComponent<RectTransform>();
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                overlayRect.sizeDelta = Vector2.zero;

                var overlayImage = clickOverlay.AddComponent<Image>();
                if (path.Contains("PlayerLifePar"))
                {
                    overlayImage.color = new Color(0, 1, 0, 0.05f); // Subtle green for player

                    // Store references for global click detection
                    playerOverlay = clickOverlay;
                    playerOverlayRect = overlayRect;
                }
                else
                {
                    overlayImage.color = new Color(1, 0, 0, 0.05f); // Subtle red for party members
                }
                overlayImage.raycastTarget = true;
                overlayImage.maskable = false;

                // Remove any existing PartyRowHandler components to avoid conflicts
                var existingHandlers = clickOverlay.GetComponents<PartyRowHandler>();
                for (int i = 0; i < existingHandlers.Length; i++)
                {
                    Destroy(existingHandlers[i]);
                }

                try
                {
                    var overlayHandler = clickOverlay.AddComponent<PartyRowHandler>();
                    overlayHandler.Initialize(plugin, lifeBG);
                    partyRowHandlers.Add(overlayHandler);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to add PartyRowHandler: {ex.Message}");
                }

                clickOverlay.transform.SetAsLastSibling();

                // Disable raycast targets for player UI to prevent conflicts with global detection
                if (path.Contains("PlayerLifePar"))
                {
                    var allImages = lifeBG.GetComponentsInChildren<Image>();
                    for (int i = 0; i < allImages.Length; i++)
                    {
                        var img = allImages[i];
                        if (img.gameObject.name != "HealbotClickOverlay" && img.raycastTarget)
                        {
                            img.raycastTarget = false;
                        }
                    }
                }

            }
            catch
            {
            }
        }

        private void EnsureCanvasRaycaster(GameObject uiElement)
        {
            var canvas = uiElement.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                {
                    raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
                }
                targetCanvas = canvas;
            }
        }

        private void ClearExistingHandlers()
        {
            foreach (var handler in partyRowHandlers)
            {
                if (handler != null)
                {
                    Destroy(handler);
                }
            }
            partyRowHandlers.Clear();

            // Also clean up any old TestClickHandler components from previous versions
            try
            {
                var allGameObjects = FindObjectsOfType<GameObject>();
                foreach (var go in allGameObjects)
                {
                    // Use reflection to check for TestClickHandler components and remove them
                    var components = go.GetComponents<MonoBehaviour>();
                    foreach (var comp in components)
                    {
                        if (comp != null && comp.GetType().Name == "TestClickHandler")
                        {
                            Debug.Log($"Removing old TestClickHandler from {go.name}");
                            Destroy(comp);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        private void OnDestroy()
        {
            ClearExistingHandlers();
        }
    }

    public class PartyRowHandler : MonoBehaviour, IPointerClickHandler
    {
        private HealbotPlugin plugin;
        private Stats targetStats;
        private GameObject rowGameObject;

        public void Initialize(HealbotPlugin healbotPlugin, GameObject lifeBG)
        {
            try
            {
                plugin = healbotPlugin;
                rowGameObject = lifeBG;
                FindTargetStats();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Exception in PartyRowHandler.Initialize: {ex.Message}");
            }
        }

        private void FindTargetStats()
        {
            try
            {
                var path = GetFullPath(rowGameObject.transform);

                // Method 1: If this is the player row, use GameData.PlayerStats
                if (IsPlayerRow() || path.Contains("PlayerLifePar"))
                {
                    targetStats = GameData.PlayerStats;
                    return;
                }

                // Method 2: For party members, extract member index from path
                if (path.Contains("Member") && path.Contains("Par"))
                {
                    int memberIndex = ExtractMemberIndex(path);
                    if (memberIndex >= 0 && memberIndex < GameData.GroupMembers.Length)
                    {
                        var tracking = GameData.GroupMembers[memberIndex];
                        if (tracking?.MyAvatar?.MyStats != null)
                        {
                            targetStats = tracking.MyAvatar.MyStats;
                            return;
                        }
                    }
                }

                // Method 3: Try name matching as fallback
                var nameText = GetNameFromUI();
                if (!string.IsNullOrEmpty(nameText))
                {
                    targetStats = FindStatsByName(nameText);
                    if (targetStats != null)
                    {
                        return;
                    }
                }

            }
            catch
            {
            }
        }

        private int ExtractMemberIndex(string path)
        {
            // Extract member number from paths like "Member1Par", "Member2Par", etc.
            if (path.Contains("Member1Par")) return 0;
            if (path.Contains("Member2Par")) return 1;
            if (path.Contains("Member3Par")) return 2;
            if (path.Contains("Member4Par")) return 3;
            return -1;
        }

        private string GetNameFromUI()
        {
            // Look for TextMeshProUGUI or Text components with names
            var tmpTexts = rowGameObject.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var tmp in tmpTexts)
            {
                if (!string.IsNullOrEmpty(tmp.text) && !tmp.text.Contains("/") && !tmp.text.Contains("%"))
                {
                    return tmp.text.Trim();
                }
            }

            var texts = rowGameObject.GetComponentsInChildren<Text>();
            foreach (var text in texts)
            {
                if (!string.IsNullOrEmpty(text.text) && !text.text.Contains("/") && !text.text.Contains("%"))
                {
                    return text.text.Trim();
                }
            }

            return null;
        }

        private bool IsPlayerRow()
        {
            // Check if this row represents the player
            var name = rowGameObject.name.ToLower();
            return name.Contains("player") || name.Contains("self") || name.Contains("main");
        }

        private Stats FindStatsByName(string name)
        {
            // Check player
            if (GameData.PlayerStats != null &&
                !string.IsNullOrEmpty(GameData.PlayerStats.MyName) &&
                GameData.PlayerStats.MyName.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                return GameData.PlayerStats;
            }

            // Check party members
            for (int i = 0; i < GameData.GroupMembers.Length; i++)
            {
                var tracking = GameData.GroupMembers[i];
                if (tracking?.MyAvatar?.MyStats != null)
                {
                    var stats = tracking.MyAvatar.MyStats;
                    if (!string.IsNullOrEmpty(stats.MyName) &&
                        stats.MyName.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return stats;
                    }
                }
            }

            return null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var path = GetFullPath(transform);

            // Force player target for player UI
            if (path.Contains("PlayerLifePar") && targetStats == null && GameData.PlayerStats != null)
            {
                targetStats = GameData.PlayerStats;
            }

            if (targetStats == null || plugin == null)
            {
                return;
            }

            string spellName = plugin.GetSpellForButton(eventData.button);
            if (!string.IsNullOrEmpty(spellName))
            {
                plugin.CastSpellOnTarget(targetStats, spellName);
            }
        }

        private string GetFullPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        private void Update()
        {
            // Periodically re-scan for target stats if missing
            if (targetStats == null)
            {
                FindTargetStats();
            }

        }
    }

}