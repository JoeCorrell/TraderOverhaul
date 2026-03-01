using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TraderOverhaul
{
    public class TraderUI : MonoBehaviour
    {
        // ── State ──
        private bool _isVisible;
        private Trader _currentTrader;
        private StoreGui _currentStoreGui;
        private TraderKind _currentTraderKind;
        private string _currentTraderName = "Trader";

        // ── Tab ──
        private int _activeTab; // 0=Buy, 1=Sell, 2=Bank
        private static readonly string[] TabNames = { "Buy", "Sell", "Bank" };

        // ── Selection ──
        private int _selectedBuyIndex = -1;
        private int _selectedSellIndex = -1;

        // ── Categories ──
        private readonly Dictionary<string, bool> _buyCategoryCollapsed = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _sellCategoryCollapsed = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _buyRarityCollapsed = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _sellRarityCollapsed = new Dictionary<string, bool>();
        private static readonly string[] CategoryBuckets =
            { "Weapons", "Armor", "Shields", "Ammo", "Consumables", "Materials", "Utility", "Trophies", "Misc" };
        private static readonly string[] RarityOrder = { "", "Magic", "Rare", "Epic", "Legendary", "Mythic" };
        private static readonly Dictionary<string, string> RarityDisplayNames = new Dictionary<string, string>
        {
            { "", "Base" }, { "Magic", "Magic" }, { "Rare", "Rare" },
            { "Epic", "Epic" }, { "Legendary", "Legendary" }, { "Mythic", "Mythic" }
        };

        // ── Tooltip cache for rarity items (so effects stay consistent while trader is open) ──
        private readonly Dictionary<string, string> _rarityTooltipCache = new Dictionary<string, string>();

        // ── Search ──
        private string _searchFilter = "";
        private TMP_InputField _searchInput;

        // ── Category filter buttons ──
        private string _activeCategoryFilter = null;
        private readonly List<Button> _categoryFilterButtons = new List<Button>();
        private readonly List<string> _categoryFilterKeys = new List<string>();
        private int _joyCategoryFocusIndex = -1; // controller hover index (-1 = none)
        private int _lastJoyCategoryFocusIndex = -1; // remembered position when leaving categories

        // ── UI root ──
        private GameObject _canvasGO;
        private bool _uiBuilt;

        // ── Extracted assets ──
        private Sprite _bgSprite;
        private Sprite _textFieldSprite;
        private Sprite _catBtnSprite;
        private GameObject _recipeElementPrefab;
        private GameObject _buttonTemplate;
        private float _scrollSensitivity = 40f;
        private TMP_FontAsset _valheimFont;

        // ── Panel structure ──
        private GameObject _mainPanel;
        private RectTransform _leftColumn;
        private RectTransform _middleColumn;
        private RectTransform _rightColumn;

        // ── Tab buttons ──
        private GameObject _tabBuy;
        private GameObject _tabSell;
        private GameObject _tabBank;

        // ── Bank ──
        private int _bankBalance;
        private int _bankFocusedButton; // 0=deposit, 1=withdraw
        private GameObject _bankContentPanel;
        private TMP_Text _bankTitleText;
        private TMP_Text _bankBalanceText;
        private TMP_Text _bankInvCoinsText;
        private TMP_Text _bankTotalText;
        private TMP_Text _bankStatusText;
        private Button _bankDepositButton;
        private Button _bankWithdrawButton;
        private GameObject _bankDepositSelected;
        private GameObject _bankWithdrawSelected;

        // ── Left column: item list ──
        private RectTransform _listRoot;
        private ScrollRect _listScrollRect;

        // ── Middle column: description ──
        private TMP_Text _itemNameText;
        private TMP_Text _itemDescText;
        private ScrollRect _descScrollRect;
        private int _descScrollResetFrames;
        private Button _actionButton;
        private TMP_Text _actionButtonLabel;
        private TMP_Text _coinDisplayText;

        // ── Right column: player preview (buy tab) ──
        private RenderTexture _playerPreviewRT;
        private GameObject _playerCamGO;
        private Camera _playerCam;
        private GameObject _playerClone;
        private GameObject _playerLightRig;
        private RawImage _playerPreviewImg;
        private static readonly Vector3 PlayerSpawnPos = new Vector3(10000f, 5000f, 10000f);

        // ── Right column: trader preview (sell tab) ──
        private RenderTexture _haldorPreviewRT;
        private GameObject _haldorCamGO;
        private Camera _haldorCam;
        private GameObject _haldorClone;
        private GameObject _haldorLightRig;
        private RawImage _haldorPreviewImg;
        private static readonly Vector3 TraderSpawnPos = new Vector3(10000f, 5000f, 10020f);

        // ── Ambient override ──
        private Color _savedAmbientColor;
        private float _savedAmbientIntensity;
        private UnityEngine.Rendering.AmbientMode _savedAmbientMode;

        // ── Preview rotation ──
        private float _previewRotation;
        private const float AutoRotateSpeed = 12f;
        private bool _isDraggingPlayerPreview;
        private float _lastMouseX;
        private const float MouseDragSensitivity = 0.5f;

        // ── Layout constants ──
        private const float ColGap = 4f;
        private const float TabTopGap = 6f;
        private const float ExtraMiddleWidth = 80f;
        private const float OuterPad = 6f;
        private const float SearchBoxHeight = 32f;
        private const float FilterRowHeight = 38f; // 30 × 1.25
        static readonly Color ColOverlay = new Color(0f, 0f, 0f, 0f); // no overlay — matches native Valheim UIs
        static readonly Color GoldColor = new Color(0.83f, 0.64f, 0.31f, 1f);
        static readonly Color GoldTextColor = new Color(0.83f, 0.52f, 0.18f, 1f);
        static readonly Color CategoryHeaderBg = new Color(0.3f, 0.25f, 0.15f, 0.85f);
        static readonly Color CategoryHeaderText = new Color(1f, 0.9f, 0.5f, 1f);

        // Computed at ExtractAssets
        private float _panelWidth, _panelHeight;
        private float _leftColWidth, _midColWidth, _rightColWidth;
        private float _leftPad, _bottomPad, _colTopInset;
        private float _tabBtnHeight, _craftBtnHeight;

        // ── Data lists ──
        private readonly List<BuyEntry> _allBuyEntries = new List<BuyEntry>();
        private readonly List<SellEntry> _allSellEntries = new List<SellEntry>();
        private readonly List<(GameObject go, int dataIndex)> _listElements = new List<(GameObject, int)>();
        private readonly List<GameObject> _categoryHeaders = new List<GameObject>();

        // ── Inventory tracking ──
        private int _lastInventoryHash;
        private int _lastCoinDisplayCount = -1;
        private int _lastBankBalanceDisplay = -1;
        private int _lastBankInvCoinsDisplay = -1;

        // ── VisEquipment cache ──
        private Dictionary<string, string> _savedEquipSlots;

        // ── Inner data classes ──
        private class BuyEntry
        {
            public string PrefabName;
            public string Name;
            public string Description;
            public int Price;
            public int Stack;
            public string Category;
            public Sprite Icon;
            public string Rarity = "";
        }

        private class SellEntry
        {
            public ItemDrop.ItemData Item;
            public string PrefabName;
            public string Name;
            public int Price;
            public int ConfigStack;
            public string Category;
            public Sprite Icon;
            public string Rarity = "";
        }

        // ══════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════

        public bool IsVisible => _isVisible;
        public bool IsSearchFocused => _searchInput != null && _searchInput.isFocused;

        public void Show(Trader trader, StoreGui storeGui)
        {
            _currentTrader = trader;
            _currentStoreGui = storeGui;
            _currentTraderKind = TraderPatches.GetTraderKind(trader);
            _currentTraderName = TraderIdentity.DisplayName(_currentTraderKind);

            if (!_uiBuilt) BuildUI();
            if (!_uiBuilt) return;

            _canvasGO.SetActive(true);
            _isVisible = true;
            _activeTab = 0;
            _selectedBuyIndex = -1;
            _selectedSellIndex = -1;
            _searchFilter = "";
            if (_searchInput != null) _searchInput.text = "";
            _activeCategoryFilter = null;
            _joyCategoryFocusIndex = -1;
            _lastJoyCategoryFocusIndex = -1;
            _lastInventoryHash = 0;
            _lastCoinDisplayCount = -1;
            _lastBankBalanceDisplay = -1;
            _lastBankInvCoinsDisplay = -1;
            _buyRarityCollapsed.Clear();
            _sellRarityCollapsed.Clear();
            _rarityTooltipCache.Clear();

            SetupPlayerPreview();
            SetupTraderPreview();
            _previewRotation = 0f;
            EnablePreviewCameras();

            LoadBankBalance(); // load before coin display and buy affordability checks
            UpdateBankTitle();

            BuildBuyEntries();
            BuildSellEntries();

            RefreshTabHighlights();
            RefreshTabPanels();
            UpdateCoinDisplay();
        }

        public void Hide()
        {
            _isVisible = false;
            DisablePreviewCameras();
            ClearPlayerPreview();
            ClearTraderPreview();
            if (_canvasGO != null)
                _canvasGO.SetActive(false);
            _currentTrader = null;
            _currentStoreGui = null;
            _currentTraderKind = TraderKind.Unknown;
            _currentTraderName = "Trader";
        }

        // ══════════════════════════════════════════
        //  MONO CALLBACKS
        // ══════════════════════════════════════════

        private void Update()
        {
            if (!_isVisible) return;

            // Distance check
            if (_currentTrader != null && Player.m_localPlayer != null)
            {
                float dist = Vector3.Distance(Player.m_localPlayer.transform.position, _currentTrader.transform.position);
                if (dist > 15f) { Hide(); return; }
            }

            // Cursor
            if (!ZInput.IsGamepadActive())
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.visible = false;
            }

            // Escape
            if (Input.GetKeyDown(KeyCode.Escape)) { Hide(); return; }

            // Tab switching (Q/E) — only when search not focused
            if (!IsSearchFocused)
            {
                if (Input.GetKeyDown(KeyCode.Q)) SwitchTab(Mathf.Max(0, _activeTab - 1));
                if (Input.GetKeyDown(KeyCode.E)) SwitchTab(Mathf.Min(2, _activeTab + 1));
            }

            // Preview rotation (player only; trader camera is fixed)
            UpdatePlayerPreviewRotation();
            UpdatePlayerCamera();

            // Inventory change detection
            RefreshSellListIfChanged();
            UpdateCoinDisplay();

            // Keep bank display live
            if (_activeTab == 2) RefreshBankDisplay();

            // Gamepad
            UpdateGamepadInput();
        }

        private void LateUpdate()
        {
            if (!_isVisible) return;

            var hud = Hud.instance;
            if (hud != null)
            {
                if (hud.m_crosshair != null) hud.m_crosshair.color = Color.clear;
                // Suppress trader hover text while our UI is open
                if (hud.m_hoverName != null) hud.m_hoverName.text = "";
            }

            if (_descScrollResetFrames > 0 && _descScrollRect != null)
            {
                _descScrollRect.verticalNormalizedPosition = 1f;
                _descScrollRect.velocity = Vector2.zero;
                _descScrollResetFrames--;
            }

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                if (_searchInput == null || EventSystem.current.currentSelectedGameObject != _searchInput.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
            }

            // Manually render the active preview camera with precise ambient control.
            // try-finally guarantees RestoreAmbient() always runs even if cam.Render() throws,
            // preventing permanent corruption of the game's scene lighting.
            SaveAmbient();
            try
            {
                SetPreviewAmbient();
                if (_activeTab == 0 && _playerCam != null)
                    _playerCam.Render();
                else if (_activeTab == 1 && _haldorCam != null)
                    _haldorCam.Render();
            }
            finally
            {
                RestoreAmbient();
            }
        }

        private void OnDestroy()
        {
            ClearPlayerPreview();
            ClearTraderPreview();
            if (_playerCamGO != null) Destroy(_playerCamGO);
            if (_haldorCamGO != null) Destroy(_haldorCamGO);
            if (_playerPreviewRT != null) { _playerPreviewRT.Release(); Destroy(_playerPreviewRT); }
            if (_haldorPreviewRT != null) { _haldorPreviewRT.Release(); Destroy(_haldorPreviewRT); }
            if (_buttonTemplate != null) Destroy(_buttonTemplate);
            if (_canvasGO != null) Destroy(_canvasGO);
        }

        // ══════════════════════════════════════════
        //  GAMEPAD INPUT
        // ══════════════════════════════════════════

        private void UpdateGamepadInput()
        {
            if (ZInput.GetButtonDown("JoyTabLeft")) SwitchTab(Mathf.Max(0, _activeTab - 1));
            if (ZInput.GetButtonDown("JoyTabRight")) SwitchTab(Mathf.Min(2, _activeTab + 1));
            if (ZInput.GetButtonDown("JoyButtonB")) { Hide(); return; }

            // Up/down — navigate item list; also clears category button focus
            if (ZInput.GetButtonDown("JoyLStickDown") || ZInput.GetButtonDown("JoyDPadDown"))
            {
                if (_joyCategoryFocusIndex >= 0) _lastJoyCategoryFocusIndex = _joyCategoryFocusIndex;
                _joyCategoryFocusIndex = -1;
                UpdateCategoryFilterVisuals();
                MoveSelection(1);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
            if (ZInput.GetButtonDown("JoyLStickUp") || ZInput.GetButtonDown("JoyDPadUp"))
            {
                bool atTop = (_activeTab == 0 && _selectedBuyIndex <= 0) ||
                             (_activeTab == 1 && _selectedSellIndex <= 0);
                if (atTop && _joyCategoryFocusIndex < 0 && _categoryFilterButtons.Count > 0)
                {
                    // Return to category buttons from top of item list
                    _joyCategoryFocusIndex = _lastJoyCategoryFocusIndex >= 0
                        ? Mathf.Min(_lastJoyCategoryFocusIndex, _categoryFilterButtons.Count - 1)
                        : 0;
                    UpdateCategoryFilterVisuals();
                }
                else
                {
                    if (_joyCategoryFocusIndex >= 0) _lastJoyCategoryFocusIndex = _joyCategoryFocusIndex;
                    _joyCategoryFocusIndex = -1;
                    UpdateCategoryFilterVisuals();
                    MoveSelection(-1);
                }
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }

            // Left/right — navigate category buttons on buy/sell, bank buttons on bank tab
            if (ZInput.GetButtonDown("JoyLStickLeft") || ZInput.GetButtonDown("JoyDPadLeft"))
            {
                if (_activeTab == 2) { _bankFocusedButton = 0; UpdateBankHighlight(); }
                else NavigateCategoryButtons(-1);
            }
            if (ZInput.GetButtonDown("JoyLStickRight") || ZInput.GetButtonDown("JoyDPadRight"))
            {
                if (_activeTab == 2) { _bankFocusedButton = 1; UpdateBankHighlight(); }
                else NavigateCategoryButtons(1);
            }

            // A = confirm focused category button, or buy/sell, or bank action
            if (ZInput.GetButtonDown("JoyButtonA"))
            {
                if (_joyCategoryFocusIndex >= 0 && _joyCategoryFocusIndex < _categoryFilterKeys.Count)
                {
                    OnCategoryFilterClicked(_categoryFilterKeys[_joyCategoryFocusIndex]);
                    _lastJoyCategoryFocusIndex = _joyCategoryFocusIndex;
                    _joyCategoryFocusIndex = -1;
                    UpdateCategoryFilterVisuals();
                }
                else if (_activeTab == 2)
                {
                    if (_bankFocusedButton == 0) OnBankDeposit();
                    else OnBankWithdraw();
                }
                else if (_actionButton != null && _actionButton.interactable)
                    OnActionButtonClicked();
            }

            // X = toggle category
            if (ZInput.GetButtonDown("JoyButtonX"))
                ToggleFocusedCategory();

            // Right stick = scroll description
            if (_descScrollRect != null)
            {
                float scrollSpeed = 2f;
                if (ZInput.GetButton("JoyRStickDown"))
                {
                    _descScrollRect.verticalNormalizedPosition -= scrollSpeed * Time.deltaTime;
                    _descScrollRect.verticalNormalizedPosition = Mathf.Clamp01(_descScrollRect.verticalNormalizedPosition);
                }
                if (ZInput.GetButton("JoyRStickUp"))
                {
                    _descScrollRect.verticalNormalizedPosition += scrollSpeed * Time.deltaTime;
                    _descScrollRect.verticalNormalizedPosition = Mathf.Clamp01(_descScrollRect.verticalNormalizedPosition);
                }
            }
        }

        private void NavigateCategoryButtons(int dir)
        {
            if (_categoryFilterButtons.Count == 0) return;
            if (_joyCategoryFocusIndex < 0)
            {
                // Restore last known position, then nudge in the pressed direction
                if (_lastJoyCategoryFocusIndex >= 0)
                    _joyCategoryFocusIndex = Mathf.Clamp(_lastJoyCategoryFocusIndex, 0, _categoryFilterButtons.Count - 1);
                else
                    _joyCategoryFocusIndex = dir > 0 ? 0 : _categoryFilterButtons.Count - 1;
            }
            else
                _joyCategoryFocusIndex = Mathf.Clamp(_joyCategoryFocusIndex + dir, 0, _categoryFilterButtons.Count - 1);
            UpdateCategoryFilterVisuals();
        }

        private void MoveSelection(int direction)
        {
            if (_activeTab == 0)
            {
                int count = GetVisibleBuyCount();
                if (count == 0) return;
                int next = _selectedBuyIndex + direction;
                next = Mathf.Clamp(next, 0, count - 1);
                SelectBuyItem(next);
                EnsureItemVisible(next);
            }
            else if (_activeTab == 1)
            {
                int count = GetVisibleSellCount();
                if (count == 0) return;
                int next = _selectedSellIndex + direction;
                next = Mathf.Clamp(next, 0, count - 1);
                SelectSellItem(next);
                EnsureItemVisible(next);
            }
            else if (_activeTab == 2)
            {
                // Bank — switch between deposit/withdraw
                _bankFocusedButton = direction > 0 ? 1 : 0;
                UpdateBankHighlight();
            }
        }

        private void ToggleFocusedCategory()
        {
            // If the selected item has a rarity, toggle its rarity sub-group.
            // Otherwise toggle the parent category header.
            if (_activeTab == 0 && _selectedBuyIndex >= 0)
            {
                var entries = GetFilteredBuyEntries();
                if (_selectedBuyIndex < entries.Count)
                {
                    var entry = entries[_selectedBuyIndex];
                    if (!string.IsNullOrEmpty(entry.Rarity))
                    {
                        string key = entry.Category + "|" + entry.Rarity;
                        _buyRarityCollapsed[key] = !(_buyRarityCollapsed.TryGetValue(key, out bool rv) && rv);
                    }
                    else
                    {
                        _buyCategoryCollapsed[entry.Category] = !(_buyCategoryCollapsed.TryGetValue(entry.Category, out bool c) && c);
                    }
                    PopulateCurrentList();
                }
            }
            else if (_activeTab == 1 && _selectedSellIndex >= 0)
            {
                var entries = GetFilteredSellEntries();
                if (_selectedSellIndex < entries.Count)
                {
                    var entry = entries[_selectedSellIndex];
                    if (!string.IsNullOrEmpty(entry.Rarity))
                    {
                        string key = entry.Category + "|" + entry.Rarity;
                        _sellRarityCollapsed[key] = !(_sellRarityCollapsed.TryGetValue(key, out bool rv) && rv);
                    }
                    else
                    {
                        _sellCategoryCollapsed[entry.Category] = !(_sellCategoryCollapsed.TryGetValue(entry.Category, out bool c) && c);
                    }
                    PopulateCurrentList();
                }
            }
        }

        // ══════════════════════════════════════════
        //  UI CONSTRUCTION
        // ══════════════════════════════════════════

        private void BuildUI()
        {
            if (!ExtractAssets())
            {
                TraderOverhaulPlugin.Log.LogError("[TraderUI] Failed to extract Valheim assets.");
                return;
            }

            // Canvas
            _canvasGO = new GameObject("TraderOverhaul_Canvas");
            _canvasGO.transform.SetParent(transform);
            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasGO.AddComponent<GraphicRaycaster>();

            // Overlay
            var overlay = new GameObject("Overlay", typeof(RectTransform));
            overlay.transform.SetParent(_canvasGO.transform, false);
            var oRT = overlay.GetComponent<RectTransform>();
            oRT.anchorMin = Vector2.zero; oRT.anchorMax = Vector2.one;
            oRT.offsetMin = Vector2.zero; oRT.offsetMax = Vector2.zero;
            overlay.AddComponent<Image>().color = ColOverlay;

            // Main panel
            _mainPanel = new GameObject("MainPanel", typeof(RectTransform), typeof(Image));
            _mainPanel.transform.SetParent(_canvasGO.transform, false);
            var mainRT = _mainPanel.GetComponent<RectTransform>();
            mainRT.anchorMin = new Vector2(0.5f, 0.5f);
            mainRT.anchorMax = new Vector2(0.5f, 0.5f);
            mainRT.pivot = new Vector2(0.5f, 0.5f);
            mainRT.sizeDelta = new Vector2(_panelWidth, _panelHeight);
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

            // Column positions
            float midColX = _leftPad + _leftColWidth + ColGap;
            float rightColX = midColX + _midColWidth + ColGap;

            _leftColumn = CreateColumn("LeftColumn", _leftPad, _leftPad + _leftColWidth);
            _middleColumn = CreateColumn("MiddleColumn", midColX, midColX + _midColWidth);
            _rightColumn = CreateColumn("RightColumn", rightColX, rightColX + _rightColWidth);

            BuildItemListArea();
            BuildDescriptionColumn();
            BuildPreviewColumn();

            // Tab buttons — each tab is exactly as wide as its column, centered over it
            // (mirrors the class selection UI pattern)
            _tabBuy  = CreateTabButton("Buy",  0, _leftPad + _leftColWidth / 2f,   _leftColWidth,  TabTopGap);
            _tabSell = CreateTabButton("Sell", 1, midColX + _midColWidth / 2f,   _midColWidth,   TabTopGap);
            _tabBank = CreateTabButton("Bank", 2, rightColX + _rightColWidth / 2f, _rightColWidth, TabTopGap);

            BuildBankPanel();

            _canvasGO.SetActive(false);
            _uiBuilt = true;
        }

        private bool ExtractAssets()
        {
            var invGui = InventoryGui.instance;
            if (invGui == null || invGui.m_crafting == null) return false;

            // Background texture
            var bgTex = TextureLoader.LoadUITexture("PanelBackground");
            if (bgTex != null)
                _bgSprite = Sprite.Create(bgTex, new Rect(0, 0, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f));

            // Recipe element prefab
            _recipeElementPrefab = invGui.m_recipeElementPrefab;
            if (_recipeElementPrefab == null) return false;

            // Search bar background — load custom texture
            var searchBarTex = TextureLoader.LoadUITexture("SearchBarBackground");
            if (searchBarTex != null)
                _textFieldSprite = Sprite.Create(searchBarTex, new Rect(0, 0, searchBarTex.width, searchBarTex.height), new Vector2(0.5f, 0.5f));

            // Category button background — load custom texture
            var catBtnTex = TextureLoader.LoadUITexture("CategoryBackground");
            if (catBtnTex != null)
                _catBtnSprite = Sprite.Create(catBtnTex, new Rect(0, 0, catBtnTex.width, catBtnTex.height), new Vector2(0.5f, 0.5f));

            // Button template
            _craftBtnHeight = 30f;
            if (invGui.m_craftButton != null)
            {
                var origRT = invGui.m_craftButton.GetComponent<RectTransform>();
                if (origRT != null) _craftBtnHeight = Mathf.Max(origRT.rect.height, 30f);
                _buttonTemplate = Instantiate(invGui.m_craftButton.gameObject);
                _buttonTemplate.name = "ButtonTemplate";
                _buttonTemplate.SetActive(false);
                DontDestroyOnLoad(_buttonTemplate);
            }

            // Scroll sensitivity
            if (invGui.m_recipeListRoot != null)
            {
                var sr = invGui.m_recipeListRoot.GetComponentInParent<ScrollRect>();
                if (sr != null) _scrollSensitivity = sr.scrollSensitivity;
            }

            // Font
            _valheimFont = FindValheimFont();

            // Dimensions from crafting panel
            var craftRT = invGui.m_crafting.GetComponent<RectTransform>();
            float origW = craftRT.rect.width;
            float origH = craftRT.rect.height;
            if (origW <= 10f) origW = 567f;
            if (origH <= 10f) origH = 480f;

            float descColWidth = 260f;
            var descPanelTr = invGui.m_crafting.transform.Find("Decription");
            if (descPanelTr != null)
            {
                var descRT = descPanelTr as RectTransform;
                if (descRT != null)
                {
                    float w = descRT.rect.width;
                    if (w > 10f) descColWidth = w;
                }
            }

            _leftColWidth = descColWidth;
            _midColWidth = descColWidth + ExtraMiddleWidth;
            _rightColWidth = descColWidth;
            _leftPad = OuterPad;
            float totalW = _leftColWidth + ColGap + _midColWidth + ColGap + _rightColWidth;
            _panelWidth = totalW + OuterPad * 2f;
            _panelHeight = origH * 0.9f;
            _bottomPad = OuterPad;
            _tabBtnHeight = Mathf.Max(_craftBtnHeight, 30f);
            _colTopInset = TabTopGap + _tabBtnHeight + 6f;

            return true;
        }

        private RectTransform CreateColumn(string name, float xLeft, float xRight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_mainPanel.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(xLeft, _bottomPad);
            rt.offsetMax = new Vector2(xRight, -_colTopInset);
            ApplyPanelStyle(go.GetComponent<Image>());
            return rt;
        }

        private void BuildItemListArea()
        {
            // Search box at top of left column
            BuildSearchBox();

            // Category filter buttons below search box
            BuildCategoryFilterRow();

            // Scroll viewport below filter row
            var scrollGO = new GameObject("ItemListScroll", typeof(RectTransform), typeof(Image), typeof(Mask));
            scrollGO.transform.SetParent(_leftColumn, false);
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(2f, 2f);
            scrollRT.offsetMax = new Vector2(-2f, -(SearchBoxHeight + 6f + FilterRowHeight + 4f));
            scrollGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            scrollGO.GetComponent<Mask>().showMaskGraphic = false;

            // Content root
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(scrollGO.transform, false);
            _listRoot = contentGO.GetComponent<RectTransform>();
            _listRoot.anchorMin = new Vector2(0f, 1f);
            _listRoot.anchorMax = new Vector2(1f, 1f);
            _listRoot.pivot = new Vector2(0.5f, 1f);
            _listRoot.anchoredPosition = Vector2.zero;
            _listRoot.sizeDelta = Vector2.zero;

            // Scrollbar (hidden)
            var sb = CreateHiddenScrollbar(_leftColumn);

            // ScrollRect
            _listScrollRect = scrollGO.AddComponent<ScrollRect>();
            _listScrollRect.content = _listRoot;
            _listScrollRect.viewport = scrollRT;
            _listScrollRect.vertical = true;
            _listScrollRect.horizontal = false;
            _listScrollRect.movementType = ScrollRect.MovementType.Clamped;
            _listScrollRect.scrollSensitivity = _scrollSensitivity * 4f;
            _listScrollRect.verticalScrollbar = sb;
            _listScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        private void BuildSearchBox()
        {
            var bg = new GameObject("SearchField", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_leftColumn, false);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 1f);
            bgRT.anchorMax = new Vector2(1f, 1f);
            bgRT.pivot = new Vector2(0.5f, 1f);
            bgRT.sizeDelta = new Vector2(0f, SearchBoxHeight);
            bgRT.anchoredPosition = new Vector2(0f, -2f);
            var bgImg = bg.GetComponent<Image>();
            if (_textFieldSprite != null)
            {
                bgImg.sprite = _textFieldSprite;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = new Color(0.12f, 0.08f, 0.04f, 0.9f);
            }

            // Text area
            var textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(bg.transform, false);
            var taRT = textArea.GetComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
            taRT.offsetMin = new Vector2(8f, 2f); taRT.offsetMax = new Vector2(-8f, -2f);

            // Placeholder — created inactive so TMP Awake doesn't fire before font is set
            var phGO = new GameObject("Placeholder", typeof(RectTransform));
            phGO.SetActive(false);
            phGO.transform.SetParent(textArea.transform, false);
            var ph = phGO.AddComponent<TextMeshProUGUI>();
            if (_valheimFont != null) ph.font = _valheimFont;
            ph.text = "Search...";
            ph.fontSize = 16f;
            ph.color = new Color(0.6f, 0.55f, 0.45f, 0.7f);
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            var phRT = phGO.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
            phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
            phGO.SetActive(true);

            // Input text — created inactive so TMP Awake doesn't fire before font is set
            var txtGO = new GameObject("Text", typeof(RectTransform));
            txtGO.SetActive(false);
            txtGO.transform.SetParent(textArea.transform, false);
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            if (_valheimFont != null) txt.font = _valheimFont;
            txt.fontSize = 16f;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            var txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
            txtGO.SetActive(true);

            // TMP_InputField
            _searchInput = bg.AddComponent<TMP_InputField>();
            _searchInput.textViewport = taRT;
            _searchInput.textComponent = txt;
            _searchInput.placeholder = ph;
            if (_valheimFont != null) _searchInput.fontAsset = _valheimFont;
            _searchInput.pointSize = 16f;
            _searchInput.characterLimit = 50;
            _searchInput.onValueChanged.AddListener(OnSearchChanged);

            // Hover effect on search background
            Color searchBaseColor = _textFieldSprite != null ? Color.white : new Color(0.12f, 0.08f, 0.04f, 0.9f);
            AddHoverEffect(bg, searchBaseColor, 0.1f);
        }

        private void BuildCategoryFilterRow()
        {
            // Container row sitting below the search box
            var rowGO = new GameObject("CategoryFilterRow", typeof(RectTransform), typeof(Image));
            rowGO.transform.SetParent(_leftColumn, false);
            var rowRT = rowGO.GetComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0f, 1f);
            rowRT.anchorMax = new Vector2(1f, 1f);
            rowRT.pivot = new Vector2(0.5f, 1f);
            rowRT.sizeDelta = new Vector2(-4f, FilterRowHeight);
            rowRT.anchoredPosition = new Vector2(0f, -(SearchBoxHeight + 6f));
            rowGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // transparent — let column bg show through

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 2f;
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            _categoryFilterButtons.Clear();
            _categoryFilterKeys.Clear();

            // Category → representative item prefab name — icon pulled from ObjectDB at runtime
            var categoryPrefabs = new Dictionary<string, string>
            {
                { "Weapons",     "AxeBronze"          },
                { "Armor",       "HelmetTrollLeather" },
                { "Shields",     "ShieldWood"         },
                { "Ammo",        "ArrowObsidian"      },
                { "Consumables", "MeadStaminaMinor"   },
                { "Materials",   "Bronze"             },
                { "Utility",     "BeltStrength"       },
                { "Trophies",    "TrophySkeleton"     },
                { "Misc",        "Coins"              },
            };

            foreach (string cat in CategoryBuckets)
            {
                string pn = categoryPrefabs.TryGetValue(cat, out string v) ? v : null;
                Sprite icon = pn != null ? GetItemIcon(pn) : null;
                var btn = CreateCategoryFilterButton(rowGO.transform, cat, icon);
                if (btn != null)
                {
                    _categoryFilterButtons.Add(btn);
                    _categoryFilterKeys.Add(cat);
                }
            }

            UpdateCategoryFilterVisuals();
        }

        private Button CreateCategoryFilterButton(Transform parent, string category, Sprite icon)
        {
            if (_buttonTemplate == null) return null;

            var btnGO = Instantiate(_buttonTemplate, parent);
            btnGO.name = $"CatBtn_{category}";
            btnGO.SetActive(true);

            // Strip all children inherited from the craft button template
            for (int i = btnGO.transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(btnGO.transform.GetChild(i).gameObject);

            // Strip components that cause stretching on hover
            var anim = btnGO.GetComponent<Animator>();
            if (anim != null) DestroyImmediate(anim);
            var csf = btnGO.GetComponent<ContentSizeFitter>();
            if (csf != null) DestroyImmediate(csf);
            var le = btnGO.GetComponent<LayoutElement>();
            if (le != null) DestroyImmediate(le);

            // Apply the game's button sprite as the button background
            var bgImg = btnGO.GetComponent<Image>();
            if (bgImg != null)
            {
                if (_catBtnSprite != null)
                {
                    bgImg.sprite = _catBtnSprite;
                    bgImg.type = Image.Type.Sliced;
                }
                bgImg.color = Color.white;
            }

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.transition = Selectable.Transition.ColorTint;
                string cat = category;
                btn.onClick.AddListener(() => OnCategoryFilterClicked(cat));
                btn.navigation = new Navigation { mode = Navigation.Mode.None };

                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f); // subtle dim on hover
                colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);     // dim on press
                colors.selectedColor = Color.white;
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                btn.colors = colors;
            }

            // Icon image inside the button
            if (icon != null)
            {
                var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGO.transform.SetParent(btnGO.transform, false);
                var iconRT = iconGO.GetComponent<RectTransform>();
                iconRT.anchorMin = new Vector2(0.15f, 0.15f);
                iconRT.anchorMax = new Vector2(0.85f, 0.85f);
                iconRT.offsetMin = Vector2.zero;
                iconRT.offsetMax = Vector2.zero;
                var iconImg = iconGO.GetComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            return btn;
        }

        private void OnCategoryFilterClicked(string category)
        {
            // Toggle: clicking the active category resets to "all"
            _activeCategoryFilter = (_activeCategoryFilter == category) ? null : category;
            _selectedBuyIndex = -1;
            _selectedSellIndex = -1;
            UpdateCategoryFilterVisuals();
            if (_listScrollRect != null)
            {
                _listScrollRect.verticalNormalizedPosition = 1f;
                _listScrollRect.velocity = Vector2.zero;
            }
            PopulateCurrentList();
        }

        private void UpdateCategoryFilterVisuals()
        {
            for (int i = 0; i < _categoryFilterButtons.Count; i++)
            {
                var btn = _categoryFilterButtons[i];
                if (btn == null) continue;
                bool active  = _activeCategoryFilter == _categoryFilterKeys[i];
                bool focused = _joyCategoryFocusIndex == i;
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = active
                        ? new Color(1f, 0.75f, 0.3f, 1f)   // warm gold tint — active filter
                        : focused
                            ? new Color(0.85f, 0.75f, 0.6f, 1f) // light warm — controller hover
                            : Color.white;                       // natural sprite colors — inactive
            }
        }

        private static Sprite GetItemIcon(string prefabName)
        {
            if (ObjectDB.instance == null) return null;
            var prefab = ObjectDB.instance.GetItemPrefab(prefabName);
            var drop = prefab?.GetComponent<ItemDrop>();
            return drop?.m_itemData?.GetIcon();
        }

        private static Sprite FindSpriteByName(string spriteName)
        {
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
                if (s != null && s.name.Equals(spriteName, StringComparison.OrdinalIgnoreCase))
                    return s;
            return null;
        }

        private void BuildDescriptionColumn()
        {
            float btnH = _craftBtnHeight;

            // Item name header
            var nameGO = new GameObject("ItemName", typeof(RectTransform));
            nameGO.SetActive(false);
            nameGO.transform.SetParent(_middleColumn, false);
            _itemNameText = nameGO.AddComponent<TextMeshProUGUI>();
            if (_valheimFont != null) _itemNameText.font = _valheimFont;
            _itemNameText.fontSize = 24f;
            _itemNameText.color = Color.white;
            _itemNameText.alignment = TextAlignmentOptions.Center;
            _itemNameText.text = "";
            nameGO.SetActive(true);
            var nameRT = nameGO.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0f, 1f);
            nameRT.anchorMax = new Vector2(1f, 1f);
            nameRT.pivot = new Vector2(0.5f, 1f);
            nameRT.sizeDelta = new Vector2(-20f, 32f);
            nameRT.anchoredPosition = new Vector2(0f, -4f);

            // Coin display above action button
            var coinGO = new GameObject("CoinDisplay", typeof(RectTransform));
            coinGO.SetActive(false);
            coinGO.transform.SetParent(_middleColumn, false);
            _coinDisplayText = coinGO.AddComponent<TextMeshProUGUI>();
            if (_valheimFont != null) _coinDisplayText.font = _valheimFont;
            _coinDisplayText.fontSize = 24f;
            _coinDisplayText.color = GoldTextColor;
            _coinDisplayText.alignment = TextAlignmentOptions.Center;
            _coinDisplayText.text = "Coins: 0";
            coinGO.SetActive(true);
            var coinRT = coinGO.GetComponent<RectTransform>();
            coinRT.anchorMin = new Vector2(0f, 0f);
            coinRT.anchorMax = new Vector2(1f, 0f);
            coinRT.pivot = new Vector2(0.5f, 0f);
            coinRT.sizeDelta = new Vector2(-24f, 24f);
            coinRT.anchoredPosition = new Vector2(0f, btnH + 14f);

            // Action button at bottom
            if (_buttonTemplate != null)
            {
                var btnGO = Instantiate(_buttonTemplate, _middleColumn);
                btnGO.name = "ActionButton";
                btnGO.SetActive(true);
                _actionButton = btnGO.GetComponent<Button>();
                if (_actionButton != null)
                {
                    _actionButton.onClick.RemoveAllListeners();
                    _actionButton.onClick.AddListener(OnActionButtonClicked);
                    _actionButton.interactable = false;
                    _actionButton.navigation = new Navigation { mode = Navigation.Mode.None };
                    _actionButton.transition = Selectable.Transition.ColorTint;
                    _actionButton.colors = new ColorBlock
                    {
                        normalColor = Color.white,
                        highlightedColor = new Color(1f, 0.9f, 0.7f, 1f),
                        pressedColor = new Color(0.85f, 0.75f, 0.55f, 1f),
                        selectedColor = Color.white,
                        disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f),
                        colorMultiplier = 1f,
                        fadeDuration = 0.1f
                    };
                }
                _actionButtonLabel = btnGO.GetComponentInChildren<TMP_Text>(true);
                if (_actionButtonLabel != null)
                {
                    _actionButtonLabel.gameObject.SetActive(true);
                    _actionButtonLabel.text = "Select an item";
                }
                StripButtonHints(btnGO, _actionButtonLabel);

                // Grey tint overlay to match panel styling
                var tintGO = new GameObject("Tint", typeof(RectTransform), typeof(Image));
                tintGO.transform.SetParent(btnGO.transform, false);
                tintGO.transform.SetAsFirstSibling();
                var tintRT = tintGO.GetComponent<RectTransform>();
                tintRT.anchorMin = Vector2.zero;
                tintRT.anchorMax = Vector2.one;
                tintRT.offsetMin = Vector2.zero;
                tintRT.offsetMax = Vector2.zero;
                var tintImg = tintGO.GetComponent<Image>();
                tintImg.color = new Color(0f, 0f, 0f, 0.75f);
                tintImg.raycastTarget = false;

                var bRT = btnGO.GetComponent<RectTransform>();
                bRT.anchorMin = new Vector2(0f, 0f);
                bRT.anchorMax = new Vector2(1f, 0f);
                bRT.pivot = new Vector2(0.5f, 0f);
                bRT.sizeDelta = new Vector2(-24f, btnH);
                bRT.anchoredPosition = new Vector2(0f, 8f);
            }

            // Scrollable description area
            float descBottom = btnH + 44f; // above coin display + button
            var descScrollGO = new GameObject("DescScrollArea", typeof(RectTransform), typeof(Image), typeof(Mask));
            descScrollGO.transform.SetParent(_middleColumn, false);
            var dsRT = descScrollGO.GetComponent<RectTransform>();
            dsRT.anchorMin = Vector2.zero;
            dsRT.anchorMax = Vector2.one;
            dsRT.offsetMin = new Vector2(8f, descBottom);
            dsRT.offsetMax = new Vector2(-14f, -38f);
            descScrollGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.003f);
            descScrollGO.GetComponent<Mask>().showMaskGraphic = false;

            var descTextGO = new GameObject("DescText", typeof(RectTransform));
            descTextGO.SetActive(false);
            descTextGO.transform.SetParent(descScrollGO.transform, false);
            _itemDescText = descTextGO.AddComponent<TextMeshProUGUI>();
            if (_valheimFont != null) _itemDescText.font = _valheimFont;
            _itemDescText.fontSize = 18f;
            _itemDescText.color = Color.white;
            _itemDescText.alignment = TextAlignmentOptions.TopLeft;
            _itemDescText.textWrappingMode = TextWrappingModes.Normal;
            _itemDescText.overflowMode = TextOverflowModes.Overflow;
            _itemDescText.richText = true;
            _itemDescText.text = "";
            var dtRT = descTextGO.GetComponent<RectTransform>();
            dtRT.anchorMin = new Vector2(0f, 1f);
            dtRT.anchorMax = new Vector2(1f, 1f);
            dtRT.pivot = new Vector2(0.5f, 1f);
            dtRT.anchoredPosition = Vector2.zero;
            dtRT.sizeDelta = Vector2.zero;
            descTextGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            descTextGO.SetActive(true);

            var descSB = CreateHiddenScrollbar(_middleColumn);
            _descScrollRect = descScrollGO.AddComponent<ScrollRect>();
            _descScrollRect.content = dtRT;
            _descScrollRect.viewport = dsRT;
            _descScrollRect.vertical = true;
            _descScrollRect.horizontal = false;
            _descScrollRect.movementType = ScrollRect.MovementType.Clamped;
            _descScrollRect.scrollSensitivity = _scrollSensitivity * 8f;
            _descScrollRect.verticalScrollbar = descSB;
            _descScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        private void BuildPreviewColumn()
        {
            float colW = _rightColWidth;
            float colH = _panelHeight - _colTopInset - _bottomPad;
            int rtScale = 4;
            int rtW = Mathf.Max(64, Mathf.RoundToInt(colW) * rtScale);
            int rtH = Mathf.Max(64, Mathf.RoundToInt(colH) * rtScale);

            // Player preview RT + camera
            _playerPreviewRT = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.ARGB32);
            _playerPreviewRT.antiAliasing = 4;
            _playerPreviewRT.filterMode = FilterMode.Trilinear;

            _playerCamGO = new GameObject("TraderUI_PlayerCam");
            DontDestroyOnLoad(_playerCamGO);
            _playerCam = _playerCamGO.AddComponent<Camera>();
            _playerCam.targetTexture = _playerPreviewRT;
            _playerCam.clearFlags = CameraClearFlags.SolidColor;
            _playerCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _playerCam.fieldOfView = 30f;
            _playerCam.nearClipPlane = 0.1f;
            _playerCam.farClipPlane = 10f;
            _playerCam.depth = -2;
            _playerCam.enabled = false;
            int charLayer = LayerMask.NameToLayer("character");
            if (charLayer < 0) charLayer = 9;
            int charNet = LayerMask.NameToLayer("character_net");
            int mask = (1 << charLayer);
            if (charNet >= 0) mask |= (1 << charNet);
            _playerCam.cullingMask = mask;

            // Trader preview RT + camera
            _haldorPreviewRT = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.ARGB32);
            _haldorPreviewRT.antiAliasing = 4;
            _haldorPreviewRT.filterMode = FilterMode.Trilinear;

            _haldorCamGO = new GameObject("TraderUI_TraderCam");
            DontDestroyOnLoad(_haldorCamGO);
            _haldorCam = _haldorCamGO.AddComponent<Camera>();
            _haldorCam.targetTexture = _haldorPreviewRT;
            _haldorCam.clearFlags = CameraClearFlags.SolidColor;
            _haldorCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _haldorCam.fieldOfView = TraderIdentity.GetPreviewProfile(TraderKind.Unknown).CameraFov;
            _haldorCam.nearClipPlane = 0.1f;
            _haldorCam.farClipPlane = 10f;
            _haldorCam.depth = -2;
            _haldorCam.enabled = false;
            _haldorCam.cullingMask = mask;

            // Player RawImage
            var playerImgGO = new GameObject("PlayerPreview", typeof(RectTransform));
            playerImgGO.transform.SetParent(_rightColumn, false);
            var pRT = playerImgGO.GetComponent<RectTransform>();
            pRT.anchorMin = Vector2.zero; pRT.anchorMax = Vector2.one;
            pRT.offsetMin = Vector2.zero; pRT.offsetMax = Vector2.zero;
            _playerPreviewImg = playerImgGO.AddComponent<RawImage>();
            _playerPreviewImg.texture = _playerPreviewRT;
            _playerPreviewImg.color = Color.white;
            _playerPreviewImg.raycastTarget = false;

            // Trader RawImage
            var haldorImgGO = new GameObject("TraderPreview", typeof(RectTransform));
            haldorImgGO.transform.SetParent(_rightColumn, false);
            var hRT = haldorImgGO.GetComponent<RectTransform>();
            hRT.anchorMin = Vector2.zero; hRT.anchorMax = Vector2.one;
            hRT.offsetMin = Vector2.zero; hRT.offsetMax = Vector2.zero;
            _haldorPreviewImg = haldorImgGO.AddComponent<RawImage>();
            _haldorPreviewImg.texture = _haldorPreviewRT;
            _haldorPreviewImg.color = Color.white;
            _haldorPreviewImg.raycastTarget = false;
        }

        // ══════════════════════════════════════════
        //  UI HELPERS
        // ══════════════════════════════════════════

        private void ApplyPanelStyle(Image img)
        {
            if (img == null) return;
            img.sprite = null;
            img.color = new Color(0f, 0f, 0f, 0.75f);
        }

        private Scrollbar CreateHiddenScrollbar(Transform parent)
        {
            float sbW = 10f;
            var sbGO = new GameObject("Scrollbar", typeof(RectTransform));
            sbGO.transform.SetParent(parent, false);
            var sbRT = sbGO.GetComponent<RectTransform>();
            sbRT.anchorMin = new Vector2(1f, 0f);
            sbRT.anchorMax = new Vector2(1f, 1f);
            sbRT.pivot = new Vector2(1f, 0.5f);
            sbRT.sizeDelta = new Vector2(sbW, 0f);
            sbRT.offsetMin = new Vector2(-sbW, 4f);
            sbRT.offsetMax = new Vector2(-2f, -4f);
            sbGO.AddComponent<Image>().color = Color.clear;

            var slidingGO = new GameObject("Sliding Area", typeof(RectTransform));
            slidingGO.transform.SetParent(sbGO.transform, false);
            var sRT = slidingGO.GetComponent<RectTransform>();
            sRT.anchorMin = Vector2.zero; sRT.anchorMax = Vector2.one;
            sRT.offsetMin = Vector2.zero; sRT.offsetMax = Vector2.zero;

            var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGO.transform.SetParent(slidingGO.transform, false);
            var hRT = handleGO.GetComponent<RectTransform>();
            hRT.anchorMin = Vector2.zero; hRT.anchorMax = Vector2.one;
            hRT.offsetMin = Vector2.zero; hRT.offsetMax = Vector2.zero;
            handleGO.GetComponent<Image>().color = Color.clear;

            var sb = sbGO.AddComponent<Scrollbar>();
            sb.handleRect = hRT;
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.targetGraphic = handleGO.GetComponent<Image>();
            return sb;
        }

        private GameObject CreateTabButton(string label, int tabIndex, float centerX, float width, float topGap)
        {
            var go = Instantiate(_buttonTemplate, _mainPanel.transform);
            go.name = "Tab_" + label;
            go.SetActive(true);
            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                int idx = tabIndex;
                btn.onClick.AddListener(() => SwitchTab(idx));
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            var txt = go.GetComponentInChildren<TMP_Text>(true);
            if (txt != null) { txt.text = label; txt.gameObject.SetActive(true); }
            StripButtonHints(go, txt);
            var tabRT = go.GetComponent<RectTransform>();
            tabRT.anchorMin = new Vector2(0f, 1f);
            tabRT.anchorMax = new Vector2(0f, 1f);
            tabRT.pivot = new Vector2(0.5f, 1f);
            tabRT.sizeDelta = new Vector2(width, _tabBtnHeight);
            tabRT.anchoredPosition = new Vector2(centerX, -topGap);

            // Hover effect: brighten current color on enter, restore via RefreshTabHighlights on exit
            var tabImg = go.GetComponent<Image>();
            if (tabImg != null)
            {
                var trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
                trigger.triggers.Clear();
                var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener((_) =>
                {
                    if (tabImg != null)
                    {
                        Color c = tabImg.color;
                        tabImg.color = new Color(
                            Mathf.Min(c.r + 0.15f, 1f),
                            Mathf.Min(c.g + 0.15f, 1f),
                            Mathf.Min(c.b + 0.15f, 1f), c.a);
                    }
                });
                trigger.triggers.Add(enterEntry);
                var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exitEntry.callback.AddListener((_) => { RefreshTabHighlights(); });
                trigger.triggers.Add(exitEntry);
            }

            return go;
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

        private static void StripLayoutComponents(GameObject go)
        {
            foreach (var c in go.GetComponents<LayoutGroup>()) DestroyImmediate(c);
            foreach (var c in go.GetComponents<ContentSizeFitter>()) DestroyImmediate(c);
            foreach (var c in go.GetComponents<LayoutElement>()) DestroyImmediate(c);
        }

        /// <summary>
        /// Replaces any vanilla EventTrigger entries with PointerEnter/Exit hover effect
        /// that brightens the element's background Image color.
        /// </summary>
        private static void AddHoverEffect(GameObject go, Color baseColor, float brighten = 0.15f)
        {
            var img = go.GetComponent<Image>();
            if (img == null) return;

            var trigger = go.GetComponent<EventTrigger>();
            if (trigger != null)
                trigger.triggers.Clear();
            else
                trigger = go.AddComponent<EventTrigger>();

            Color hoverColor = new Color(
                Mathf.Min(baseColor.r + brighten, 1f),
                Mathf.Min(baseColor.g + brighten, 1f),
                Mathf.Min(baseColor.b + brighten, 1f),
                Mathf.Min(baseColor.a + 0.08f, 1f)
            );

            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) => { if (img != null) img.color = hoverColor; });
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) => { if (img != null) img.color = baseColor; });
            trigger.triggers.Add(exitEntry);
        }

        // ══════════════════════════════════════════
        //  TAB MANAGEMENT
        // ══════════════════════════════════════════

        private void SwitchTab(int newTab)
        {
            newTab = Mathf.Clamp(newTab, 0, 2);
            if (newTab == _activeTab) return;
            _activeTab = newTab;
            _searchFilter = "";
            if (_searchInput != null) _searchInput.text = "";
            _activeCategoryFilter = null;
            _joyCategoryFocusIndex = -1;
            _lastJoyCategoryFocusIndex = -1;
            _buyRarityCollapsed.Clear();
            _sellRarityCollapsed.Clear();
            UpdateCategoryFilterVisuals();
            RefreshTabHighlights();
            RefreshTabPanels();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void RefreshTabHighlights()
        {
            var tabs = new[] { _tabBuy, _tabSell, _tabBank };
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                var btn = tabs[i].GetComponent<Button>();
                if (btn == null) continue;
                btn.interactable = true;
                btn.transition = Selectable.Transition.None;
                var img = tabs[i].GetComponent<Image>();
                if (img != null)
                    img.color = (i == _activeTab) ? GoldColor : new Color(0.45f, 0.45f, 0.45f, 1f);
            }
        }

        private void RefreshTabPanels()
        {
            bool isBuy = (_activeTab == 0);
            bool isSell = (_activeTab == 1);
            bool isBank = (_activeTab == 2);

            // Show/hide the 3-column layout vs bank panel
            if (_leftColumn != null) _leftColumn.gameObject.SetActive(!isBank);
            if (_middleColumn != null) _middleColumn.gameObject.SetActive(!isBank);
            if (_rightColumn != null) _rightColumn.gameObject.SetActive(!isBank);
            if (_bankContentPanel != null) _bankContentPanel.SetActive(isBank);

            if (isBank)
            {
                LoadBankBalance();
                _bankFocusedButton = 0;
                RefreshBankDisplay();
                return;
            }

            // Toggle preview visibility
            if (_playerPreviewImg != null) _playerPreviewImg.gameObject.SetActive(isBuy);
            if (_haldorPreviewImg != null) _haldorPreviewImg.gameObject.SetActive(isSell);

            PopulateCurrentList();
        }

        // ══════════════════════════════════════════
        //  DATA BUILDING
        // ══════════════════════════════════════════

        private void BuildBuyEntries()
        {
            _allBuyEntries.Clear();

            // Hildir/Bog Witch always keep their vanilla stock, even if not listed in config.
            if (TraderIdentity.KeepsVanillaBuyStock(_currentTraderKind))
            {
                AddVanillaTraderBuyEntries();
            }

            // All traders consume the shared buy config; config entries override duplicate vanilla entries.
            AddConfiguredBuyEntries();
        }

        private void AddConfiguredBuyEntries()
        {
            if (ObjectDB.instance == null) return;

            foreach (var entry in ConfigLoader.GetBuyEntries())
            {
                if (string.IsNullOrEmpty(entry.prefab)) continue;
                if (!HasGlobalKey(entry.requiredGlobalKey)) continue;

                GameObject prefab = ObjectDB.instance.GetItemPrefab(entry.prefab);
                if (prefab == null) continue;

                var drop = prefab.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null) continue;

                string itemName = Localize(drop.m_itemData.m_shared.m_name);
                string desc = Localize(drop.m_itemData.m_shared.m_description);
                Sprite icon = null;
                try { icon = drop.m_itemData.GetIcon(); } catch { }

                UpsertBuyEntry(new BuyEntry
                {
                    PrefabName = entry.prefab,
                    Name = itemName,
                    Description = desc,
                    Price = Mathf.Max(0, entry.price),
                    Stack = Mathf.Max(1, entry.stack),
                    Category = GetItemCategory(drop.m_itemData),
                    Icon = icon,
                    Rarity = entry.rarity ?? ""
                }, overwriteExisting: true);
            }
        }

        private void AddVanillaTraderBuyEntries()
        {
            if (_currentTrader == null || _currentTrader.m_items == null) return;

            foreach (var trade in _currentTrader.m_items)
            {
                if (trade == null || trade.m_prefab == null || trade.m_prefab.m_itemData == null) continue;
                if (!HasGlobalKey(trade.m_requiredGlobalKey)) continue;

                var drop = trade.m_prefab;
                string prefabName = drop.gameObject != null ? drop.gameObject.name : null;
                if (string.IsNullOrEmpty(prefabName)) continue;
                if (prefabName.EndsWith("(Clone)", StringComparison.Ordinal))
                    prefabName = prefabName.Replace("(Clone)", "").Trim();

                string itemName = Localize(drop.m_itemData.m_shared.m_name);
                string desc = Localize(drop.m_itemData.m_shared.m_description);
                Sprite icon = null;
                try { icon = drop.m_itemData.GetIcon(); } catch { }

                UpsertBuyEntry(new BuyEntry
                {
                    PrefabName = prefabName,
                    Name = itemName,
                    Description = desc,
                    Price = Mathf.Max(0, trade.m_price),
                    Stack = Mathf.Max(1, trade.m_stack),
                    Category = GetItemCategory(drop.m_itemData),
                    Icon = icon
                }, overwriteExisting: false);
            }
        }

        private void UpsertBuyEntry(BuyEntry entry, bool overwriteExisting)
        {
            if (entry == null || string.IsNullOrEmpty(entry.PrefabName)) return;

            int idx = _allBuyEntries.FindIndex(e =>
                string.Equals(e.PrefabName, entry.PrefabName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.Rarity ?? "", entry.Rarity ?? "", StringComparison.OrdinalIgnoreCase));

            if (idx < 0)
            {
                _allBuyEntries.Add(entry);
                return;
            }

            if (overwriteExisting)
                _allBuyEntries[idx] = entry;
        }

        private void BuildSellEntries()
        {
            _allSellEntries.Clear();

            var player = Player.m_localPlayer;
            if (player == null) return;

            var inv = ((Humanoid)player).GetInventory();
            if (inv == null) return;

            string coinName = _currentStoreGui?.m_coinPrefab?.m_itemData?.m_shared?.m_name;
            var sellConfigs = ConfigLoader.GetSellEntries()
                .Where(e => !string.IsNullOrEmpty(e.prefab))
                .GroupBy(e => (e.prefab.ToLowerInvariant(), (e.rarity ?? "").ToLowerInvariant()))
                .Select(g => g.First())
                .ToList();

            // Build lookup: prefab → list of sell configs (one per rarity)
            var sellByPrefab = new Dictionary<string, List<TradeEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in sellConfigs)
            {
                if (!sellByPrefab.TryGetValue(e.prefab, out var list))
                {
                    list = new List<TradeEntry>();
                    sellByPrefab[e.prefab] = list;
                }
                list.Add(e);
            }

            foreach (var item in inv.GetAllItems())
            {
                if (item == null || item.m_shared == null) continue;
                if (!string.IsNullOrEmpty(coinName) && item.m_shared.m_name == coinName) continue;

                string prefabName = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
                if (string.IsNullOrEmpty(prefabName))
                {
                    string sn = item.m_shared.m_name;
                    if (!string.IsNullOrEmpty(sn))
                    {
                        if (sn.StartsWith("$item_")) prefabName = sn.Substring(6);
                        else if (sn.StartsWith("$")) prefabName = sn.Substring(1);
                        else prefabName = sn;
                    }
                }

                if (string.IsNullOrEmpty(prefabName)) continue;
                if (!sellByPrefab.TryGetValue(prefabName, out var configs)) continue;

                // Find the matching rarity config for this item
                string itemRarity = GetItemRarity(item);
                TradeEntry config = configs.Find(c =>
                    string.Equals(c.rarity ?? "", itemRarity, StringComparison.OrdinalIgnoreCase))
                    ?? configs.Find(c => string.IsNullOrEmpty(c.rarity));
                if (config == null) continue;
                if (item.m_stack < config.stack) continue;
                if (config.price <= 0) continue;

                // Use the item's actual rarity (detected from Epic Loot data) and scale price
                int sellPrice = config.price;
                if (!string.IsNullOrEmpty(itemRarity) && string.IsNullOrEmpty(config.rarity))
                    sellPrice = (int)(config.price * GetRaritySellMultiplier(itemRarity));

                string name = Localize(item.m_shared.m_name);
                Sprite icon = null;
                try { icon = item.GetIcon(); } catch { }

                _allSellEntries.Add(new SellEntry
                {
                    Item = item,
                    PrefabName = prefabName,
                    Name = name,
                    Price = sellPrice,
                    ConfigStack = config.stack,
                    Category = GetItemCategory(item),
                    Icon = icon,
                    Rarity = !string.IsNullOrEmpty(itemRarity) ? itemRarity : (config.rarity ?? "")
                });
            }

            _lastInventoryHash = CalculateInventoryHash(inv.GetAllItems());
        }

        // ══════════════════════════════════════════
        //  LIST POPULATION
        // ══════════════════════════════════════════

        private void PopulateCurrentList(bool autoSelect = true)
        {
            ClearListElements();

            if (_activeTab == 0)
            {
                PopulateBuyListUI();
                if (autoSelect && _selectedBuyIndex < 0 && GetVisibleBuyCount() > 0)
                    SelectBuyItem(0);
                else
                    RefreshDescription();
            }
            else if (_activeTab == 1)
            {
                PopulateSellListUI();
                if (autoSelect && _selectedSellIndex < 0 && GetVisibleSellCount() > 0)
                    SelectSellItem(0);
                else
                    RefreshDescription();
            }
            // tab 2 (Bank): handled by RefreshTabPanels directly
        }

        private void ClearListElements()
        {
            foreach (var (go, _) in _listElements) if (go != null) Destroy(go);
            _listElements.Clear();
            foreach (var go in _categoryHeaders) if (go != null) Destroy(go);
            _categoryHeaders.Clear();
        }

        private void PopulateBuyListUI()
        {
            if (_listRoot == null || _recipeElementPrefab == null) return;

            var entries = GetFilteredBuyEntries();
            bool searchActive = !string.IsNullOrEmpty(_searchFilter);

            var templateRT = _recipeElementPrefab.transform as RectTransform;
            float templateH = templateRT != null ? Mathf.Max(24f, templateRT.rect.height) : 32f;
            float rowH = Mathf.Max(templateH, 32f);
            float spacing = rowH + 2f;

            StripLayoutComponents(_listRoot.gameObject);

            // Group by category
            var grouped = new Dictionary<string, List<int>>();
            for (int i = 0; i < entries.Count; i++)
            {
                string cat = entries[i].Category;
                if (!grouped.ContainsKey(cat)) grouped[cat] = new List<int>();
                grouped[cat].Add(i);
            }

            int visualIndex = 0;

            foreach (string cat in CategoryBuckets)
            {
                if (!grouped.ContainsKey(cat)) continue;
                var indices = grouped[cat];

                bool collapsed = _buyCategoryCollapsed.TryGetValue(cat, out bool c) && c;

                // Category header (skip if searching)
                if (!searchActive)
                {
                    var header = CreateCategoryHeader(cat, collapsed, false, visualIndex, spacing);
                    if (header != null)
                    {
                        _categoryHeaders.Add(header);
                        visualIndex++;
                    }
                }

                if (searchActive || !collapsed)
                {
                    // Check if this category has any rarity items
                    bool hasRarityItems = false;
                    for (int k = 0; k < indices.Count; k++)
                    {
                        if (!string.IsNullOrEmpty(entries[indices[k]].Rarity)) { hasRarityItems = true; break; }
                    }

                    if (hasRarityItems && !searchActive)
                    {
                        // Sub-group by rarity
                        var byRarity = new Dictionary<string, List<int>>();
                        foreach (int idx in indices)
                        {
                            string r = entries[idx].Rarity ?? "";
                            if (!byRarity.ContainsKey(r)) byRarity[r] = new List<int>();
                            byRarity[r].Add(idx);
                        }

                        foreach (string rarity in RarityOrder)
                        {
                            if (!byRarity.ContainsKey(rarity)) continue;
                            var rarityIndices = byRarity[rarity];

                            string collapseKey = cat + "|" + rarity;
                            bool rarityCollapsed = _buyRarityCollapsed.TryGetValue(collapseKey, out bool rc) && rc;

                            var subHeader = CreateRaritySubHeader(cat, rarity, rarityCollapsed, false, visualIndex, spacing);
                            if (subHeader != null)
                            {
                                _categoryHeaders.Add(subHeader);
                                visualIndex++;
                            }

                            if (!rarityCollapsed)
                            {
                                foreach (int idx in rarityIndices)
                                {
                                    var element = CreateBuyListEntry(entries[idx], idx, visualIndex, rowH, spacing);
                                    if (element != null)
                                    {
                                        _listElements.Add((element, idx));
                                        visualIndex++;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Flat list (no rarity items or searching)
                        foreach (int idx in indices)
                        {
                            var element = CreateBuyListEntry(entries[idx], idx, visualIndex, rowH, spacing);
                            if (element != null)
                            {
                                _listElements.Add((element, idx));
                                visualIndex++;
                            }
                        }
                    }
                }
            }

            float contentH = visualIndex * spacing + 4f;
            _listRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(contentH, 100f));
            LayoutRebuilder.ForceRebuildLayoutImmediate(_listRoot);
        }

        private void PopulateSellListUI()
        {
            if (_listRoot == null || _recipeElementPrefab == null) return;

            var entries = GetFilteredSellEntries();
            bool searchActive = !string.IsNullOrEmpty(_searchFilter);

            var templateRT = _recipeElementPrefab.transform as RectTransform;
            float templateH = templateRT != null ? Mathf.Max(24f, templateRT.rect.height) : 32f;
            float rowH = Mathf.Max(templateH, 32f);
            float spacing = rowH + 2f;

            StripLayoutComponents(_listRoot.gameObject);

            var grouped = new Dictionary<string, List<int>>();
            for (int i = 0; i < entries.Count; i++)
            {
                string cat = entries[i].Category;
                if (!grouped.ContainsKey(cat)) grouped[cat] = new List<int>();
                grouped[cat].Add(i);
            }

            int visualIndex = 0;

            foreach (string cat in CategoryBuckets)
            {
                if (!grouped.ContainsKey(cat)) continue;
                var indices = grouped[cat];

                bool collapsed = _sellCategoryCollapsed.TryGetValue(cat, out bool c) && c;

                if (!searchActive)
                {
                    var header = CreateCategoryHeader(cat, collapsed, true, visualIndex, spacing);
                    if (header != null)
                    {
                        _categoryHeaders.Add(header);
                        visualIndex++;
                    }
                }

                if (searchActive || !collapsed)
                {
                    bool hasRarityItems = false;
                    for (int k = 0; k < indices.Count; k++)
                    {
                        if (!string.IsNullOrEmpty(entries[indices[k]].Rarity)) { hasRarityItems = true; break; }
                    }

                    if (hasRarityItems && !searchActive)
                    {
                        var byRarity = new Dictionary<string, List<int>>();
                        foreach (int idx in indices)
                        {
                            string r = entries[idx].Rarity ?? "";
                            if (!byRarity.ContainsKey(r)) byRarity[r] = new List<int>();
                            byRarity[r].Add(idx);
                        }

                        foreach (string rarity in RarityOrder)
                        {
                            if (!byRarity.ContainsKey(rarity)) continue;
                            var rarityIndices = byRarity[rarity];

                            string collapseKey = cat + "|" + rarity;
                            bool rarityCollapsed = _sellRarityCollapsed.TryGetValue(collapseKey, out bool rc) && rc;

                            var subHeader = CreateRaritySubHeader(cat, rarity, rarityCollapsed, true, visualIndex, spacing);
                            if (subHeader != null)
                            {
                                _categoryHeaders.Add(subHeader);
                                visualIndex++;
                            }

                            if (!rarityCollapsed)
                            {
                                foreach (int idx in rarityIndices)
                                {
                                    var element = CreateSellListEntry(entries[idx], idx, visualIndex, rowH, spacing);
                                    if (element != null)
                                    {
                                        _listElements.Add((element, idx));
                                        visualIndex++;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (int idx in indices)
                        {
                            var element = CreateSellListEntry(entries[idx], idx, visualIndex, rowH, spacing);
                            if (element != null)
                            {
                                _listElements.Add((element, idx));
                                visualIndex++;
                            }
                        }
                    }
                }
            }

            float contentH = visualIndex * spacing + 4f;
            _listRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(contentH, 100f));
            LayoutRebuilder.ForceRebuildLayoutImmediate(_listRoot);
        }

        private GameObject CreateBuyListEntry(BuyEntry entry, int dataIndex, int visualIndex, float rowH, float spacing)
        {
            var element = Instantiate(_recipeElementPrefab, _listRoot);
            element.SetActive(true);
            element.name = "BuyItem_" + entry.PrefabName;

            var elemRT = element.transform as RectTransform;
            StripLayoutComponents(element);
            elemRT.anchorMin = new Vector2(0f, 1f);
            elemRT.anchorMax = new Vector2(1f, 1f);
            elemRT.pivot = new Vector2(0.5f, 1f);
            elemRT.anchoredPosition = new Vector2(0f, visualIndex * -spacing);
            elemRT.sizeDelta = new Vector2(0f, rowH);

            Color bgColor = new Color(0f, 0f, 0f, 0.25f);

            var elemImg = element.GetComponent<Image>();
            if (elemImg != null)
                elemImg.color = bgColor;

            AddHoverEffect(element, bgColor);

            // Icon
            var iconTr = element.transform.Find("icon");
            if (iconTr != null)
            {
                if (entry.Icon != null)
                {
                    var iconImg = iconTr.GetComponent<Image>();
                    if (iconImg != null) { iconImg.sprite = entry.Icon; iconImg.color = Color.white; iconImg.preserveAspect = true; }
                    iconTr.gameObject.SetActive(true);

                    // Rarity color background behind icon — parented to row, placed before icon in sibling order
                    if (!string.IsNullOrEmpty(entry.Rarity) && ColorUtility.TryParseHtmlString(GetRarityHexColor(entry.Rarity), out Color iconBgColor))
                    {
                        var iconBgGO = new GameObject("rarityBg", typeof(RectTransform), typeof(Image));
                        iconBgGO.transform.SetParent(element.transform, false);
                        int iconSibIdx = iconTr.GetSiblingIndex();
                        iconBgGO.transform.SetSiblingIndex(iconSibIdx);
                        var iconRT = iconTr as RectTransform;
                        var iconBgRT = iconBgGO.GetComponent<RectTransform>();
                        iconBgRT.anchorMin = iconRT.anchorMin;
                        iconBgRT.anchorMax = iconRT.anchorMax;
                        iconBgRT.pivot = iconRT.pivot;
                        iconBgRT.anchoredPosition = iconRT.anchoredPosition;
                        iconBgRT.sizeDelta = iconRT.sizeDelta + new Vector2(4f, 4f);
                        var iconBgImg = iconBgGO.GetComponent<Image>();
                        iconBgColor.a = 0.55f;
                        iconBgImg.color = iconBgColor;
                    }
                }
                else iconTr.gameObject.SetActive(false);
            }

            // Name
            var nameTr = element.transform.Find("name");
            if (nameTr != null)
            {
                var nameRT = nameTr as RectTransform;
                if (nameRT != null)
                {
                    nameRT.anchorMin = new Vector2(0f, 0f);
                    nameRT.anchorMax = new Vector2(0.7f, 1f);
                    nameRT.offsetMin = new Vector2(36f, 0f);
                    nameRT.offsetMax = new Vector2(0f, 0f);
                }
                var nameTxt = nameTr.GetComponent<TMP_Text>();
                if (nameTxt != null)
                {
                    string text = entry.Name;
                    if (entry.Stack > 1) text += $" x{entry.Stack}";
                    if (!string.IsNullOrEmpty(entry.Rarity))
                        text += $" <size=14><color={GetRarityHexColor(entry.Rarity)}>({entry.Rarity})</color></size>";
                    nameTxt.text = text;
                    nameTxt.richText = true;
                    nameTxt.color = Color.white;
                    nameTxt.fontSize = 19f;
                    nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
                }
            }

            // Price text — created inactive so TMP Awake is deferred, letting us assign the
            // Valheim font before Awake runs and before it falls back to LiberationSans.
            var priceGO = new GameObject("PriceText");
            priceGO.SetActive(false);
            priceGO.AddComponent<RectTransform>();
            priceGO.transform.SetParent(element.transform, false);
            var priceTxt = priceGO.AddComponent<TextMeshProUGUI>();
            var buyPriceFont = _valheimFont ?? element.transform.Find("name")?.GetComponent<TMP_Text>()?.font;
            if (buyPriceFont != null) priceTxt.font = buyPriceFont;
            priceTxt.text = entry.Price.ToString("N0");
            priceTxt.fontSize = 19f;
            priceTxt.color = GoldTextColor;
            priceTxt.alignment = TextAlignmentOptions.MidlineRight;
            var priceRT = priceGO.GetComponent<RectTransform>();
            priceRT.anchorMin = new Vector2(0.7f, 0f);
            priceRT.anchorMax = new Vector2(1f, 1f);
            priceRT.offsetMin = new Vector2(0f, 0f);
            priceRT.offsetMax = new Vector2(-8f, 0f);
            priceGO.SetActive(true); // Awake now runs — font already set, no LiberationSans fallback

            // Hide unused children
            var durTr = element.transform.Find("Durability");
            if (durTr != null) durTr.gameObject.SetActive(false);
            var qualTr = element.transform.Find("QualityLevel");
            if (qualTr != null) qualTr.gameObject.SetActive(false);

            // Selected highlight
            var selTr = element.transform.Find("selected");
            if (selTr != null)
            {
                selTr.gameObject.SetActive(false);
                var selImg = selTr.GetComponent<Image>();
                if (selImg != null) selImg.color = new Color(0.83f, 0.64f, 0.31f, 0.5f);
            }

            // Click handler
            var btn = element.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                int captured = dataIndex;
                btn.onClick.AddListener(() =>
                {
                    SelectBuyItem(captured);
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                });
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            return element;
        }

        private GameObject CreateSellListEntry(SellEntry entry, int dataIndex, int visualIndex, float rowH, float spacing)
        {
            var element = Instantiate(_recipeElementPrefab, _listRoot);
            element.SetActive(true);
            element.name = "SellItem_" + entry.PrefabName;

            var elemRT = element.transform as RectTransform;
            StripLayoutComponents(element);
            elemRT.anchorMin = new Vector2(0f, 1f);
            elemRT.anchorMax = new Vector2(1f, 1f);
            elemRT.pivot = new Vector2(0.5f, 1f);
            elemRT.anchoredPosition = new Vector2(0f, visualIndex * -spacing);
            elemRT.sizeDelta = new Vector2(0f, rowH);

            Color bgColor = new Color(0f, 0f, 0f, 0.25f);

            var elemImg = element.GetComponent<Image>();
            if (elemImg != null)
                elemImg.color = bgColor;

            AddHoverEffect(element, bgColor);

            var iconTr = element.transform.Find("icon");
            if (iconTr != null)
            {
                if (entry.Icon != null)
                {
                    var iconImg = iconTr.GetComponent<Image>();
                    if (iconImg != null) { iconImg.sprite = entry.Icon; iconImg.color = Color.white; iconImg.preserveAspect = true; }
                    iconTr.gameObject.SetActive(true);

                    // Rarity color background behind icon — parented to row, placed before icon in sibling order
                    if (!string.IsNullOrEmpty(entry.Rarity) && ColorUtility.TryParseHtmlString(GetRarityHexColor(entry.Rarity), out Color iconBgColor))
                    {
                        var iconBgGO = new GameObject("rarityBg", typeof(RectTransform), typeof(Image));
                        iconBgGO.transform.SetParent(element.transform, false);
                        int iconSibIdx = iconTr.GetSiblingIndex();
                        iconBgGO.transform.SetSiblingIndex(iconSibIdx);
                        var iconRT = iconTr as RectTransform;
                        var iconBgRT = iconBgGO.GetComponent<RectTransform>();
                        iconBgRT.anchorMin = iconRT.anchorMin;
                        iconBgRT.anchorMax = iconRT.anchorMax;
                        iconBgRT.pivot = iconRT.pivot;
                        iconBgRT.anchoredPosition = iconRT.anchoredPosition;
                        iconBgRT.sizeDelta = iconRT.sizeDelta + new Vector2(4f, 4f);
                        var iconBgImg = iconBgGO.GetComponent<Image>();
                        iconBgColor.a = 0.55f;
                        iconBgImg.color = iconBgColor;
                    }
                }
                else iconTr.gameObject.SetActive(false);
            }

            var nameTr = element.transform.Find("name");
            if (nameTr != null)
            {
                var nameRT = nameTr as RectTransform;
                if (nameRT != null)
                {
                    nameRT.anchorMin = new Vector2(0f, 0f);
                    nameRT.anchorMax = new Vector2(0.7f, 1f);
                    nameRT.offsetMin = new Vector2(36f, 0f);
                    nameRT.offsetMax = new Vector2(0f, 0f);
                }
                var nameTxt = nameTr.GetComponent<TMP_Text>();
                if (nameTxt != null)
                {
                    string text = entry.Name;
                    if (entry.ConfigStack > 1) text += $" x{entry.ConfigStack}";
                    if (!string.IsNullOrEmpty(entry.Rarity))
                        text += $" <size=14><color={GetRarityHexColor(entry.Rarity)}>({entry.Rarity})</color></size>";
                    nameTxt.text = text;
                    nameTxt.richText = true;
                    nameTxt.color = Color.white;
                    nameTxt.fontSize = 19f;
                    nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
                }
            }

            var priceGO = new GameObject("PriceText");
            priceGO.SetActive(false);
            priceGO.AddComponent<RectTransform>();
            priceGO.transform.SetParent(element.transform, false);
            var priceTxt = priceGO.AddComponent<TextMeshProUGUI>();
            var sellPriceFont = _valheimFont ?? element.transform.Find("name")?.GetComponent<TMP_Text>()?.font;
            if (sellPriceFont != null) priceTxt.font = sellPriceFont;
            priceTxt.text = entry.Price.ToString();
            priceTxt.fontSize = 19f;
            priceTxt.color = GoldTextColor;
            priceTxt.alignment = TextAlignmentOptions.MidlineRight;
            var priceRT = priceGO.GetComponent<RectTransform>();
            priceRT.anchorMin = new Vector2(0.7f, 0f);
            priceRT.anchorMax = new Vector2(1f, 1f);
            priceRT.offsetMin = new Vector2(0f, 0f);
            priceRT.offsetMax = new Vector2(-8f, 0f);
            priceGO.SetActive(true);

            var durTr = element.transform.Find("Durability");
            if (durTr != null) durTr.gameObject.SetActive(false);
            var qualTr = element.transform.Find("QualityLevel");
            if (qualTr != null) qualTr.gameObject.SetActive(false);

            var selTr = element.transform.Find("selected");
            if (selTr != null)
            {
                selTr.gameObject.SetActive(false);
                var selImg = selTr.GetComponent<Image>();
                if (selImg != null) selImg.color = new Color(0.83f, 0.64f, 0.31f, 0.5f);
            }

            var btn = element.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                int captured = dataIndex;
                btn.onClick.AddListener(() =>
                {
                    SelectSellItem(captured);
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                });
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            return element;
        }

        private GameObject CreateCategoryHeader(string category, bool collapsed, bool isSell, int visualIndex, float spacing)
        {
            if (_recipeElementPrefab == null || _listRoot == null) return null;

            var header = Instantiate(_recipeElementPrefab, _listRoot);
            header.SetActive(true);
            header.name = (isSell ? "SellCat_" : "BuyCat_") + category;

            var hRT = header.transform as RectTransform;
            StripLayoutComponents(header);
            hRT.anchorMin = new Vector2(0f, 1f);
            hRT.anchorMax = new Vector2(1f, 1f);
            hRT.pivot = new Vector2(0.5f, 1f);
            hRT.anchoredPosition = new Vector2(0f, visualIndex * -spacing);
            hRT.sizeDelta = new Vector2(0f, spacing - 2f);

            var bgImg = header.GetComponent<Image>();
            if (bgImg != null) bgImg.color = CategoryHeaderBg;
            AddHoverEffect(header, CategoryHeaderBg);

            // Hide icon
            var iconTr = header.transform.Find("icon");
            if (iconTr != null) iconTr.gameObject.SetActive(false);

            // Category name
            var nameTr = header.transform.Find("name");
            if (nameTr != null)
            {
                var nameRT = nameTr as RectTransform;
                if (nameRT != null)
                {
                    nameRT.anchorMin = Vector2.zero;
                    nameRT.anchorMax = Vector2.one;
                    nameRT.offsetMin = new Vector2(10f, 0f);
                    nameRT.offsetMax = new Vector2(-10f, 0f);
                }
                var nameTxt = nameTr.GetComponent<TMP_Text>();
                if (nameTxt != null)
                {
                    nameTxt.text = category;
                    nameTxt.color = CategoryHeaderText;
                    nameTxt.fontStyle = FontStyles.Bold;
                    nameTxt.fontSize = 18f;
                    nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
                }
            }

            // Hide unused
            var durTr = header.transform.Find("Durability");
            if (durTr != null) durTr.gameObject.SetActive(false);
            var qualTr = header.transform.Find("QualityLevel");
            if (qualTr != null) qualTr.gameObject.SetActive(false);
            var selTr = header.transform.Find("selected");
            if (selTr != null) selTr.gameObject.SetActive(false);

            // Click to toggle
            var btn = header.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                string cat = category;
                btn.onClick.AddListener(() =>
                {
                    if (isSell)
                    {
                        _sellCategoryCollapsed[cat] = !(_sellCategoryCollapsed.TryGetValue(cat, out bool cv) && cv);
                        _selectedSellIndex = -1;
                    }
                    else
                    {
                        _buyCategoryCollapsed[cat] = !(_buyCategoryCollapsed.TryGetValue(cat, out bool cv) && cv);
                        _selectedBuyIndex = -1;
                    }
                    // Don't auto-select item 0 after toggling a category — leave selection empty
                    PopulateCurrentList(autoSelect: false);
                });
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            return header;
        }

        private static readonly Color RaritySubHeaderBg = new Color(0.25f, 0.22f, 0.15f, 0.7f);

        private GameObject CreateRaritySubHeader(string category, string rarity, bool collapsed, bool isSell, int visualIndex, float spacing)
        {
            if (_recipeElementPrefab == null || _listRoot == null) return null;

            var header = Instantiate(_recipeElementPrefab, _listRoot);
            header.SetActive(true);
            header.name = (isSell ? "SellRar_" : "BuyRar_") + category + "_" + (rarity == "" ? "Base" : rarity);

            var hRT = header.transform as RectTransform;
            StripLayoutComponents(header);
            hRT.anchorMin = new Vector2(0f, 1f);
            hRT.anchorMax = new Vector2(1f, 1f);
            hRT.pivot = new Vector2(0.5f, 1f);
            hRT.anchoredPosition = new Vector2(0f, visualIndex * -spacing);
            hRT.sizeDelta = new Vector2(0f, spacing - 2f);

            var bgImg = header.GetComponent<Image>();
            if (bgImg != null) bgImg.color = RaritySubHeaderBg;
            AddHoverEffect(header, RaritySubHeaderBg);

            var iconTr = header.transform.Find("icon");
            if (iconTr != null) iconTr.gameObject.SetActive(false);

            var nameTr = header.transform.Find("name");
            if (nameTr != null)
            {
                var nameRT = nameTr as RectTransform;
                if (nameRT != null)
                {
                    nameRT.anchorMin = Vector2.zero;
                    nameRT.anchorMax = Vector2.one;
                    nameRT.offsetMin = new Vector2(20f, 0f);
                    nameRT.offsetMax = new Vector2(-10f, 0f);
                }
                var nameTxt = nameTr.GetComponent<TMP_Text>();
                if (nameTxt != null)
                {
                    string displayName = RarityDisplayNames.TryGetValue(rarity, out string dn) ? dn : rarity;
                    string arrow = collapsed ? ">" : "v";
                    if (string.IsNullOrEmpty(rarity))
                    {
                        nameTxt.text = $"{arrow}  {displayName}";
                        nameTxt.color = new Color(0.85f, 0.8f, 0.65f, 1f);
                    }
                    else
                    {
                        string hexColor = GetRarityHexColor(rarity);
                        nameTxt.text = $"{arrow}  <color={hexColor}>{displayName}</color>";
                        nameTxt.richText = true;
                        nameTxt.color = Color.white;
                    }
                    nameTxt.fontStyle = FontStyles.Bold;
                    nameTxt.fontSize = 16f;
                    nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
                }
            }

            var durTr = header.transform.Find("Durability");
            if (durTr != null) durTr.gameObject.SetActive(false);
            var qualTr = header.transform.Find("QualityLevel");
            if (qualTr != null) qualTr.gameObject.SetActive(false);
            var selTr = header.transform.Find("selected");
            if (selTr != null) selTr.gameObject.SetActive(false);

            var btn = header.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                string collapseKey = category + "|" + rarity;
                btn.onClick.AddListener(() =>
                {
                    if (isSell)
                    {
                        _sellRarityCollapsed[collapseKey] = !(_sellRarityCollapsed.TryGetValue(collapseKey, out bool cv) && cv);
                        _selectedSellIndex = -1;
                    }
                    else
                    {
                        _buyRarityCollapsed[collapseKey] = !(_buyRarityCollapsed.TryGetValue(collapseKey, out bool cv) && cv);
                        _selectedBuyIndex = -1;
                    }
                    PopulateCurrentList(autoSelect: false);
                });
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            return header;
        }

        // ══════════════════════════════════════════
        //  SEARCH
        // ══════════════════════════════════════════

        private void OnSearchChanged(string value)
        {
            _searchFilter = value ?? "";
            if (_activeTab == 0) _selectedBuyIndex = -1;
            else _selectedSellIndex = -1;
            PopulateCurrentList();
        }

        // ══════════════════════════════════════════
        //  SELECTION & DESCRIPTION
        // ══════════════════════════════════════════

        private void SelectBuyItem(int index)
        {
            var entries = GetFilteredBuyEntries();
            if (entries.Count == 0) { _selectedBuyIndex = -1; RefreshDescription(); return; }
            _selectedBuyIndex = Mathf.Clamp(index, 0, entries.Count - 1);

            // Update highlights — match by dataIndex, not visual position
            foreach (var (elem, elemIdx) in _listElements)
            {
                var sel = elem?.transform.Find("selected");
                if (sel != null) sel.gameObject.SetActive(elemIdx == _selectedBuyIndex);
            }

            RefreshDescription();
            UpdatePreviewEquipment(entries[_selectedBuyIndex]);
        }

        private void SelectSellItem(int index)
        {
            var entries = GetFilteredSellEntries();
            if (entries.Count == 0) { _selectedSellIndex = -1; RefreshDescription(); return; }
            _selectedSellIndex = Mathf.Clamp(index, 0, entries.Count - 1);

            // Update highlights — match by dataIndex, not visual position
            foreach (var (elem, elemIdx) in _listElements)
            {
                var sel = elem?.transform.Find("selected");
                if (sel != null) sel.gameObject.SetActive(elemIdx == _selectedSellIndex);
            }

            RefreshDescription();
        }

        private void RefreshDescription()
        {
            if (_activeTab == 0)
            {
                var entries = GetFilteredBuyEntries();
                if (_selectedBuyIndex >= 0 && _selectedBuyIndex < entries.Count)
                {
                    var e = entries[_selectedBuyIndex];
                    if (_itemNameText != null)
                    {
                        string nameText = e.Name;
                        if (!string.IsNullOrEmpty(e.Rarity))
                            nameText += $" <size=14><color={GetRarityHexColor(e.Rarity)}>({e.Rarity})</color></size>";
                        _itemNameText.text = nameText;
                        _itemNameText.richText = true;
                    }
                    if (_itemDescText != null)
                    {
                        string tooltipText = e.Description;
                        var prefab = ObjectDB.instance?.GetItemPrefab(e.PrefabName);
                        var drop = prefab?.GetComponent<ItemDrop>();
                        if (drop?.m_itemData != null)
                        {
                            // For rarity items, create a temp enchanted item so Epic Loot's
                            // tooltip patch can show the magic effects and modified stats.
                            if (!string.IsNullOrEmpty(e.Rarity) && EpicLootIntegration.IsAvailable)
                            {
                                string cacheKey = e.PrefabName + "|" + e.Rarity;
                                if (!_rarityTooltipCache.TryGetValue(cacheKey, out string cached))
                                {
                                    cached = GenerateRarityTooltip(prefab, e.Rarity, e.Stack);
                                    if (cached != null)
                                        _rarityTooltipCache[cacheKey] = cached;
                                }
                                tooltipText = cached ?? Localize(ItemDrop.ItemData.GetTooltip(drop.m_itemData, 1, false, Game.m_worldLevel, e.Stack));
                            }
                            else
                            {
                                string raw = ItemDrop.ItemData.GetTooltip(drop.m_itemData, 1, false, Game.m_worldLevel, e.Stack);
                                tooltipText = Localize(raw);
                            }
                        }
                        _itemDescText.text = tooltipText;
                        _itemDescText.ForceMeshUpdate();
                        LayoutRebuilder.ForceRebuildLayoutImmediate(_itemDescText.rectTransform);
                    }
                    _descScrollResetFrames = 3;

                    if (_actionButton != null)
                    {
                        int coins = GetBankBalance();
                        _actionButton.interactable = coins >= e.Price;
                        if (_actionButtonLabel != null)
                            _actionButtonLabel.text = coins >= e.Price ? $"Buy ({e.Price})" : $"Need {e.Price} coins";
                    }
                }
                else
                {
                    ClearDescription();
                }
            }
            else
            {
                var entries = GetFilteredSellEntries();
                if (_selectedSellIndex >= 0 && _selectedSellIndex < entries.Count)
                {
                    var e = entries[_selectedSellIndex];
                    if (_itemNameText != null)
                    {
                        string nameText = e.Name;
                        if (!string.IsNullOrEmpty(e.Rarity))
                            nameText += $" <size=14><color={GetRarityHexColor(e.Rarity)}>({e.Rarity})</color></size>";
                        _itemNameText.text = nameText;
                        _itemNameText.richText = true;
                    }
                    if (_itemDescText != null)
                    {
                        string tooltipText = Localize(e.Item?.m_shared?.m_description ?? "");
                        if (e.Item != null)
                        {
                            string raw = ItemDrop.ItemData.GetTooltip(e.Item, e.Item.m_quality, false, Game.m_worldLevel);
                            tooltipText = Localize(raw);
                        }
                        _itemDescText.text = tooltipText + $"\n\n<color=#AAAAAA>In inventory: {e.Item?.m_stack ?? 0}</color>";
                        _itemDescText.ForceMeshUpdate();
                        LayoutRebuilder.ForceRebuildLayoutImmediate(_itemDescText.rectTransform);
                    }
                    _descScrollResetFrames = 3;

                    if (_actionButton != null)
                    {
                        _actionButton.interactable = true;
                        if (_actionButtonLabel != null)
                            _actionButtonLabel.text = $"Sell ({e.Price})";
                    }
                }
                else
                {
                    ClearDescription();
                }
            }
        }

        private void ClearDescription()
        {
            if (_itemNameText != null) _itemNameText.text = "";
            if (_itemDescText != null) _itemDescText.text = "";
            if (_actionButton != null)
            {
                _actionButton.interactable = false;
                if (_actionButtonLabel != null) _actionButtonLabel.text = "Select an item";
            }
        }

        // ══════════════════════════════════════════
        //  TRANSACTIONS
        // ══════════════════════════════════════════

        private void OnActionButtonClicked()
        {
            if (_activeTab == 0) ExecuteBuy();
            else ExecuteSell();
        }

        private void ExecuteBuy()
        {
            var entries = GetFilteredBuyEntries();
            if (_selectedBuyIndex < 0 || _selectedBuyIndex >= entries.Count) return;
            var entry = entries[_selectedBuyIndex];

            var player = Player.m_localPlayer;
            if (player == null) return;
            var inv = ((Humanoid)player).GetInventory();
            if (inv == null || _currentStoreGui == null) return;

            if (_bankBalance < entry.Price)
            {
                ((Character)player).Message(MessageHud.MessageType.Center, "Not enough coins in bank!");
                return;
            }

            _bankBalance -= entry.Price;
            SaveBankBalance();

            bool hasRarity = !string.IsNullOrEmpty(entry.Rarity);
            ItemDrop.ItemData added = null;

            if (hasRarity)
            {
                // For rarity items, manually create the item so we can inject magic data
                // BEFORE it enters the inventory (Epic Loot caches ItemInfo on AddItem).
                added = CreateMagicItem(inv, entry.PrefabName, entry.Stack, entry.Rarity);
            }
            else
            {
                added = inv.AddItem(entry.PrefabName, entry.Stack, 1, 0, 0L, "");
            }

            if (added == null)
            {
                _bankBalance += entry.Price;
                SaveBankBalance();
                ((Character)player).Message(MessageHud.MessageType.Center, "Inventory full!");
                return;
            }

            if (_currentStoreGui.m_sellEffects != null)
                _currentStoreGui.m_sellEffects.Create(_currentStoreGui.transform.position, Quaternion.identity);

            string rarityTag = !string.IsNullOrEmpty(entry.Rarity) ? $" ({entry.Rarity})" : "";
            string msg = entry.Stack > 1 ? $"Bought {entry.Stack} {entry.Name}{rarityTag}" : $"Bought {entry.Name}{rarityTag}";
            ((Character)player).Message(MessageHud.MessageType.TopLeft, $"{msg} for {entry.Price} coins", 0, entry.Icon);

            BuildSellEntries();
            UpdateCoinDisplay();
            RefreshDescription();
        }

        /// <summary>
        /// Creates an item, applies Epic Loot magic data to its m_customData, then adds
        /// it to the inventory. Magic data is written BEFORE AddItem so Epic Loot's
        /// ItemInfo cache picks it up immediately.
        /// </summary>
        private static ItemDrop.ItemData CreateMagicItem(Inventory inv, string prefabName, int stack, string rarity)
        {
            GameObject prefab = ObjectDB.instance?.GetItemPrefab(prefabName);
            if (prefab == null) return null;

            ItemDrop.ItemData result = null;
            int remaining = stack;

            while (remaining > 0)
            {
                ZNetView.m_forceDisableInit = true;
                GameObject tempObj = Instantiate(prefab);
                ZNetView.m_forceDisableInit = false;

                var drop = tempObj.GetComponent<ItemDrop>();
                if (drop == null)
                {
                    Destroy(tempObj);
                    return null;
                }

                int amount = Mathf.Min(remaining, drop.m_itemData.m_shared.m_maxStackSize);
                remaining -= amount;
                drop.m_itemData.m_stack = amount;
                drop.SetQuality(1);
                drop.m_itemData.m_variant = 0;
                drop.m_itemData.m_durability = drop.m_itemData.GetMaxDurability();
                drop.m_itemData.m_crafterID = 0L;
                drop.m_itemData.m_crafterName = "";
                drop.m_itemData.m_worldLevel = (byte)Game.m_worldLevel;
                drop.m_itemData.m_pickedUp = false;

                // Inject magic data BEFORE adding to inventory so Epic Loot sees it immediately
                EpicLootIntegration.ApplyRarity(drop.m_itemData, rarity);

                if (!inv.AddItem(drop.m_itemData))
                {
                    Destroy(tempObj);
                    return null;
                }

                result = drop.m_itemData;
                Destroy(tempObj);
            }

            return result;
        }

        /// <summary>
        /// Creates a temporary enchanted item, applies Epic Loot magic data,
        /// then calls GetTooltip so Epic Loot's patches generate the enchanted tooltip.
        /// </summary>
        private static string GenerateRarityTooltip(GameObject prefab, string rarity, int stack)
        {
            try
            {
                ZNetView.m_forceDisableInit = true;
                GameObject tempObj = Instantiate(prefab);
                ZNetView.m_forceDisableInit = false;

                var drop = tempObj.GetComponent<ItemDrop>();
                if (drop?.m_itemData == null)
                {
                    Destroy(tempObj);
                    return null;
                }

                drop.m_itemData.m_stack = stack;
                drop.m_itemData.m_quality = 1;
                drop.m_itemData.m_durability = drop.m_itemData.GetMaxDurability();

                EpicLootIntegration.ApplyRarity(drop.m_itemData, rarity);

                // Pass crafting: false so Epic Loot's tooltip patch processes magic effects
                string raw = ItemDrop.ItemData.GetTooltip(drop.m_itemData, 1, false, Game.m_worldLevel, stack);
                string result = Localize(raw);

                Destroy(tempObj);
                return result;
            }
            catch (System.Exception ex)
            {
                TraderOverhaulPlugin.Log.LogWarning($"[TraderUI] GenerateRarityTooltip failed: {ex.Message}");
                return null;
            }
        }

        private void ExecuteSell()
        {
            var entries = GetFilteredSellEntries();
            if (_selectedSellIndex < 0 || _selectedSellIndex >= entries.Count) return;
            var entry = entries[_selectedSellIndex];

            var player = Player.m_localPlayer;
            if (player == null) return;
            var inv = ((Humanoid)player).GetInventory();
            if (inv == null || _currentStoreGui == null) return;

            // entry.Item could be stale if the inventory changed between selection and click
            if (entry.Item == null || entry.Item.m_stack < entry.ConfigStack)
            {
                BuildSellEntries();
                PopulateCurrentList();
                return;
            }

            int prevIndex = _selectedSellIndex;

            // Deposit sale price directly into bank (always succeeds), then remove item
            _bankBalance += entry.Price;
            SaveBankBalance();

            if (entry.Item.m_stack <= entry.ConfigStack)
                inv.RemoveItem(entry.Item);
            else
                entry.Item.m_stack -= entry.ConfigStack;

            if (_currentStoreGui.m_sellEffects != null)
                _currentStoreGui.m_sellEffects.Create(_currentStoreGui.transform.position, Quaternion.identity);

            string qty = entry.ConfigStack > 1 ? $"{entry.ConfigStack} {entry.Name}" : entry.Name;
            ((Character)player).Message(MessageHud.MessageType.TopLeft, $"Sold {qty} for {entry.Price} coins", 0, entry.Icon);

            BuildSellEntries();
            PopulateCurrentList();

            var newEntries = GetFilteredSellEntries();
            if (newEntries.Count > 0)
                SelectSellItem(Mathf.Min(prevIndex, newEntries.Count - 1));
            else
            {
                _selectedSellIndex = -1;
                RefreshDescription();
            }

            UpdateCoinDisplay();
        }

        private void UpdateCoinDisplay()
        {
            if (_coinDisplayText == null) return;
            int coins = GetBankBalance();
            if (coins == _lastCoinDisplayCount) return;
            _lastCoinDisplayCount = coins;
            _coinDisplayText.text = $"Bank: {coins:N0}";
        }

        // ══════════════════════════════════════════
        //  PLAYER PREVIEW
        // ══════════════════════════════════════════

        private void SetupPlayerPreview()
        {
            ClearPlayerPreview();
            var player = Player.m_localPlayer;
            if (player == null) return;

            var prefab = ZNetScene.instance?.GetPrefab("Player");
            if (prefab == null) return;

            ZNetView.m_forceDisableInit = true;
            try { _playerClone = Instantiate(prefab, PlayerSpawnPos, Quaternion.identity); }
            finally { ZNetView.m_forceDisableInit = false; }

            var rb = _playerClone.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            foreach (var mb in _playerClone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is VisEquipment) continue;
                mb.enabled = false;
            }

            foreach (var anim in _playerClone.GetComponentsInChildren<Animator>())
                anim.updateMode = AnimatorUpdateMode.Normal;

            var clonePlayer = _playerClone.GetComponent<Player>();
            if (clonePlayer != null)
            {
                clonePlayer.enabled = true;
                var tempProfile = new PlayerProfile("_preview_tmp", FileHelpers.FileSource.Local);
                tempProfile.SavePlayerData(player);
                tempProfile.LoadPlayerData(clonePlayer);
                clonePlayer.enabled = false;

                var visEquip = _playerClone.GetComponent<VisEquipment>();
                if (visEquip != null)
                    AccessTools.Method(typeof(VisEquipment), "UpdateVisuals")?.Invoke(visEquip, null);
            }

            _playerClone.transform.rotation = Quaternion.identity;
            int charLayer = LayerMask.NameToLayer("character");
            if (charLayer < 0) charLayer = 9;
            foreach (var t in _playerClone.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = charLayer;

            // Save the player's current equipment as the baseline for preview
            var baseVisEquip = _playerClone.GetComponent<VisEquipment>();
            if (baseVisEquip != null)
            {
                var slotNames = new[] { "m_rightItem", "m_leftItem", "m_chestItem",
                    "m_legItem", "m_helmetItem", "m_shoulderItem",
                    "m_utilityItem", "m_leftBackItem", "m_rightBackItem" };
                _savedEquipSlots = new Dictionary<string, string>();
                foreach (var s in slotNames)
                {
                    var val = AccessTools.Field(typeof(VisEquipment), s)?.GetValue(baseVisEquip) as string;
                    _savedEquipSlots[s] = val ?? "";
                }
            }

            SetupLightRig(ref _playerLightRig, PlayerSpawnPos);

            Vector3 center = PlayerSpawnPos + Vector3.up * 0.9f;
            _playerCamGO.transform.position = center + new Vector3(0f, 0.3f, 5.0f);
            _playerCamGO.transform.LookAt(center);
        }

        private void ClearPlayerPreview()
        {
            if (_playerLightRig != null) { Destroy(_playerLightRig); _playerLightRig = null; }
            if (_playerClone != null) { Destroy(_playerClone); _playerClone = null; }
        }

        private void UpdatePreviewEquipment(BuyEntry entry)
        {
            if (_playerClone == null || entry == null || _savedEquipSlots == null) return;
            var visEquip = _playerClone.GetComponent<VisEquipment>();
            if (visEquip == null) return;

            // Start from the player's actual equipped gear
            var slots = new Dictionary<string, string>(_savedEquipSlots);

            // Only override the specific slot matching the highlighted item
            var prefab = ObjectDB.instance?.GetItemPrefab(entry.PrefabName);
            if (prefab != null)
            {
                var drop = prefab.GetComponent<ItemDrop>();
                if (drop?.m_itemData?.m_shared != null)
                {
                    string slot = GetVisEquipSlot(drop.m_itemData.m_shared.m_itemType);
                    if (slot != null) slots[slot] = entry.PrefabName;
                }
            }

            foreach (var kv in slots)
                AccessTools.Field(typeof(VisEquipment), kv.Key)?.SetValue(visEquip, kv.Value);

            // Don't reset all hashes — let UpdateVisuals detect only the changed slot naturally,
            // preventing the full teardown/rebuild flicker that ResetVisEquipHashes caused.
            AccessTools.Method(typeof(VisEquipment), "UpdateVisuals")?.Invoke(visEquip, null);

            // Unity's Animation step (which binds SkinnedMeshRenderers to the skeleton) normally
            // runs between Update and LateUpdate. If UpdateVisuals just instantiated a new mesh,
            // the Animator hasn't processed it yet when cam.Render() fires in LateUpdate, causing
            // a one-frame rest-pose flash. Force an evaluation now so the new mesh is fully bound
            // before we render.
            var animator = _playerClone.GetComponentInChildren<Animator>(true);
            if (animator != null) animator.Update(0f);

            int charLayer = LayerMask.NameToLayer("character");
            if (charLayer < 0) charLayer = 9;
            foreach (var t in _playerClone.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = charLayer;
        }

        private static string GetVisEquipSlot(ItemDrop.ItemData.ItemType type)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.Tool:
                    return "m_rightItem";
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Torch:
                    return "m_leftItem";
                case ItemDrop.ItemData.ItemType.Bow:
                    return "m_leftBackItem";
                case ItemDrop.ItemData.ItemType.Chest:
                    return "m_chestItem";
                case ItemDrop.ItemData.ItemType.Legs:
                    return "m_legItem";
                case ItemDrop.ItemData.ItemType.Helmet:
                    return "m_helmetItem";
                case ItemDrop.ItemData.ItemType.Shoulder:
                    return "m_shoulderItem";
                case ItemDrop.ItemData.ItemType.Utility:
                    return "m_utilityItem";
                default:
                    return null;
            }
        }

        private void UpdatePlayerPreviewRotation()
        {
            // Begin drag when mouse button pressed over the player preview image
            if (_playerPreviewImg != null && _playerPreviewImg.gameObject.activeInHierarchy
                && Input.GetMouseButtonDown(0))
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        _playerPreviewImg.rectTransform, Input.mousePosition, null))
                {
                    _isDraggingPlayerPreview = true;
                    _lastMouseX = Input.mousePosition.x;
                }
            }

            if (_isDraggingPlayerPreview)
            {
                if (Input.GetMouseButton(0))
                {
                    float delta = Input.mousePosition.x - _lastMouseX;
                    _previewRotation = (_previewRotation + delta * MouseDragSensitivity) % 360f;
                    _lastMouseX = Input.mousePosition.x;
                }
                else
                {
                    _isDraggingPlayerPreview = false;
                }
            }
            else
            {
                // Auto-rotate when not dragging
                _previewRotation = (_previewRotation + AutoRotateSpeed * Time.deltaTime) % 360f;
            }
        }

        private void UpdatePlayerCamera()
        {
            if (_playerCamGO == null) return;
            Vector3 center = PlayerSpawnPos + Vector3.up * 0.9f;
            float rad = _previewRotation * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(rad), 0.3f, Mathf.Cos(rad)) * 5.0f;
            _playerCamGO.transform.position = center + offset;
            _playerCamGO.transform.LookAt(center);
        }

        // ══════════════════════════════════════════
        //  TRADER PREVIEW
        // ══════════════════════════════════════════        
        private void SetupTraderPreview()
        {
            ClearTraderPreview();
            if (_currentTrader == null) return;

            TraderPreviewProfile preview = TraderIdentity.GetPreviewProfile(_currentTraderKind);
            Vector3 spawnPos = TraderSpawnPos + preview.SpawnOffset;
            Vector3 center = TraderSpawnPos + preview.CenterOffset;
            if (_haldorCam != null) _haldorCam.fieldOfView = preview.CameraFov;

            ZNetView.m_forceDisableInit = true;
            try { _haldorClone = Instantiate(_currentTrader.gameObject, spawnPos, Quaternion.identity); }
            finally { ZNetView.m_forceDisableInit = false; }

            var rb = _haldorClone.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            foreach (var mb in _haldorClone.GetComponentsInChildren<MonoBehaviour>(true))
                mb.enabled = false;

            var traderAnim = _haldorClone.GetComponentInChildren<Animator>();
            if (traderAnim != null)
            {
                SetAnimatorBoolIfPresent(traderAnim, "Stand", true);
                SetAnimatorBoolIfPresent(traderAnim, "Sit", false);
                SetAnimatorBoolIfPresent(traderAnim, "Sitting", false);
                traderAnim.Update(preview.AnimatorUpdateDelta);
            }

            _haldorClone.transform.rotation = preview.Rotation;

            int charLayer = LayerMask.NameToLayer("character");
            if (charLayer < 0) charLayer = 9;
            foreach (var t in _haldorClone.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = charLayer;

            SetupLightRig(ref _haldorLightRig, spawnPos);
            _haldorCamGO.transform.position = center + preview.CameraOffset;
            _haldorCamGO.transform.LookAt(center);
        }

        private static void SetAnimatorBoolIfPresent(Animator animator, string name, bool value)
        {
            if (animator == null || string.IsNullOrEmpty(name)) return;
            foreach (var param in animator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Bool &&
                    string.Equals(param.name, name, StringComparison.Ordinal))
                {
                    animator.SetBool(name, value);
                    return;
                }
            }
        }

        private void ClearTraderPreview()
        {
            if (_haldorLightRig != null) { Destroy(_haldorLightRig); _haldorLightRig = null; }
            if (_haldorClone != null) { Destroy(_haldorClone); _haldorClone = null; }
        }


        // ══════════════════════════════════════════
        //  SHARED PREVIEW HELPERS
        // ══════════════════════════════════════════

        private void SetupLightRig(ref GameObject rig, Vector3 pos)
        {
            if (rig != null) Destroy(rig);
            rig = new GameObject("PreviewLightRig");
            DontDestroyOnLoad(rig);
            rig.transform.position = pos;

            int charLayer = LayerMask.NameToLayer("character");
            if (charLayer < 0) charLayer = 9;
            int lightMask = (1 << charLayer);
            int charNet = LayerMask.NameToLayer("character_net");
            if (charNet >= 0) lightMask |= (1 << charNet);

            CreateLight(rig.transform, "Key", new Vector3(1.5f, 2.5f, 3.5f), 2.0f, new Color(1f, 0.92f, 0.82f), 15f, lightMask);
            CreateLight(rig.transform, "Fill", new Vector3(-2.5f, 1.5f, 3f), 1.2f, new Color(0.9f, 0.92f, 1f), 15f, lightMask);
            CreateLight(rig.transform, "Rim", new Vector3(0f, 3f, -2.5f), 1.2f, new Color(0.95f, 0.88f, 0.78f), 15f, lightMask);
            CreateLight(rig.transform, "Bottom", new Vector3(0f, -0.5f, 3f), 0.5f, new Color(0.85f, 0.82f, 0.78f), 10f, lightMask);
        }

        private static void CreateLight(Transform parent, string name, Vector3 localPos, float intensity, Color color, float range, int mask)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            light.cullingMask = mask;
            light.shadows = LightShadows.None;
        }

        private void EnablePreviewCameras()
        {
            // Cameras are rendered manually via cam.Render() in LateUpdate — keep disabled
            if (_playerCam != null) _playerCam.enabled = false;
            if (_haldorCam != null) _haldorCam.enabled = false;
        }

        private void DisablePreviewCameras()
        {
            if (_playerCam != null) _playerCam.enabled = false;
            if (_haldorCam != null) _haldorCam.enabled = false;
        }

        private void SaveAmbient()
        {
            _savedAmbientColor = RenderSettings.ambientLight;
            _savedAmbientIntensity = RenderSettings.ambientIntensity;
            _savedAmbientMode = RenderSettings.ambientMode;
        }

        private void SetPreviewAmbient()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.4f, 0.35f);
            RenderSettings.ambientIntensity = 1.2f;
        }

        private void RestoreAmbient()
        {
            RenderSettings.ambientMode = _savedAmbientMode;
            RenderSettings.ambientLight = _savedAmbientColor;
            RenderSettings.ambientIntensity = _savedAmbientIntensity;
        }

        // ══════════════════════════════════════════
        //  UTILITY HELPERS
        // ══════════════════════════════════════════

        private List<BuyEntry> GetFilteredBuyEntries()
        {
            bool hasCat    = !string.IsNullOrEmpty(_activeCategoryFilter);
            bool hasSearch = !string.IsNullOrEmpty(_searchFilter);
            if (!hasCat && !hasSearch) return _allBuyEntries;
            var result = _allBuyEntries.AsEnumerable();
            if (hasCat)    result = result.Where(e => e.Category == _activeCategoryFilter);
            if (hasSearch) result = result.Where(e =>
                e.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (!string.IsNullOrEmpty(e.Rarity) && e.Rarity.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0));
            return result.ToList();
        }

        private List<SellEntry> GetFilteredSellEntries()
        {
            bool hasCat    = !string.IsNullOrEmpty(_activeCategoryFilter);
            bool hasSearch = !string.IsNullOrEmpty(_searchFilter);
            if (!hasCat && !hasSearch) return _allSellEntries;
            var result = _allSellEntries.AsEnumerable();
            if (hasCat)    result = result.Where(e => e.Category == _activeCategoryFilter);
            if (hasSearch) result = result.Where(e =>
                e.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (!string.IsNullOrEmpty(e.Rarity) && e.Rarity.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0));
            return result.ToList();
        }

        private int GetVisibleBuyCount()
        {
            // Count visible items (excluding collapsed categories)
            var entries = GetFilteredBuyEntries();
            if (string.IsNullOrEmpty(_searchFilter))
            {
                int count = 0;
                var grouped = new Dictionary<string, int>();
                foreach (var e in entries)
                {
                    if (!grouped.ContainsKey(e.Category)) grouped[e.Category] = 0;
                    grouped[e.Category]++;
                }
                foreach (var kv in grouped)
                {
                    bool collapsed = _buyCategoryCollapsed.TryGetValue(kv.Key, out bool c) && c;
                    if (!collapsed) count += kv.Value;
                }
                return count;
            }
            return entries.Count;
        }

        private int GetVisibleSellCount()
        {
            var entries = GetFilteredSellEntries();
            if (string.IsNullOrEmpty(_searchFilter))
            {
                int count = 0;
                var grouped = new Dictionary<string, int>();
                foreach (var e in entries)
                {
                    if (!grouped.ContainsKey(e.Category)) grouped[e.Category] = 0;
                    grouped[e.Category]++;
                }
                foreach (var kv in grouped)
                {
                    bool collapsed = _sellCategoryCollapsed.TryGetValue(kv.Key, out bool c) && c;
                    if (!collapsed) count += kv.Value;
                }
                return count;
            }
            return entries.Count;
        }

        private void EnsureItemVisible(int dataIndex)
        {
            if (_listScrollRect == null || _listElements.Count == 0) return;
            var match = _listElements.Find(e => e.dataIndex == dataIndex);
            var itemRT = match.go?.transform as RectTransform;
            if (itemRT == null || _listScrollRect.content == null) return;

            float contentH = _listScrollRect.content.rect.height;
            float viewportH = _listScrollRect.viewport != null ? _listScrollRect.viewport.rect.height : 300f;
            if (contentH <= viewportH) return;

            float itemY = Mathf.Abs(itemRT.anchoredPosition.y);
            float scrollable = contentH - viewportH;
            float norm = 1f - Mathf.Clamp01(itemY / scrollable);
            _listScrollRect.verticalNormalizedPosition = norm;
        }

        private static string GetItemCategory(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null) return "Misc";
            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Attach_Atgeir:
                    return "Weapons";
                case ItemDrop.ItemData.ItemType.Shield:
                    return "Shields";
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Hands:
                    return "Armor";
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                    return "Ammo";
                case ItemDrop.ItemData.ItemType.Consumable:
                    return "Consumables";
                case ItemDrop.ItemData.ItemType.Material:
                    return "Materials";
                case ItemDrop.ItemData.ItemType.Trophy:
                    return "Trophies";
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Utility:
                    return "Utility";
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.Customization:
                case ItemDrop.ItemData.ItemType.Fish:
                case ItemDrop.ItemData.ItemType.Trinket:
                default:
                    return "Misc";
            }
        }

        // Epic Loot m_customData key and rarity enum names (no hard dependency)
        private const string EpicLootDataKey = "randyknapp.mods.epicloot#EpicLoot.MagicItemComponent,EpicLoot";
        private static readonly string[] EpicLootRarityNames = { "Magic", "Rare", "Epic", "Legendary", "Mythic" };

        private static string GetRarityHexColor(string rarity)
        {
            switch (rarity)
            {
                case "Magic": return "#00abff";
                case "Rare": return "#ffff75";
                case "Epic": return "#d078ff";
                case "Legendary": return "#18e7a9";
                case "Mythic": return "#ffac59";
                default: return "#FFFFFF";
            }
        }

        private static float GetRaritySellMultiplier(string rarity)
        {
            switch (rarity)
            {
                case "Magic": return 2f;
                case "Rare": return 3f;
                case "Epic": return 5f;
                case "Legendary": return 10f;
                case "Mythic": return 20f;
                default: return 1f;
            }
        }

        private static string GetItemRarity(ItemDrop.ItemData item)
        {
            if (item?.m_customData == null) return "";
            if (!item.m_customData.TryGetValue(EpicLootDataKey, out string json)) return "";
            if (string.IsNullOrEmpty(json)) return "";

            // Parse "Rarity":N from the JSON without needing Epic Loot as a dependency.
            // The value is an int: Magic=0, Rare=1, Epic=2, Legendary=3, Mythic=4
            var match = System.Text.RegularExpressions.Regex.Match(json, "\"Rarity\"\\s*:\\s*(\\d+)");
            if (!match.Success) return "";
            if (!int.TryParse(match.Groups[1].Value, out int idx)) return "";
            if (idx < 0 || idx >= EpicLootRarityNames.Length) return "";
            return EpicLootRarityNames[idx];
        }

        private static string Localize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (Localization.instance != null)
            {
                string loc = Localization.instance.Localize(text);
                if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[")) return loc;
            }
            return text;
        }

        private static bool HasGlobalKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return true;
            var zs = ZoneSystem.instance;
            if (zs == null) return false;
            return zs.GetGlobalKey(key);
        }

        private int GetBankBalance() => _bankBalance;

        /// <summary>
        /// Reloads the bank balance from m_customData and refreshes any visible displays.
        /// Call this after externally modifying the bank balance (e.g. from a console command).
        /// </summary>
        public void ReloadBankBalance()
        {
            LoadBankBalance();
            if (!IsVisible) return;
            UpdateCoinDisplay();
            RefreshBankDisplay();
        }

        private static int CalculateInventoryHash(List<ItemDrop.ItemData> items)
        {
            if (items == null) return 0;
            int hash = 17;
            foreach (var item in items)
            {
                if (item == null) continue;
                hash = hash * 397 + (item.m_shared?.m_name?.GetHashCode() ?? 0);
                hash = hash * 397 + item.m_stack;
            }
            return hash;
        }

        private void RefreshSellListIfChanged()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            var inv = ((Humanoid)player).GetInventory();
            if (inv == null) return;

            int hash = CalculateInventoryHash(inv.GetAllItems());
            if (hash != _lastInventoryHash)
            {
                _lastInventoryHash = hash;
                BuildSellEntries();
                if (_activeTab == 1) PopulateCurrentList();
            }
        }

        private TMP_FontAsset FindValheimFont()
        {
            if (_valheimFont != null) return _valheimFont;
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

        // ══════════════════════════════════════════
        //  BANK TAB
        // ══════════════════════════════════════════

        private void BuildBankPanel()
        {
            // Full-size panel replacing the 3 columns when bank tab is active
            _bankContentPanel = new GameObject("BankContent", typeof(RectTransform), typeof(Image));
            _bankContentPanel.transform.SetParent(_mainPanel.transform, false);
            var rt = _bankContentPanel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(OuterPad, _bottomPad);
            rt.offsetMax = new Vector2(-OuterPad, -_colTopInset);
            ApplyPanelStyle(_bankContentPanel.GetComponent<Image>());

            var p = _bankContentPanel.transform;

            // ── Title ──
            _bankTitleText = CreateBankText(p, "BankTitle", "Trader's Bank", 28f, GoldTextColor);
            var titleRT = _bankTitleText.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0f, 1f); titleRT.anchorMax = new Vector2(1f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.sizeDelta = new Vector2(0f, 38f);
            titleRT.anchoredPosition = new Vector2(0f, -14f);

            // ── Separator 1 ──
            CreateBankSeparator(p, -58f);

            // ── Bank Balance ──
            _bankBalanceText = CreateBankText(p, "BankBalance", "Bank Balance: 0", 26f, GoldTextColor);
            var bbRT = _bankBalanceText.GetComponent<RectTransform>();
            bbRT.anchorMin = new Vector2(0f, 1f); bbRT.anchorMax = new Vector2(1f, 1f);
            bbRT.pivot = new Vector2(0.5f, 1f);
            bbRT.sizeDelta = new Vector2(0f, 36f);
            bbRT.anchoredPosition = new Vector2(0f, -74f);

            // ── Inventory Coins ──
            _bankInvCoinsText = CreateBankText(p, "InvCoins", "Inventory Coins: 0", 20f, new Color(0.85f, 0.85f, 0.85f, 1f));
            var icRT = _bankInvCoinsText.GetComponent<RectTransform>();
            icRT.anchorMin = new Vector2(0f, 1f); icRT.anchorMax = new Vector2(1f, 1f);
            icRT.pivot = new Vector2(0.5f, 1f);
            icRT.sizeDelta = new Vector2(0f, 28f);
            icRT.anchoredPosition = new Vector2(0f, -118f);

            // ── Total Wealth ──
            _bankTotalText = CreateBankText(p, "BankTotal", "Total Wealth: 0", 16f, new Color(0.6f, 0.6f, 0.6f, 1f));
            var twRT = _bankTotalText.GetComponent<RectTransform>();
            twRT.anchorMin = new Vector2(0f, 1f); twRT.anchorMax = new Vector2(1f, 1f);
            twRT.pivot = new Vector2(0.5f, 1f);
            twRT.sizeDelta = new Vector2(0f, 22f);
            twRT.anchoredPosition = new Vector2(0f, -152f);

            // ── Separator 2 ──
            CreateBankSeparator(p, -180f);

            // ── Buttons (symmetric about centre) ──
            float btnH = Mathf.Max(_craftBtnHeight, 36f);
            float btnW = 200f;
            float gap  = 24f;

            _bankDepositButton  = CreateBankButton(p, "BankDeposit",  "Deposit All",
                -(btnW + gap / 2f), 12f, btnW, OnBankDeposit,  out _bankDepositSelected);
            _bankWithdrawButton = CreateBankButton(p, "BankWithdraw", "Withdraw All",
                gap / 2f,           12f, btnW, OnBankWithdraw, out _bankWithdrawSelected);

            // ── Status text ──
            _bankStatusText = CreateBankText(p, "BankStatus", "", 16f, new Color(0.65f, 0.62f, 0.50f, 1f));
            var stRT = _bankStatusText.GetComponent<RectTransform>();
            stRT.anchorMin = new Vector2(0f, 0f); stRT.anchorMax = new Vector2(1f, 0f);
            stRT.pivot = new Vector2(0.5f, 0f);
            stRT.sizeDelta = new Vector2(0f, 24f);
            stRT.anchoredPosition = new Vector2(0f, btnH + 20f);

            UpdateBankTitle();
            _bankContentPanel.SetActive(false);
        }

        private Button CreateBankButton(Transform parent, string name, string label,
            float xOffset, float y, float width, UnityEngine.Events.UnityAction onClick, out GameObject outSelected)
        {
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
            var txt = go.GetComponentInChildren<TMP_Text>(true);
            if (txt != null) { txt.gameObject.SetActive(true); txt.text = label; }
            StripButtonHints(go, txt);

            // Dark tint overlay (same as buy/sell buttons)
            var tintGO = new GameObject("Tint", typeof(RectTransform), typeof(Image));
            tintGO.transform.SetParent(go.transform, false);
            tintGO.transform.SetAsFirstSibling();
            var tintRT = tintGO.GetComponent<RectTransform>();
            tintRT.anchorMin = Vector2.zero;
            tintRT.anchorMax = Vector2.one;
            tintRT.offsetMin = Vector2.zero;
            tintRT.offsetMax = Vector2.zero;
            var tintImg = tintGO.GetComponent<Image>();
            tintImg.color = new Color(0f, 0f, 0f, 0.75f);
            tintImg.raycastTarget = false;

            var brt = go.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0f);
            brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0f, 0f);
            brt.sizeDelta = new Vector2(width, Mathf.Max(_craftBtnHeight, 36f));
            brt.anchoredPosition = new Vector2(xOffset, y);

            // Selection highlight for controller
            var selGO = new GameObject("selected", typeof(RectTransform), typeof(Image));
            selGO.transform.SetParent(go.transform, false);
            selGO.transform.SetAsFirstSibling();
            var selRT = selGO.GetComponent<RectTransform>();
            selRT.anchorMin = Vector2.zero; selRT.anchorMax = Vector2.one;
            selRT.offsetMin = new Vector2(-4f, -4f);
            selRT.offsetMax = new Vector2(4f, 4f);
            selGO.GetComponent<Image>().color = new Color(1f, 0.82f, 0.24f, 0.25f);
            selGO.SetActive(false);
            outSelected = selGO;

            return btn;
        }

        private TMP_Text CreateBankText(Transform parent, string name, string text, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (_valheimFont != null) tmp.font = _valheimFont;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = text;
            go.SetActive(true);
            return tmp;
        }

        private void CreateBankSeparator(Transform parent, float yPos)
        {
            var sep = new GameObject("Separator", typeof(RectTransform), typeof(Image));
            sep.transform.SetParent(parent, false);
            var srt = sep.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.03f, 1f);
            srt.anchorMax = new Vector2(0.97f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(0f, 2f);
            srt.anchoredPosition = new Vector2(0f, yPos);
            sep.GetComponent<Image>().color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.40f);
        }

        private void UpdateBankTitle()
        {
            if (_bankTitleText == null) return;
            _bankTitleText.text = $"{_currentTraderName}'s Bank";
        }

        private void LoadBankBalance()
        {
            _bankBalance = BankBalanceStore.Read(Player.m_localPlayer);
        }

        private void SaveBankBalance()
        {
            BankBalanceStore.Write(Player.m_localPlayer, _bankBalance);
        }

        private int GetBankInventoryCoins()
        {
            var player = Player.m_localPlayer;
            if (player == null) return 0;
            var inv = ((Humanoid)player).GetInventory();
            var coinPrefab = ObjectDB.instance?.GetItemPrefab("Coins");
            var coinDrop = coinPrefab?.GetComponent<ItemDrop>();
            string coinName = coinDrop?.m_itemData?.m_shared?.m_name;
            if (inv == null || string.IsNullOrEmpty(coinName)) return 0;
            return inv.CountItems(coinName);
        }

        private void RefreshBankDisplay()
        {
            int invCoins = GetBankInventoryCoins();
            bool balChanged = _bankBalance != _lastBankBalanceDisplay;
            bool invChanged = invCoins != _lastBankInvCoinsDisplay;

            if (balChanged)
            {
                _lastBankBalanceDisplay = _bankBalance;
                if (_bankBalanceText != null)
                    _bankBalanceText.text = $"Bank Balance: {_bankBalance:N0}";
                if (_bankWithdrawButton != null)
                    _bankWithdrawButton.interactable = _bankBalance > 0;
            }
            if (invChanged)
            {
                _lastBankInvCoinsDisplay = invCoins;
                if (_bankInvCoinsText != null)
                    _bankInvCoinsText.text = $"Inventory Coins: {invCoins:N0}";
                if (_bankDepositButton != null)
                    _bankDepositButton.interactable = invCoins > 0;
            }
            if ((balChanged || invChanged) && _bankTotalText != null)
                _bankTotalText.text = $"Total Wealth: {(_bankBalance + invCoins):N0}";

            UpdateBankHighlight();
        }

        private void UpdateBankHighlight()
        {
            bool gp = ZInput.IsGamepadActive();
            if (_bankDepositSelected != null) _bankDepositSelected.SetActive(gp && _bankFocusedButton == 0);
            if (_bankWithdrawSelected != null) _bankWithdrawSelected.SetActive(gp && _bankFocusedButton == 1);
        }

        private void OnBankDeposit()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            var inv = ((Humanoid)player).GetInventory();
            if (inv == null) return;

            var coinPrefab = ObjectDB.instance?.GetItemPrefab("Coins");
            var coinDrop = coinPrefab?.GetComponent<ItemDrop>();
            string coinName = coinDrop?.m_itemData?.m_shared?.m_name;
            if (string.IsNullOrEmpty(coinName)) return;

            int coins = inv.CountItems(coinName);
            if (coins <= 0)
            {
                if (_bankStatusText != null) _bankStatusText.text = "No coins to deposit!";
                return;
            }

            inv.RemoveItem(coinName, coins);
            _bankBalance += coins;
            SaveBankBalance();
            RefreshBankDisplay();
            UpdateCoinDisplay();
            if (_bankStatusText != null) _bankStatusText.text = $"Deposited {coins:N0} coins";
            ((Character)player).Message(MessageHud.MessageType.Center, $"Deposited {coins:N0} coins");
        }

        private void OnBankWithdraw()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            var inv = ((Humanoid)player).GetInventory();
            if (inv == null) return;

            if (_bankBalance <= 0)
            {
                if (_bankStatusText != null) _bankStatusText.text = "Bank is empty!";
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
                if (_bankStatusText != null) _bankStatusText.text = "Inventory full!";
                return;
            }
            _bankBalance = 0;
            SaveBankBalance();
            RefreshBankDisplay();
            UpdateCoinDisplay();
            if (_bankStatusText != null) _bankStatusText.text = $"Withdrew {amount:N0} coins";
            ((Character)player).Message(MessageHud.MessageType.Center, $"Withdrew {amount:N0} coins");
        }
    }
}
