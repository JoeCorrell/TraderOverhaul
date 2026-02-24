using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TraderOverhaul
{
    public class BankUI : MonoBehaviour
    {
        // ── State ──
        private bool _isVisible;
        private bool _uiBuilt;
        private int _bankBalance;

        // ── UI Root ──
        private GameObject _canvasGO;
        private GameObject _mainPanel;

        // ── Text displays ──
        private TMP_Text _titleText;
        private TMP_Text _bankBalanceText;
        private TMP_Text _inventoryCoinsText;
        private TMP_Text _totalWealthText;
        private TMP_Text _statusText;

        // ── Buttons ──
        private Button _depositButton;
        private TMP_Text _depositLabel;
        private GameObject _depositSelected;
        private Button _withdrawButton;
        private TMP_Text _withdrawLabel;
        private GameObject _withdrawSelected;

        // ── Extracted assets ──
        private Sprite _bgSprite;
        private TMP_FontAsset _valheimFont;
        private GameObject _buttonTemplate;
        private float _btnHeight = 36f;

        // ── Gamepad ──
        private int _focusedButton; // 0=deposit, 1=withdraw

        // ── Colors ──
        static readonly Color ColOverlay = new Color(0f, 0f, 0f, 0.65f);
        static readonly Color GoldTextColor = new Color(0.83f, 0.52f, 0.18f, 1f);
        static readonly Color PanelBgColor = new Color(0.22f, 0.10f, 0.04f, 0.65f);
        static readonly Color SelectedColor = new Color(1f, 0.82f, 0.24f, 0.25f);

        // ── Constants ──
        const float PanelWidth = 420f;
        const float PanelHeight = 320f;

        // ── Public API ──
        public bool IsVisible => _isVisible;

        public void Show()
        {
            if (!_uiBuilt) BuildUI();
            if (!_uiBuilt) return;

            LoadBalance();
            _canvasGO.SetActive(true);
            _isVisible = true;
            _focusedButton = 0;
            RefreshDisplay();
            UpdateControllerHighlight();
        }

        public void Hide()
        {
            _isVisible = false;
            if (_canvasGO != null) _canvasGO.SetActive(false);
        }

        // ══════════════════════════════════════════
        //  UPDATE
        // ══════════════════════════════════════════

        private void Update()
        {
            if (!_isVisible) return;

            // Close on Escape
            if (Input.GetKeyDown(KeyCode.Escape)) { Hide(); return; }

            // Gamepad input
            UpdateGamepadInput();

            // Keep display fresh
            RefreshDisplay();
        }

        private void LateUpdate()
        {
            if (!_isVisible) return;

            // Cursor — run in LateUpdate so we overwrite GameCamera's lock each frame
            if (!ZInput.IsGamepadActive())
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.visible = false;
            }

            // Hide crosshair and hover text
            var hud = Hud.instance;
            if (hud != null)
            {
                if (hud.m_crosshair != null) hud.m_crosshair.color = Color.clear;
                if (hud.m_hoverName != null) hud.m_hoverName.text = "";
            }

            // On gamepad, clear EventSystem so Unity's built-in nav doesn't interfere
            if (ZInput.IsGamepadActive() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void UpdateGamepadInput()
        {
            // B = close
            if (ZInput.GetButtonDown("JoyButtonB")) { Hide(); return; }

            // D-Pad / stick left-right or up-down to switch buttons
            if (ZInput.GetButtonDown("JoyLStickLeft") || ZInput.GetButtonDown("JoyDPadLeft") ||
                ZInput.GetButtonDown("JoyLStickUp") || ZInput.GetButtonDown("JoyDPadUp"))
            {
                _focusedButton = 0;
                UpdateControllerHighlight();
            }
            if (ZInput.GetButtonDown("JoyLStickRight") || ZInput.GetButtonDown("JoyDPadRight") ||
                ZInput.GetButtonDown("JoyLStickDown") || ZInput.GetButtonDown("JoyDPadDown"))
            {
                _focusedButton = 1;
                UpdateControllerHighlight();
            }

            // A = activate focused button
            if (ZInput.GetButtonDown("JoyButtonA"))
            {
                if (_focusedButton == 0 && _depositButton != null && _depositButton.interactable)
                    OnDeposit();
                else if (_focusedButton == 1 && _withdrawButton != null && _withdrawButton.interactable)
                    OnWithdraw();
            }
        }

        private void UpdateControllerHighlight()
        {
            bool gamepad = ZInput.IsGamepadActive();
            if (_depositSelected != null) _depositSelected.SetActive(gamepad && _focusedButton == 0);
            if (_withdrawSelected != null) _withdrawSelected.SetActive(gamepad && _focusedButton == 1);
        }

        // ══════════════════════════════════════════
        //  BUILD UI
        // ══════════════════════════════════════════

        private void BuildUI()
        {
            if (!ExtractAssets())
            {
                TraderOverhaulPlugin.Log.LogError("[BankUI] Failed to extract Valheim assets.");
                return;
            }

            // ── Canvas ──
            _canvasGO = new GameObject("HaldorBank_Canvas");
            _canvasGO.transform.SetParent(transform);
            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;
            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasGO.AddComponent<GraphicRaycaster>();

            // ── Fullscreen overlay ──
            var overlay = new GameObject("Overlay", typeof(RectTransform));
            overlay.transform.SetParent(_canvasGO.transform, false);
            var oRT = overlay.GetComponent<RectTransform>();
            oRT.anchorMin = Vector2.zero; oRT.anchorMax = Vector2.one;
            oRT.offsetMin = Vector2.zero; oRT.offsetMax = Vector2.zero;
            overlay.AddComponent<Image>().color = ColOverlay;

            // ── Main panel ──
            _mainPanel = new GameObject("BankPanel", typeof(RectTransform), typeof(Image));
            _mainPanel.transform.SetParent(_canvasGO.transform, false);
            var mainRT = _mainPanel.GetComponent<RectTransform>();
            mainRT.anchorMin = new Vector2(0.5f, 0.5f);
            mainRT.anchorMax = new Vector2(0.5f, 0.5f);
            mainRT.pivot = new Vector2(0.5f, 0.5f);
            mainRT.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            mainRT.anchoredPosition = Vector2.zero;
            var mainImg = _mainPanel.GetComponent<Image>();
            if (_bgSprite != null)
            {
                mainImg.sprite = _bgSprite;
                mainImg.type = Image.Type.Simple;
                mainImg.preserveAspect = false;
                mainImg.color = Color.white;
            }
            else
            {
                mainImg.color = new Color(0.18f, 0.14f, 0.09f, 0.92f);
            }

            // ── Inner content panel (dark inset) ──
            var innerPanel = new GameObject("InnerPanel", typeof(RectTransform), typeof(Image));
            innerPanel.transform.SetParent(_mainPanel.transform, false);
            var ipRT = innerPanel.GetComponent<RectTransform>();
            ipRT.anchorMin = Vector2.zero; ipRT.anchorMax = Vector2.one;
            ipRT.offsetMin = new Vector2(14f, 14f);
            ipRT.offsetMax = new Vector2(-14f, -14f);
            innerPanel.GetComponent<Image>().color = PanelBgColor;

            // All content goes inside the inner panel
            var content = innerPanel.transform;

            // ── Title ──
            _titleText = CreateText(content, "Title", "Trader's Bank", 26f, GoldTextColor, TextAlignmentOptions.Center);
            var titleRT = _titleText.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0f, 1f);
            titleRT.anchorMax = new Vector2(1f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.sizeDelta = new Vector2(0f, 36f);
            titleRT.anchoredPosition = new Vector2(0f, -8f);

            // ── Separator ──
            CreateSeparator(content, -48f);

            // ── Bank Balance ──
            _bankBalanceText = CreateText(content, "BankBalance", "Bank Balance: 0", 26f, GoldTextColor, TextAlignmentOptions.Center);
            var bbRT = _bankBalanceText.GetComponent<RectTransform>();
            bbRT.anchorMin = new Vector2(0f, 1f);
            bbRT.anchorMax = new Vector2(1f, 1f);
            bbRT.pivot = new Vector2(0.5f, 1f);
            bbRT.sizeDelta = new Vector2(0f, 36f);
            bbRT.anchoredPosition = new Vector2(0f, -64f);

            // ── Inventory Coins ──
            _inventoryCoinsText = CreateText(content, "InventoryCoins", "Inventory Coins: 0", 20f, new Color(0.85f, 0.85f, 0.85f), TextAlignmentOptions.Center);
            var icRT = _inventoryCoinsText.GetComponent<RectTransform>();
            icRT.anchorMin = new Vector2(0f, 1f);
            icRT.anchorMax = new Vector2(1f, 1f);
            icRT.pivot = new Vector2(0.5f, 1f);
            icRT.sizeDelta = new Vector2(0f, 28f);
            icRT.anchoredPosition = new Vector2(0f, -104f);

            // ── Total Wealth ──
            _totalWealthText = CreateText(content, "TotalWealth", "Total Wealth: 0", 16f, new Color(0.58f, 0.58f, 0.58f), TextAlignmentOptions.Center);
            var twRT = _totalWealthText.GetComponent<RectTransform>();
            twRT.anchorMin = new Vector2(0f, 1f);
            twRT.anchorMax = new Vector2(1f, 1f);
            twRT.pivot = new Vector2(0.5f, 1f);
            twRT.sizeDelta = new Vector2(0f, 22f);
            twRT.anchoredPosition = new Vector2(0f, -136f);

            // ── Separator before buttons ──
            CreateSeparator(content, -164f);

            // ── Status text (feedback) ──
            _statusText = CreateText(content, "Status", "", 16f, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Center);
            var stRT = _statusText.GetComponent<RectTransform>();
            stRT.anchorMin = new Vector2(0f, 0f);
            stRT.anchorMax = new Vector2(1f, 0f);
            stRT.pivot = new Vector2(0.5f, 0f);
            stRT.sizeDelta = new Vector2(0f, 24f);
            stRT.anchoredPosition = new Vector2(0f, _btnHeight + 20f);

            // ── Buttons ──
            float btnW = 160f;
            float gap = 16f;
            float totalW = btnW * 2f + gap;
            float startX = -totalW / 2f;
            float btnY = 10f;

            // Deposit (left)
            _depositButton = CreateActionButton(content, "DepositButton", "Deposit All",
                startX, btnY, btnW, OnDeposit, out _depositLabel, out _depositSelected);

            // Withdraw (right)
            _withdrawButton = CreateActionButton(content, "WithdrawButton", "Withdraw All",
                startX + btnW + gap, btnY, btnW, OnWithdraw, out _withdrawLabel, out _withdrawSelected);

            _canvasGO.SetActive(false);
            _uiBuilt = true;
        }

        private Button CreateActionButton(Transform parent, string name, string label,
            float x, float y, float width, UnityEngine.Events.UnityAction onClick,
            out TMP_Text outLabel, out GameObject outSelected)
        {
            outLabel = null;
            outSelected = null;

            if (_buttonTemplate == null) return null;

            var go = Instantiate(_buttonTemplate, parent);
            go.name = name;
            go.SetActive(true);
            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(onClick);
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            outLabel = go.GetComponentInChildren<TMP_Text>(true);
            if (outLabel != null)
            {
                outLabel.gameObject.SetActive(true);
                outLabel.text = label;
            }
            StripButtonHints(go, outLabel);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(width, _btnHeight);
            rt.anchoredPosition = new Vector2(x, y);

            // Controller selection highlight — sits behind button content
            var selGO = new GameObject("selected", typeof(RectTransform), typeof(Image));
            selGO.transform.SetParent(go.transform, false);
            selGO.transform.SetAsFirstSibling();
            var selRT = selGO.GetComponent<RectTransform>();
            selRT.anchorMin = Vector2.zero; selRT.anchorMax = Vector2.one;
            selRT.offsetMin = new Vector2(-4f, -4f);
            selRT.offsetMax = new Vector2(4f, 4f);
            selGO.GetComponent<Image>().color = SelectedColor;
            selGO.SetActive(false);
            outSelected = selGO;

            return btn;
        }

        private void CreateSeparator(Transform parent, float yPos)
        {
            var sep = new GameObject("Separator", typeof(RectTransform), typeof(Image));
            sep.transform.SetParent(parent, false);
            var rt = sep.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 1f);
            rt.anchorMax = new Vector2(0.95f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 2f);
            rt.anchoredPosition = new Vector2(0f, yPos);
            sep.GetComponent<Image>().color = new Color(GoldTextColor.r, GoldTextColor.g, GoldTextColor.b, 0.3f);
        }

        // ══════════════════════════════════════════
        //  TRANSACTIONS
        // ══════════════════════════════════════════

        private void OnDeposit()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            var inv = ((Humanoid)player).GetInventory();
            if (inv == null) return;

            string coinName = GetCoinName();
            if (string.IsNullOrEmpty(coinName)) return;

            int coins = inv.CountItems(coinName);
            if (coins <= 0)
            {
                SetStatus("No coins to deposit!");
                return;
            }

            inv.RemoveItem(coinName, coins);
            _bankBalance += coins;
            SaveBalance();
            RefreshDisplay();
            SetStatus($"Deposited {coins:N0} coins");
            ((Character)player).Message(MessageHud.MessageType.Center, $"Deposited {coins:N0} coins");
        }

        private void OnWithdraw()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            var inv = ((Humanoid)player).GetInventory();
            if (inv == null) return;

            if (_bankBalance <= 0)
            {
                SetStatus("Bank is empty!");
                return;
            }

            var coinPrefab = ObjectDB.instance?.GetItemPrefab("Coins");
            var coinDrop = coinPrefab?.GetComponent<ItemDrop>();
            if (coinDrop == null) return;

            int amount = _bankBalance;
            var added = inv.AddItem("Coins", amount, coinDrop.m_itemData.m_quality,
                coinDrop.m_itemData.m_variant, 0L, "");
            if (added == null)
            {
                SetStatus("Inventory full!");
                return;
            }
            _bankBalance = 0;
            SaveBalance();
            RefreshDisplay();
            SetStatus($"Withdrew {amount:N0} coins");
            ((Character)player).Message(MessageHud.MessageType.Center, $"Withdrew {amount:N0} coins");
        }

        // ══════════════════════════════════════════
        //  DISPLAY
        // ══════════════════════════════════════════

        private void RefreshDisplay()
        {
            int invCoins = GetInventoryCoins();

            if (_bankBalanceText != null)
                _bankBalanceText.text = $"Bank Balance: {_bankBalance:N0}";
            if (_inventoryCoinsText != null)
                _inventoryCoinsText.text = $"Inventory Coins: {invCoins:N0}";
            if (_totalWealthText != null)
                _totalWealthText.text = $"Total Wealth: {(_bankBalance + invCoins):N0}";

            if (_depositButton != null)
                _depositButton.interactable = invCoins > 0;
            if (_withdrawButton != null)
                _withdrawButton.interactable = _bankBalance > 0;

            UpdateControllerHighlight();
        }

        private void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
        }

        // ══════════════════════════════════════════
        //  PERSISTENCE
        // ══════════════════════════════════════════

        private void LoadBalance()
        {
            _bankBalance = BankBalanceStore.Read(Player.m_localPlayer);
        }

        private void SaveBalance()
        {
            BankBalanceStore.Write(Player.m_localPlayer, _bankBalance);
        }

        // ══════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════

        private int GetInventoryCoins()
        {
            var player = Player.m_localPlayer;
            if (player == null) return 0;
            var inv = ((Humanoid)player).GetInventory();
            string coinName = GetCoinName();
            if (inv == null || string.IsNullOrEmpty(coinName)) return 0;
            return inv.CountItems(coinName);
        }

        private string GetCoinName()
        {
            var coinPrefab = ObjectDB.instance?.GetItemPrefab("Coins");
            var coinDrop = coinPrefab?.GetComponent<ItemDrop>();
            return coinDrop?.m_itemData?.m_shared?.m_name;
        }

        private bool ExtractAssets()
        {
            var invGui = InventoryGui.instance;
            if (invGui == null) return false;

            var bgTex = TextureLoader.LoadUITexture("PanelBackground");
            if (bgTex != null)
                _bgSprite = Sprite.Create(bgTex, new Rect(0, 0, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f));

            if (invGui.m_craftButton != null)
            {
                var origRT = invGui.m_craftButton.GetComponent<RectTransform>();
                if (origRT != null) _btnHeight = Mathf.Max(origRT.rect.height, 36f);
                _buttonTemplate = Instantiate(invGui.m_craftButton.gameObject);
                _buttonTemplate.name = "BankButtonTemplate";
                _buttonTemplate.SetActive(false);
                DontDestroyOnLoad(_buttonTemplate);
            }

            _valheimFont = FindValheimFont();
            return _buttonTemplate != null;
        }

        private TMP_Text CreateText(Transform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (_valheimFont != null) tmp.font = _valheimFont;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.text = text;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        private TMP_FontAsset FindValheimFont()
        {
            var invGui = InventoryGui.instance;
            if (invGui != null)
            {
                if (invGui.m_recipeName != null && invGui.m_recipeName.font != null)
                    return invGui.m_recipeName.font;
                if (invGui.m_recipeDecription != null && invGui.m_recipeDecription.font != null)
                    return invGui.m_recipeDecription.font;
            }
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f.name.Contains("Valheim") || f.name.Contains("Averia"))
                    return f;
            return null;
        }

        private static void StripButtonHints(GameObject buttonGO, TMP_Text labelText)
        {
            for (int i = buttonGO.transform.childCount - 1; i >= 0; i--)
            {
                var child = buttonGO.transform.GetChild(i);
                if (labelText != null &&
                    (child.gameObject == labelText.gameObject || labelText.transform.IsChildOf(child)))
                    continue;
                DestroyImmediate(child.gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_buttonTemplate != null) Destroy(_buttonTemplate);
        }
    }
}
