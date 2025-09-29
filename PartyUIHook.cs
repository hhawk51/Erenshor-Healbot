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
        private List<PetRowHandler> petRowHandlers = new List<PetRowHandler>();

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
            if (scanTimer >= SCAN_INTERVAL)
            {
                scanTimer = 0f;

                // Always rescan for pet UI since pets can be summoned/dismissed
                // But only rescan party UI if we haven't found it yet
                if (!hasFoundUI)
                {
                    ScanForPartyUI();
                }
                else
                {
                    // Just check for pets if party UI is already found
                    ScanForPetUI();
                }
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

        public void ForceRescanForCharacterSwitch()
        {
            hasFoundUI = false; // Reset so we rescan
            ScanForPartyUI();
        }

        public void ScanForPetUI()
        {
            try
            {
                // Look for pet UI elements at CharmedPar/CharmedNPC/LifeBG/
                var charmedPar = GameObject.Find("UI/UIElements/CharmedPar");
                if (charmedPar != null)
                {
                    // Check if CharmedNPC container is now active (pet summoned)
                    var charmedNPC = charmedPar.transform.Find("CharmedNPC");
                    if (charmedNPC != null && charmedNPC.gameObject.activeSelf)
                    {
                        SetupPetUIHooks(charmedPar);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"HealbotPlugin: Error in ScanForPetUI: {ex.Message}");
            }
        }

        public void ScanForPartyUI()
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

                // Look for pet UI elements at CharmedPar/CharmedNPC/LifeBG/
                var charmedPar = GameObject.Find("UI/UIElements/CharmedPar");
                if (charmedPar != null)
                {
                    SetupPetUIHooks(charmedPar);
                    foundAnyUI = true;
                }
                else
                {
                    // Try alternative pet UI searches
                    var altCharmedPar = GameObject.Find("CharmedPar");
                    if (altCharmedPar != null)
                    {
                        SetupPetUIHooks(altCharmedPar);
                        foundAnyUI = true;
                    }
                    else
                    {
                        // Try recursive search for pet UI
                        var uiRoot = GameObject.Find("UI");
                        if (uiRoot != null)
                        {
                            var foundPetUI = FindPetUIRecursive(uiRoot.transform);
                            if (foundPetUI != null)
                            {
                                SetupPetUIHooks(foundPetUI);
                                foundAnyUI = true;
                            }
                        }
                    }
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

        private GameObject FindPetUIRecursive(Transform parent)
        {
            // Search for CharmedPar, CharmedNPC, or pet-related UI elements
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var name = child.name.ToLower();

                if (name.Contains("charmed") || name.Contains("pet") || name.Contains("companion"))
                {
                    return child.gameObject;
                }

                // Recursively search children
                var result = FindPetUIRecursive(child);
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

        private void SetupPetUIHooks(GameObject charmedPar)
        {
            try
            {
                // Find pet LifeBG elements under CharmedPar/CharmedNPC/LifeBG/
                var petLifeBGs = FindPetLifeBGElements(charmedPar.transform);

                foreach (var petLifeBG in petLifeBGs)
                {
                    SetupPetRowHook(petLifeBG);
                }

                // Ensure Canvas has GraphicRaycaster
                EnsureCanvasRaycaster(charmedPar);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"HealbotPlugin: Error in SetupPetUIHooks: {ex.Message}");
            }
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

        private List<GameObject> FindPetLifeBGElements(Transform charmedPar)
        {
            var petLifeBGs = new List<GameObject>();

            try
            {
                // Search for CharmedNPC containers under CharmedPar
                SearchForPetLifeBG(charmedPar, petLifeBGs);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"HealbotPlugin: Error in FindPetLifeBGElements: {ex.Message}");
            }

            return petLifeBGs;
        }

        private void SearchForPetLifeBG(Transform parent, List<GameObject> results)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var name = child.name;

                // Look for CharmedNPC containers or LifeBG elements
                if (name == "CharmedNPC" || name.Contains("CharmedNPC"))
                {
                    // Only process if CharmedNPC is active (pet is summoned)
                    if (child.gameObject.activeSelf)
                    {
                        SearchForPetLifeBG(child, results);
                    }
                }
                else if (name == "LifeBG" && HasPetComponents(child))
                {
                    var path = GetFullPath(child);
                    if (path.Contains("CharmedPar") && !results.Any(r => GetFullPath(r.transform).StartsWith(path)))
                    {
                        results.Add(child.gameObject);
                    }
                }

                // Continue searching children recursively only if parent is active
                if (child.gameObject.activeSelf)
                {
                    SearchForPetLifeBG(child, results);
                }
            }
        }


        private bool HasPetComponents(Transform element)
        {
            // For pets, we only need an Image component (text might be in sibling elements)
            var hasImage = element.GetComponent<Image>() != null ||
                          element.GetComponentInChildren<Image>() != null;

            // Pet LifeBG might not have text directly, so be more lenient
            return hasImage;
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
                    overlayImage.color = new Color(0, 1, 0, 0f); // Fully transparent for player

                    // Store references for global click detection
                    playerOverlay = clickOverlay;
                    playerOverlayRect = overlayRect;
                }
                else
                {
                    overlayImage.color = new Color(1, 0, 0, 0f); // Fully transparent for party members
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

        private void SetupPetRowHook(GameObject petLifeBG)
        {
            try
            {
                // Skip if this element already has our handler
                var existingPetHandler = petLifeBG.GetComponent<PetRowHandler>();
                if (existingPetHandler != null)
                {
                    return;
                }

                // Create click overlay similar to party members
                var clickOverlay = new GameObject("HealbotPetClickOverlay");
                clickOverlay.transform.SetParent(petLifeBG.transform, false);

                var overlayRect = clickOverlay.AddComponent<RectTransform>();
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                overlayRect.sizeDelta = Vector2.zero;

                var overlayImage = clickOverlay.AddComponent<Image>();
                overlayImage.color = new Color(0, 0, 1, 0f); // Blue, fully transparent for pets
                overlayImage.raycastTarget = true;
                overlayImage.maskable = false;

                // Remove any existing PetRowHandler components to avoid conflicts
                var existingHandlers = clickOverlay.GetComponents<PetRowHandler>();
                for (int i = 0; i < existingHandlers.Length; i++)
                {
                    Destroy(existingHandlers[i]);
                }

                try
                {
                    var overlayHandler = clickOverlay.AddComponent<PetRowHandler>();
                    overlayHandler.Initialize(plugin, petLifeBG);
                    petRowHandlers.Add(overlayHandler);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"HealbotPlugin: Failed to add PetRowHandler: {ex.Message}");
                }

                clickOverlay.transform.SetAsLastSibling();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"HealbotPlugin: Error in SetupPetRowHook: {ex.Message}");
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

            foreach (var handler in petRowHandlers)
            {
                if (handler != null)
                {
                    Destroy(handler);
                }
            }
            petRowHandlers.Clear();

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
                            Destroy(comp);
                        }
                    }
                    // keep existing overlays
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

    public class PetRowHandler : MonoBehaviour, IPointerClickHandler
    {
        private HealbotPlugin plugin;
        private Stats targetStats;
        private GameObject rowGameObject;

        public void Initialize(HealbotPlugin healbotPlugin, GameObject petLifeBG)
        {
            try
            {
                plugin = healbotPlugin;
                rowGameObject = petLifeBG;
                FindPetTargetStats();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"HealbotPlugin: Exception in PetRowHandler.Initialize: {ex.Message}");
            }
        }

        private void FindPetTargetStats()
        {
            try
            {
                // Method 1: Try to find pet stats through game data exploration
                targetStats = FindPetStatsByExploration();
                if (targetStats != null)
                {
                    return;
                }

                // Method 2: Try name matching as fallback
                var nameText = GetPetNameFromUI();
                if (!string.IsNullOrEmpty(nameText))
                {
                    targetStats = FindStatsByName(nameText);
                    if (targetStats != null)
                    {
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"HealbotPlugin: Error in FindPetTargetStats: {ex.Message}");
            }
        }

        private Stats FindPetStatsByExploration()
        {
            try
            {
                // Based on CharmedNPC code, pet stats are at:
                // GameData.PlayerControl.Myself.MyCharmedNPC.GetComponent<Stats>()

                if (GameData.PlayerControl?.Myself?.MyCharmedNPC != null)
                {
                    var charmedNPC = GameData.PlayerControl.Myself.MyCharmedNPC;
                    var charmedStats = charmedNPC.GetComponent<Stats>();

                    if (charmedStats != null)
                    {
                        return charmedStats;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"HealbotPlugin: Error in FindPetStatsByExploration: {ex.Message}");
            }

            return null;
        }


        private string GetPetNameFromUI()
        {
            // Look for pet name in parent CharmedNPC container (based on UI structure logs)
            var charmedNPCParent = rowGameObject.transform.parent;
            while (charmedNPCParent != null && charmedNPCParent.name != "CharmedNPC")
            {
                charmedNPCParent = charmedNPCParent.parent;
            }

            if (charmedNPCParent != null)
            {
                // Look for TargetName specifically
                var targetNameText = charmedNPCParent.Find("Image (2)/TargetName");
                if (targetNameText != null)
                {
                    var tmpText = targetNameText.GetComponent<TextMeshProUGUI>();
                    if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                    {
                        return tmpText.text.Trim();
                    }
                }

                // Fallback to any text under CharmedNPC
                var tmpTexts = charmedNPCParent.GetComponentsInChildren<TextMeshProUGUI>();
                foreach (var tmp in tmpTexts)
                {
                    if (!string.IsNullOrEmpty(tmp.text) &&
                        !tmp.text.Contains("/") &&
                        !tmp.text.Contains("%") &&
                        !tmp.text.Contains("Attack") &&
                        !tmp.text.Contains("Back") &&
                        !tmp.text.Contains("Break") &&
                        tmp.name.Contains("TargetName"))
                    {
                        return tmp.text.Trim();
                    }
                }
            }

            return null;
        }

        private Stats FindStatsByName(string name)
        {
            // First check the known charmed NPC path
            try
            {
                if (GameData.PlayerControl?.Myself?.MyCharmedNPC != null)
                {
                    var charmedStats = GameData.PlayerControl.Myself.MyCharmedNPC.GetComponent<Stats>();
                    if (charmedStats != null && !string.IsNullOrEmpty(charmedStats.MyName) &&
                        charmedStats.MyName.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return charmedStats;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"HealbotPlugin: Error checking charmed NPC by name: {ex.Message}");
            }

            // Fallback to searching all Stats objects
            try
            {
                var allStats = Resources.FindObjectsOfTypeAll<Stats>();
                foreach (var stats in allStats)
                {
                    if (!string.IsNullOrEmpty(stats.MyName) &&
                        stats.MyName.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return stats;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"HealbotPlugin: Error in FindStatsByName: {ex.Message}");
            }

            return null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (targetStats == null)
            {
                // Try to find stats again
                FindPetTargetStats();
                if (targetStats == null)
                {
                    return;
                }
            }

            if (plugin == null)
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
                FindPetTargetStats();
            }
        }
    }

}
