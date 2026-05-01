using System.Collections;
using System.Collections.Generic;
using System;
using PeterHan.PLib.UI;
using ScopeMod.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeMod {
    internal sealed class ScopeOverlay : KScreen {
        private const int   MAX_RESULTS      = 120;
        private const float PANEL_WIDTH      = 520f;
        private const float PANEL_HEIGHT     = 560f;
        private const float STATE_REFRESH_INTERVAL_SECONDS = 0.25f;

        private static ScopeOverlay liveInstance;

        private TMP_InputField           inputField;
        private RectTransform            sectionsContent;
        private readonly List<SectionWidget> sections = new List<SectionWidget>(32);
        private readonly List<RowWidget>     visibleRows = new List<RowWidget>(MAX_RESULTS);
        private readonly Dictionary<string, bool> expandedSections =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        private List<RankedResult> currentResults = new List<RankedResult>(MAX_RESULTS);
        private int                      highlighted;
        private List<IQuickAction>       allActions;
        private float                    nextStateRefreshAt;

        // Sort key 60 sits above EDITING_SCREEN (50) and below MODAL (100). Receives input
        // before BuildingGroupScreen's KInputTextField (sort 0) per KScreenManager's
        // reverse-stack dispatch (top-most first). May need tweaking.
        public override float GetSortKey() => 60f;

        public override bool IsModal() => false;  // explicit: do NOT pause game

        public static void Open() {
            if (liveInstance != null) return;
            var parent = GameScreenManager.Instance.GetParent(
                GameScreenManager.UIRenderTarget.ScreenSpaceOverlay);
            // GraphicRaycaster is required alongside the Canvas — without it,
            // Unity's EventSystem can't dispatch pointer events into our hierarchy
            // and clicks pass through to the world. The parent's GraphicRaycaster
            // doesn't reach us once we add our own Canvas.
            var go = new GameObject("ScopeOverlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            go.layer = parent.layer;
            var screen = go.AddComponent<ScopeOverlay>();
            liveInstance = screen;
            screen.Activate();
        }

        public override void OnPrefabInit() {
            base.OnPrefabInit();
            BuildUI();
        }

        // OnSpawn is part of Klei's prefab-spawn pipeline (KMonoBehaviour). When we
        // instantiate manually via gameObject.AddComponent<ScopeOverlay>() it never fires —
        // only Awake → OnPrefabInit and Activate() → OnActivate do. So all post-construction
        // init happens in OnActivate.

        public override void OnActivate() {
            base.OnActivate();

            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
            rt.anchoredPosition = Vector2.zero;

            inputField.text = "";
            inputField.Select();
            inputField.ActivateInputField();
            highlighted = 0;
            nextStateRefreshAt = Time.unscaledTime + STATE_REFRESH_INTERVAL_SECONDS;
            RefreshActionsAndResults(keepHighlight: false);
        }

        public override void OnDeactivate() {
            base.OnDeactivate();
            if (liveInstance == this) liveInstance = null;
        }

        // Defensive consumption at the Klei pipeline so letter keys don't fire game hotkeys
        // (e.g. 'c' → cancel-tool) while the overlay is open. PLib's PTextFieldEvents at
        // sort 99 already does this while the field is focused, but we cover the gap when
        // the field momentarily isn't.
        public override void OnKeyDown(KButtonEvent e) { e.Consumed = true; }
        public override void OnKeyUp(KButtonEvent e)   { e.Consumed = true; }

        // Navigation / dismiss runs in Update() rather than OnKey* because PTextFieldEvents
        // at sort 99 consumes all Klei key events while the field is focused — our sort-60
        // OnKeyDown/Up never fires for typed keys. Unity's Input polling sees keys regardless.
        public void Update() {
            if (Time.unscaledTime >= nextStateRefreshAt) {
                nextStateRefreshAt = Time.unscaledTime + STATE_REFRESH_INTERVAL_SECONDS;
                RefreshActionsAndResults(keepHighlight: true);
            }

            if (Input.GetKeyDown(KeyCode.Escape)) {
                Deactivate();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) {
                Submit();
                return;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow))   { Highlight(highlighted - 1); return; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { Highlight(highlighted + 1); return; }

            // Click outside the panel dismisses. The panel itself catches its own
            // clicks via Image.raycastTarget, so this only fires when the cursor is over
            // empty space / the game world.
            if (Input.GetMouseButtonDown(0)) {
                var rt = (RectTransform)transform;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        rt, Input.mousePosition, /* camera */ null, out var local)
                    && !rt.rect.Contains(local)) {
                    Deactivate();
                }
            }
        }

        private void Submit() {
            if (visibleRows.Count == 0) {
                Deactivate();
                return;
            }

            var picked = visibleRows[Mathf.Clamp(highlighted, 0, visibleRows.Count - 1)].Action;
            SubmitAction(picked);
        }

        private void SubmitAction(IQuickAction picked) {
            // Defer invocation past Input.anyKeyDown so Klei's KeyDown event
            // doesn't fall through to game hotkeys. Hosted on a persistent
            // GameObject because we're about to Destroy ourselves.
            ScopeCoroutineHost.Run(WaitThenInvoke(picked));
            Deactivate();
        }

        private static IEnumerator WaitThenInvoke(IQuickAction action) {
            while (Input.anyKeyDown) yield return null;
            try { action.Invoke(); }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }

        private void UpdateResults(string query) {
            currentResults = ScopeSearch.Rank(query, allActions, MAX_RESULTS);
            RebuildSections();
        }

        private void RefreshActionsAndResults(bool keepHighlight) {
            int previousHighlight = highlighted;

            // Re-enumerate from providers so RequirementsState tracks vanilla's live cache
            // while the overlay is open (materials/research/world-state can change mid-session).
            allActions = new List<IQuickAction>(256);
            foreach (var provider in ScopeProviders.All)
                foreach (var action in provider.Enumerate()) allActions.Add(action);

            UpdateResults(inputField != null ? inputField.text : "");
            if (keepHighlight) Highlight(previousHighlight);
        }

        private void Highlight(int idx) {
            if (visibleRows.Count == 0) {
                highlighted = 0;
                return;
            }

            highlighted = ((idx % visibleRows.Count) + visibleRows.Count) % visibleRows.Count;
            for (int i = 0; i < visibleRows.Count; i++) {
                visibleRows[i].SetHighlighted(i == highlighted);
            }
        }

        private void BuildUI() {
            // Attempt to pull live values off PlanScreen /
            // BuildingGroupScreen now — they're populated once a save
            // is loaded, and the user has to be in-game for
            // ScopeOverlay.Open() to fire.
            OniUiTokens.EnsureExtracted();

            var border = gameObject.AddComponent<Image>();
            border.sprite = PUITuning.Images.BoxBorderWhite;
            border.type = Image.Type.Sliced;
            border.color = OniUiTokens.HeaderBg;
            border.raycastTarget = true;

            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(transform, worldPositionStays: false);
            var bodyRT = (RectTransform)bodyGo.transform;
            bodyRT.anchorMin = Vector2.zero;
            bodyRT.anchorMax = Vector2.one;
            bodyRT.offsetMin = new Vector2(2f, 2f);
            bodyRT.offsetMax = new Vector2(-2f, -2f);
            var bodyBg = bodyGo.AddComponent<Image>();
            bodyBg.color = Color.white;

            var layout = bodyGo.AddComponent<VerticalLayoutGroup>();
            layout.padding                = new RectOffset(0, 0, 0, 0);
            layout.spacing                = 0f;
            layout.childAlignment         = TextAnchor.UpperLeft;
            layout.childForceExpandWidth  = true;
            layout.childForceExpandHeight = false;

            BuildHeader(bodyGo.transform);
            BuildSubheader(bodyGo.transform);
            BuildBody(bodyGo.transform);
        }

        private static void BuildHeader(Transform parent) {
            var go = new GameObject("Header", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, worldPositionStays: false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = OniUiTokens.HeaderHeight;

            var bg = go.AddComponent<Image>();
            bg.color = OniUiTokens.HeaderBg;

            var labelGo = new GameObject("Title", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, worldPositionStays: false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.font              = OniUiTokens.HeaderFont;
            label.fontSize          = OniUiTokens.HeaderFontSize;
            label.color             = OniUiTokens.HeaderText;
            label.alignment         = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode  = TextWrappingModes.NoWrap;
            label.text              = "SCOPE";
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10f, 0f);
            lrt.offsetMax = new Vector2(-10f, 0f);
        }

        private void BuildSubheader(Transform parent) {
            var subheader = new GameObject("Subheader", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            subheader.transform.SetParent(parent, worldPositionStays: false);
            var subheaderLE = subheader.GetComponent<LayoutElement>();
            subheaderLE.minHeight = subheaderLE.preferredHeight = OniUiTokens.SubheaderHeight;
            subheaderLE.flexibleHeight = 0f;
            subheader.GetComponent<Image>().color = OniUiTokens.SubheaderBg;

            var subheaderLayout = subheader.AddComponent<HorizontalLayoutGroup>();
            // Vertical padding leaves the input centred at its natural height
            // inside the (taller) subheader band.
            var subheaderVPad = Mathf.Max(2, (int)((OniUiTokens.SubheaderHeight - OniUiTokens.InputHeight) / 2f));
            subheaderLayout.padding = new RectOffset(8, 8, subheaderVPad, subheaderVPad);
            subheaderLayout.spacing = 6f;
            subheaderLayout.childForceExpandHeight = true;
            subheaderLayout.childForceExpandWidth = false;
            subheaderLayout.childControlHeight = true;
            subheaderLayout.childControlWidth = true;

            var pField = new PTextField("ScopeInput") {
                Text            = "",
                PlaceholderText = STRINGS.UI.BUILDMENU.SEARCH_TEXT_PLACEHOLDER,
                MaxLength       = 64,
                MinWidth        = (int)(PANEL_WIDTH - 96),
                FlexSize        = new Vector2(1f, 0f),
                Type            = PTextField.FieldType.Text,
                BackColor       = OniUiTokens.InputBg,
                TextAlignment   = TextAlignmentOptions.Left,
                TextStyle       = PUITuning.Fonts.TextDarkStyle,
            };
            var fieldGo = pField.Build();
            fieldGo.transform.SetParent(subheader.transform, worldPositionStays: false);

            var fieldLE = fieldGo.GetComponent<LayoutElement>();
            if (fieldLE == null) fieldLE = fieldGo.AddComponent<LayoutElement>();
            fieldLE.flexibleWidth = 1f;
            fieldLE.minHeight = fieldLE.preferredHeight = OniUiTokens.InputHeight;

            var border = fieldGo.GetComponent<Image>();
            if (border != null) border.color = Color.white;

            inputField = fieldGo.GetComponent<TMP_InputField>();

            // Live filtering: PTextField's OnTextChanged delegate only fires on EndEdit
            // (Enter / blur). Wire onValueChanged directly for keystroke-by-keystroke
            // updates.
            inputField.onValueChanged.AddListener(UpdateResults);

            if (inputField.placeholder is TextMeshProUGUI placeholder) {
                placeholder.fontStyle = FontStyles.Italic;
                placeholder.color     = OniUiTokens.InputPlaceholder;
                placeholder.font      = OniUiTokens.InputFont;
                placeholder.fontSize  = OniUiTokens.InputFontSize;
            }

            if (inputField.textComponent is TextMeshProUGUI text) {
                text.fontStyle = FontStyles.Normal;
                text.color     = OniUiTokens.InputText;
                text.font      = OniUiTokens.InputFont;
                text.fontSize  = OniUiTokens.InputFontSize;
            }

            inputField.restoreOriginalTextOnEscape = false;

            BuildClearButton(subheader.transform);
        }

        private void BuildClearButton(Transform parent) {
            var clearGo = new GameObject("ClearButton", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
            clearGo.transform.SetParent(parent, worldPositionStays: false);

            var clearLE = clearGo.GetComponent<LayoutElement>();
            clearLE.minWidth = clearLE.preferredWidth = OniUiTokens.InputHeight;
            clearLE.minHeight = clearLE.preferredHeight = OniUiTokens.InputHeight;

            var clearBg = clearGo.GetComponent<Image>();
            clearBg.color = Color.white;

            var clearButton = clearGo.GetComponent<Button>();
            clearButton.onClick.AddListener(() => {
                inputField.text = "";
                inputField.Select();
                inputField.ActivateInputField();
            });

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(clearGo.transform, worldPositionStays: false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.font = PUITuning.Fonts.TextDarkStyle.sdfFont;
            label.fontSize = 16f;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "X";

            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
        }

        private void BuildBody(Transform parent) {
            var body = new GameObject("ResultsBody", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(ScrollRect));
            body.transform.SetParent(parent, worldPositionStays: false);

            var bodyLE = body.GetComponent<LayoutElement>();
            bodyLE.flexibleHeight = 1f;
            bodyLE.minHeight = 120f;

            var bodyImage = body.GetComponent<Image>();
            bodyImage.color = OniUiTokens.BodyBg;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(body.transform, worldPositionStays: false);
            var vrt = (RectTransform)viewport.transform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(0f, 0f);
            vrt.offsetMax = new Vector2(-14f, 0f);
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, worldPositionStays: false);
            sectionsContent = (RectTransform)content.transform;
            sectionsContent.anchorMin = new Vector2(0f, 1f);
            sectionsContent.anchorMax = new Vector2(1f, 1f);
            sectionsContent.pivot = new Vector2(0.5f, 1f);
            sectionsContent.anchoredPosition = Vector2.zero;
            sectionsContent.sizeDelta = new Vector2(0f, 0f);

            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(8, 8, 8, 8);
            contentLayout.spacing = 4f;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;

            var contentFitter = content.GetComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbar = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbar.transform.SetParent(body.transform, worldPositionStays: false);
            var srt = (RectTransform)scrollbar.transform;
            srt.anchorMin = new Vector2(1f, 0f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 1f);
            srt.sizeDelta = new Vector2(OniUiTokens.ScrollbarWidth, 0f);
            srt.anchoredPosition = Vector2.zero;
            var trackImg = scrollbar.GetComponent<Image>();
            trackImg.color = OniUiTokens.ScrollbarTrackColor;
            if (OniUiTokens.ScrollbarTrackSprite != null) {
                trackImg.sprite = OniUiTokens.ScrollbarTrackSprite;
                trackImg.type   = Image.Type.Sliced;
            }

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(scrollbar.transform, worldPositionStays: false);
            // Pre-clean the handle's RectTransform: Scrollbar.UpdateVisuals only drives the
            // handle's anchors — leftover sizeDelta gets added to the anchor-stretched size,
            // so a fresh RectTransform's default (100,100) blooms into a giant grey blob.
            var handleRT = (RectTransform)handle.transform;
            handleRT.anchorMin        = Vector2.zero;
            handleRT.anchorMax        = Vector2.one;
            handleRT.sizeDelta        = OniUiTokens.ScrollbarHandleInset;
            handleRT.anchoredPosition = Vector2.zero;
            var handleImage = handle.GetComponent<Image>();
            handleImage.color = OniUiTokens.ScrollbarHandleColor;
            if (OniUiTokens.ScrollbarHandleSprite != null) {
                handleImage.sprite = OniUiTokens.ScrollbarHandleSprite;
                handleImage.type   = Image.Type.Sliced;
            }

            var scrollbarComponent = scrollbar.GetComponent<Scrollbar>();
            scrollbarComponent.direction = Scrollbar.Direction.BottomToTop;
            scrollbarComponent.handleRect = handleRT;
            scrollbarComponent.targetGraphic = handleImage;
            scrollbarComponent.size = 0.25f;

            var scrollRect = body.GetComponent<ScrollRect>();
            scrollRect.viewport = vrt;
            scrollRect.content = sectionsContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.verticalScrollbar = scrollbarComponent;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollRect.movementType      = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity        = OniUiTokens.ScrollElasticity;
            scrollRect.inertia           = OniUiTokens.ScrollInertia;
            scrollRect.decelerationRate  = OniUiTokens.ScrollDecelerationRate;
            scrollRect.scrollSensitivity = OniUiTokens.ScrollSensitivity;
        }

        private void RebuildSections() {
            for (int i = 0; i < sections.Count; i++) {
                UnityEngine.Object.Destroy(sections[i].Root);
            }
            sections.Clear();
            visibleRows.Clear();

            var order = new List<string>(32);
            var grouped = new Dictionary<string, List<IQuickAction>>(StringComparer.Ordinal);
            var titles = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < currentResults.Count; i++) {
                var action = currentResults[i].Action;
                var baseKey = string.IsNullOrEmpty(action.SubcategoryKey) ? "default" : action.SubcategoryKey;
                var baseTitle = string.IsNullOrEmpty(action.SubcategoryTitle) ? baseKey : action.SubcategoryTitle;

                string key = baseKey;
                string title = baseTitle;
                if (action.SearchDemotionTier > 0) {
                    var suffix = string.IsNullOrEmpty(action.SearchDemotionSuffix)
                        ? "demoted"
                        : action.SearchDemotionSuffix;
                    key = baseKey + "__demoted__" + action.SearchDemotionTier + "__" + suffix;
                    title = baseTitle + " (" + suffix + ")";
                }

                if (!grouped.TryGetValue(key, out var bucket)) {
                    bucket = new List<IQuickAction>(16);
                    grouped[key] = bucket;
                    order.Add(key);
                    titles[key] = title;
                }
                bucket.Add(action);
            }

            for (int i = 0; i < order.Count; i++) {
                var key = order[i];
                var section = SectionWidget.Create(
                    sectionsContent,
                    key,
                    titles[key],
                    grouped[key],
                    IsSectionExpanded(key),
                    OnSectionToggled,
                    SubmitAction
                );
                sections.Add(section);
            }

            RefreshVisibleRows();
            Highlight(0);
        }

        private bool IsSectionExpanded(string key) {
            if (expandedSections.TryGetValue(key, out var expanded)) return expanded;
            expandedSections[key] = true;
            return true;
        }

        private void OnSectionToggled(string key, bool expanded) {
            expandedSections[key] = expanded;
            RefreshVisibleRows();
            Highlight(highlighted);
        }

        private void RefreshVisibleRows() {
            visibleRows.Clear();
            for (int i = 0; i < sections.Count; i++) {
                sections[i].AppendVisibleRows(visibleRows);
            }
        }

        private sealed class SectionWidget {
            public readonly GameObject Root;
            private readonly GameObject rowsContainer;
            private readonly Action<bool> applyArrow;
            private readonly string key;
            private readonly Action<string, bool> onToggled;
            private bool expanded;
            private readonly List<RowWidget> rows;

            // Mirrors Klei's subgroup header structure (path map in OniUiTokens.cs):
            //   [BarLeft 8x2][Arrow 12x8][Label auto][BarRight flex×2]
            // Arrow rotates -90° z-axis when collapsed (sprite=iconDown points down).
            // Falls back to a Unicode glyph if SectionArrowSprite isn't extracted.
            public static SectionWidget Create(
                Transform parent,
                string key,
                string title,
                List<IQuickAction> actions,
                bool expanded,
                Action<string, bool> onToggled,
                Action<IQuickAction> onRowClicked
            ) {
                var root = new GameObject("Section_" + key, typeof(RectTransform), typeof(VerticalLayoutGroup));
                root.transform.SetParent(parent, worldPositionStays: false);

                var rootLayout = root.GetComponent<VerticalLayoutGroup>();
                rootLayout.spacing = 2f;
                rootLayout.padding = new RectOffset(0, 0, 0, 0);
                rootLayout.childForceExpandWidth = true;
                rootLayout.childForceExpandHeight = false;
                rootLayout.childControlWidth = true;
                rootLayout.childControlHeight = true;

                var header = new GameObject("Header", typeof(RectTransform), typeof(LayoutElement), typeof(Button), typeof(Image), typeof(HorizontalLayoutGroup));
                header.transform.SetParent(root.transform, worldPositionStays: false);
                var headerLE = header.GetComponent<LayoutElement>();
                headerLE.minHeight = headerLE.preferredHeight = OniUiTokens.SectionHeight;
                header.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

                var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
                headerLayout.padding = new RectOffset(0, 0, 0, 0);
                headerLayout.spacing = 4f;
                headerLayout.childAlignment = TextAnchor.MiddleLeft;
                headerLayout.childForceExpandWidth  = false;
                headerLayout.childForceExpandHeight = false;  // critical: don't stretch the 2px bars
                headerLayout.childControlHeight     = true;
                headerLayout.childControlWidth      = true;

                BuildBar(header.transform, "BarLeft",
                    fixedWidth: OniUiTokens.SectionBarLeftWidth, flexible: false);

                var applyArrow = BuildArrow(header.transform, expanded);

                var titleGo = new GameObject("Label", typeof(RectTransform), typeof(LayoutElement));
                titleGo.transform.SetParent(header.transform, worldPositionStays: false);
                // No preferredWidth — let TMP's natural width drive layout so
                // BarRight starts immediately after the title text.
                var titleText = titleGo.AddComponent<TextMeshProUGUI>();
                titleText.font = OniUiTokens.SectionFont;
                titleText.fontSize = OniUiTokens.SectionFontSize;
                titleText.color = OniUiTokens.SectionText;
                titleText.alignment = TextAlignmentOptions.MidlineLeft;
                titleText.textWrappingMode = TextWrappingModes.NoWrap;
                titleText.text = title;

                BuildBar(header.transform, "BarRight",
                    fixedWidth: 0f, flexible: true);

                var rowsContainer = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
                rowsContainer.transform.SetParent(root.transform, worldPositionStays: false);
                var rowsLayout = rowsContainer.GetComponent<VerticalLayoutGroup>();
                rowsLayout.spacing = 2f;
                rowsLayout.padding = new RectOffset(0, 0, 0, 0);
                rowsLayout.childForceExpandWidth = true;
                rowsLayout.childForceExpandHeight = false;
                rowsLayout.childControlWidth = true;
                rowsLayout.childControlHeight = true;

                var rows = new List<RowWidget>(actions.Count);
                for (int i = 0; i < actions.Count; i++) {
                    rows.Add(RowWidget.Create(rowsContainer.transform, actions[i], onRowClicked));
                }

                var section = new SectionWidget(root, rowsContainer, applyArrow, key, onToggled, rows, expanded);
                header.GetComponent<Button>().onClick.AddListener(section.Toggle);
                section.ApplyExpanded();
                return section;
            }

            private static void BuildBar(Transform parent, string name, float fixedWidth, bool flexible) {
                var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
                go.transform.SetParent(parent, worldPositionStays: false);
                var le = go.GetComponent<LayoutElement>();
                if (flexible) {
                    le.flexibleWidth = 1f;
                } else {
                    le.minWidth = le.preferredWidth = fixedWidth;
                }
                le.minHeight = le.preferredHeight = OniUiTokens.SectionRuleHeight;

                var img = go.GetComponent<Image>();
                img.color = OniUiTokens.SectionRule;
                if (OniUiTokens.SectionBarSprite != null) {
                    img.sprite = OniUiTokens.SectionBarSprite;
                    img.type   = Image.Type.Sliced;
                }
            }

            private static Action<bool> BuildArrow(Transform parent, bool initiallyExpanded) {
                var go = new GameObject("Arrow", typeof(RectTransform), typeof(LayoutElement));
                go.transform.SetParent(parent, worldPositionStays: false);

                var size = OniUiTokens.SectionArrowSize;
                var le = go.GetComponent<LayoutElement>();
                le.minWidth  = le.preferredWidth  = size.x;
                le.minHeight = le.preferredHeight = size.y;

                var rt = (RectTransform)go.transform;
                rt.sizeDelta = size;

                var sprite = OniUiTokens.SectionArrowSprite;
                if (sprite != null) {
                    var img = go.AddComponent<Image>();
                    img.sprite          = sprite;
                    img.color           = OniUiTokens.SectionText;
                    img.preserveAspect  = true;
                    img.raycastTarget   = false;
                    return e => rt.localEulerAngles = new Vector3(0f, 0f, e ? 0f : -90f);
                } else {
                    // Unicode-glyph fallback. Sized to the arrow rect rather
                    // than SectionFontSize so it doesn't dwarf a tiny sprite slot.
                    var glyph = go.AddComponent<TextMeshProUGUI>();
                    glyph.font          = OniUiTokens.SectionFont;
                    glyph.fontSize      = size.y;
                    glyph.color         = OniUiTokens.SectionText;
                    glyph.alignment     = TextAlignmentOptions.Center;
                    glyph.raycastTarget = false;
                    glyph.text          = initiallyExpanded ? "▼" : "▶";
                    return e => glyph.text = e ? "▼" : "▶";
                }
            }

            private SectionWidget(
                GameObject root,
                GameObject rowsContainer,
                Action<bool> applyArrow,
                string key,
                Action<string, bool> onToggled,
                List<RowWidget> rows,
                bool expanded
            ) {
                Root = root;
                this.rowsContainer = rowsContainer;
                this.applyArrow = applyArrow;
                this.key = key;
                this.onToggled = onToggled;
                this.rows = rows;
                this.expanded = expanded;
            }

            public void AppendVisibleRows(List<RowWidget> dest) {
                if (!expanded) return;
                for (int i = 0; i < rows.Count; i++) dest.Add(rows[i]);
            }

            private void Toggle() {
                expanded = !expanded;
                ApplyExpanded();
                onToggled?.Invoke(key, expanded);
            }

            private void ApplyExpanded() {
                rowsContainer.SetActive(expanded);
                applyArrow(expanded);
            }
        }

        private sealed class RowWidget {
            private readonly GameObject root;
            private readonly Image background;
            private readonly Image icon;
            private readonly Image techBadge;
            private readonly TextMeshProUGUI label;
            private readonly bool isAvailable;
            public IQuickAction Action { get; }

            public static RowWidget Create(Transform parent, IQuickAction action, Action<IQuickAction> onClick) {
                var go = new GameObject("ScopeRow", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, worldPositionStays: false);
                var le = go.GetComponent<LayoutElement>();
                le.minHeight = le.preferredHeight = OniUiTokens.RowHeight;

                var bg = go.GetComponent<Image>();
                bg.raycastTarget = true;
                bool isAvailable = action.IsCurrentlyAvailable;
                var rowSprite = isAvailable ? OniUiTokens.RowBgSprite : OniUiTokens.RowBgDisabledSprite;
                if (rowSprite != null) {
                    bg.sprite = rowSprite;
                    bg.type   = Image.Type.Sliced;  // web_button is 9-sliced
                }

                var button = go.GetComponent<Button>();
                button.targetGraphic = bg;

                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(go.transform, worldPositionStays: false);
                var iconRT = (RectTransform)iconGo.transform;
                iconRT.anchorMin = new Vector2(0f, 0.5f);
                iconRT.anchorMax = new Vector2(0f, 0.5f);
                iconRT.pivot     = new Vector2(0f, 0.5f);
                iconRT.sizeDelta = new Vector2(OniUiTokens.RowIconSize, OniUiTokens.RowIconSize);
                iconRT.anchoredPosition = new Vector2(8f, 0f);
                var icon = iconGo.AddComponent<Image>();
                icon.preserveAspect = true;
                var iconMat = isAvailable ? OniUiTokens.RowIconMaterial : OniUiTokens.RowIconDisabledMaterial;
                if (iconMat != null) icon.material = iconMat;

                var badgeGo = new GameObject("NeedTech", typeof(RectTransform));
                badgeGo.transform.SetParent(iconGo.transform, worldPositionStays: false);
                var badgeRT = (RectTransform)badgeGo.transform;
                badgeRT.anchorMin = new Vector2(0f, 1f);
                badgeRT.anchorMax = new Vector2(0f, 1f);
                badgeRT.pivot = new Vector2(0f, 1f);
                badgeRT.sizeDelta = OniUiTokens.RowNeedsTechSize;
                badgeRT.anchoredPosition = Vector2.zero;
                var techBadge = badgeGo.AddComponent<Image>();
                techBadge.raycastTarget = false;
                techBadge.sprite = OniUiTokens.RowNeedsTechSprite;
                techBadge.color = OniUiTokens.RowNeedsTechColor;
                techBadge.enabled = action is BuildingSelectAction buildingAction
                    && buildingAction.RequirementsState == PlanScreen.RequirementsState.Tech
                    && OniUiTokens.RowNeedsTechSprite != null;

                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(go.transform, worldPositionStays: false);
                var lrt = (RectTransform)labelGo.transform;
                lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
                lrt.offsetMin = new Vector2(8f + OniUiTokens.RowIconSize + 8f, 0f);
                lrt.offsetMax = new Vector2(-8f, 0f);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.font              = OniUiTokens.RowFont;
                label.fontSize          = OniUiTokens.RowFontSize;
                label.color             = OniUiTokens.RowText;
                label.alignment         = TextAlignmentOptions.MidlineLeft;
                label.textWrappingMode  = TextWrappingModes.NoWrap;
                label.text              = action.DisplayName;

                icon.sprite = action.Sprite;
                icon.enabled = action.Sprite != null;

                var row = new RowWidget(go, bg, icon, techBadge, label, action, isAvailable);
                button.onClick.AddListener(() => onClick(row.Action));
                row.SetHighlighted(false);

                return row;
            }

            private RowWidget(GameObject g, Image bg, Image ic, Image tech, TextMeshProUGUI lbl, IQuickAction action, bool isAvailable) {
                root = g;
                background = bg;
                icon = ic;
                techBadge = tech;
                label = lbl;
                Action = action;
                this.isAvailable = isAvailable;
            }

            public void SetHighlighted(bool on) {
                background.color = isAvailable
                    ? (on ? OniUiTokens.RowBgHover : OniUiTokens.RowBgNormal)
                    : (on ? OniUiTokens.RowBgDisabledHover : OniUiTokens.RowBgDisabled);
            }
        }
    }

    internal static class ScopeProviders {
        public static readonly List<IActionProvider> All = new List<IActionProvider> {
            new BuildingActionProvider(),
        };
    }

    // Persistent host for transient coroutines that must outlive the overlay's GameObject.
    internal sealed class ScopeCoroutineHost : MonoBehaviour {
        private static ScopeCoroutineHost instance;
        public static void Run(IEnumerator routine) {
            if (instance == null) {
                var go = new GameObject("ScopeCoroutineHost");
                UnityEngine.Object.DontDestroyOnLoad(go);
                instance = go.AddComponent<ScopeCoroutineHost>();
            }
            instance.StartCoroutine(routine);
        }
    }
}
