using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PeterHan.PLib.UI;
using Roslyn.Utilities;
using ScopeMod.Mru;
using ScopeMod.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeMod;

// Ensure TMP_InputField has already processed arrow keys via EventSystem
// before our Update runs the caret-restore.
[DefaultExecutionOrder(int.MaxValue)]
internal sealed class ScopeOverlay : KScreen
{
   private const int MAX_RESULTS = 120;
   private const float PANEL_WIDTH = 520f;
   private const float PANEL_HEIGHT = 560f;

   private static ScopeOverlay liveInstance;

   // Path follows the PLib POptions convention
   // (...\Documents\Klei\OxygenNotIncluded\mods\config\<mod>\) so we
   // land alongside any options-state.
   private static MruStore mruStore;
   private static MruStore Mru
   {
      get
      {
         if (mruStore != null)
            return mruStore;
         Action<string> logError = msg => Log.Error(msg);
         try
         {
            var path = Path.Combine(
               KMod.Manager.GetDirectory(),
               "config",
               "ScopeMod",
               "scope-mru.json"
            );
            mruStore = MruStore.ForFile(path, logError);
            mruStore.Load();
         }
         catch (Exception ex)
         {
            logError($"MRU init failed; running without persistence: {ex.Message}");
            // In-memory-only fallback so call sites don't need null-checks.
            mruStore = new MruStore(loader: () => "", saver: _ => { }, logError);
         }
         return mruStore;
      }
   }

   private TMP_InputField inputField;
   private RectTransform sectionsContent;
   private RectTransform viewportRT;
   private ScrollRect bodyScroll;
   private GameObject emptyState;
   private bool suppressEndEditHandling;
   private readonly List<SectionWidget> sections = new(32);
   private readonly List<RowWidget> visibleRows = new(MAX_RESULTS);
   private readonly Dictionary<string, bool> expandedSections = new(StringComparer.Ordinal);

   private List<RankedResult> currentResults = new(MAX_RESULTS);
   private readonly ScopeSelection selection = new();
   private readonly List<IQuickAction> allActions = new(256);
   private readonly List<ProviderSession> providerSessions = new(8);

   private int caretBeforeArrowKeyOverride = -1;

   private int resultsFingerprint;
   private bool hasResultsFingerprint;

   // Delegate caches to avoid instance-allocations (HAA0602)
   private Func<Vector2, int?> findRowAtDelegate;
   private Func<Vector2, bool> isPointerOverPanelDelegate;

   // Sort key 60 sits above EDITING_SCREEN (50) and below MODAL (100). Receives input
   // before BuildingGroupScreen's KInputTextField (sort 0) per KScreenManager's
   // reverse-stack dispatch (top-most first). May need tweaking.
   public override float GetSortKey() => 60f;

   public override bool IsModal() => false; // explicit: do NOT pause game

   public static void Open()
   {
      if (liveInstance != null)
      {
         Log.Trace("Open() — refocusing live instance.");
         liveInstance.FocusInput();
         return;
      }
      Log.Trace("Open() — creating overlay.");
      var parent = GameScreenManager.Instance.GetParent(
         GameScreenManager.UIRenderTarget.ScreenSpaceOverlay
      );
      // GraphicRaycaster is required alongside the Canvas — without it,
      // Unity's EventSystem can't dispatch pointer events into our hierarchy
      // and clicks pass through to the world. The parent's GraphicRaycaster
      // doesn't reach us once we add our own Canvas.
      var go = new GameObject(
         "ScopeOverlay",
         typeof(RectTransform),
         typeof(Canvas),
         typeof(GraphicRaycaster)
      );
      go.transform.SetParent(parent.transform, worldPositionStays: false);
      go.layer = parent.layer;
      var screen = go.AddComponent<ScopeOverlay>();
      liveInstance = screen;
      screen.Activate();
   }

   public override void OnPrefabInit()
   {
      base.OnPrefabInit();
      BuildUI();
   }

   // OnSpawn is part of Klei's prefab-spawn pipeline (KMonoBehaviour). When we
   // instantiate manually via gameObject.AddComponent<ScopeOverlay>() it never fires —
   // only Awake → OnPrefabInit and Activate() → OnActivate do. So all post-construction
   // init happens in OnActivate.

   public override void OnActivate()
   {
      base.OnActivate();

      var rt = (RectTransform)transform;
      rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
      rt.pivot = new Vector2(0.5f, 0.5f);
      rt.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
      rt.anchoredPosition = Vector2.zero;

      inputField.text = "";
      FocusInput();
      selection.Reset(Input.mousePosition, CurrentScrollPos());
      findRowAtDelegate = FindRowAt;
      isPointerOverPanelDelegate = IsPointerOverPanel;

      BeginProviderSessions();

      // Initial population; enumerate every provider once and `Rank`
      RebuildAllActionsAndRank();
   }

   public override void OnDeactivate()
   {
      EndProviderSessions();

      base.OnDeactivate();
      if (liveInstance == this)
         liveInstance = null;
   }

   private void BeginProviderSessions()
   {
      providerSessions.Clear();
      float now = Time.unscaledTime;
      foreach (var p in ScopeProviders.All)
      {
         var session = new ProviderSession(p);
         session.BeginSession(now);
         providerSessions.Add(session);
      }
   }

   private void EndProviderSessions()
   {
      for (int i = 0; i < providerSessions.Count; i++)
         providerSessions[i].EndSession();
      providerSessions.Clear();
      allActions.Clear();
   }

   // Defensive consumption at the Klei pipeline so letter keys don't fire
   // game hotkeys (e.g. 'c' → cancel-tool) while the overlay is open.
   // ScopeInputFieldEvents at sort 99 covers the focused case (and lets
   // wheel + Escape events through); this covers the gap when the field
   // momentarily isn't focused.
   //
   // Escape invariant: while scope is open, scope owns Escape. Pause menu
   // (opened by RootMenu at sort -1 via Action.Escape) MUST NOT open before
   // scope closes. We sit at sort 60, so consuming Escape here blocks
   // RootMenu, ManagementMenu (sort 21), and PauseScreen (sort 30). Modals
   // at sort 100 still take Escape first as expected.
   //
   // Future gap: a non-modal overlay opened *after* scope (while scope is
   // still visible — e.g. via a "click-out keeps scope open" UX, currently
   // TBD) would sit at lower sort than us and thus get Escape after we've
   // already consumed. Handling that correctly needs stack-temporal
   // bookkeeping ("most-recently- opened wins") which isn't necessary today
   // because click-outside deactivates scope synchronously.
   public override void OnKeyDown(KButtonEvent e)
   {
      if (e.IsAction(Action.ZoomIn) || e.IsAction(Action.ZoomOut))
      {
         if (IsPointerOverPanel())
            e.Consumed = true;
         return;
      }
      if (e.IsAction(Action.Escape))
      {
         e.Consumed = true;
         Deactivate();
         return;
      }
      if (IsInputFocused)
         e.Consumed = true;
   }

   public override void OnKeyUp(KButtonEvent e)
   {
      if (e.IsAction(Action.ZoomIn) || e.IsAction(Action.ZoomOut))
         return;
      if (e.IsAction(Action.Escape))
         return;
      if (IsInputFocused)
         e.Consumed = true;
   }

   private bool IsPointerOverPanel() => IsPointerOverPanel(Input.mousePosition);

   // Normalized vertical scroll; ~[0..1]-ish. (0 during buildUI; slightly
   // over/under during elastic overscrolling)
   private float CurrentScrollPos() =>
      bodyScroll != null ? bodyScroll.verticalNormalizedPosition : 0f;

   [PerformanceSensitive("scope-overlay-per-frame")]
   private bool IsPointerOverPanel(Vector2 screenPos)
   {
      var rt = (RectTransform)transform;
      if (rt == null)
         return false;
      return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt,
            screenPos,
            null,
            out var local
         ) && rt.rect.Contains(local);
   }

   // Navigation / dismiss runs in Update() rather than OnKey* because our
   // ScopeInputFieldEvents at sort 99 consumes Klei key events while the
   // field is focused, so this KScreen's sort-60 OnKeyDown/Up never fires
   // for typed keys. Unity's Input polling sees keys regardless.
   //
   // NOTE: Ordering and timing sensitive;
   //   1. PollMouse: picks up cursor delta from the previous frame
   //   2. Provider tick: re-enumerate dirty providers; preserves Attention
   //   3. RenderHighlight: paints whatever the above resolved to
   //   4. Keyboard handling (arrows, click-outside): last, so a same-frame
   //      arrow-press sets Attention=Keyboard *after* PollMouse, which is
   //      the contract for "most-recent input wins"
   public void Update()
   {
      selection.PollMouse(
         Input.mousePosition,
         CurrentScrollPos(),
         findRowAtDelegate,
         isPointerOverPanelDelegate
      );

      TickProviders();

      RenderHighlight();

      // Keyboard nav is gated on IsInputFocused — that's our canonical
      // "scope owns the keyboard" signal. Scope can be long-lived (e.g.
      // calculator flow with the user opening/closing other UIs to reading
      // numbers), so when the user has clicked out of the input field we
      // deliberately yield arrows + Enter to whatever they're attending to.
      // The fix for intra-scope clicks (section toggles, panel empty-space)
      // is to restore input focus, not to capture keys regardless of focus.
      // Enter when focused goes through TMP's onEndEdit → Submit.
      if (IsInputFocused)
      {
         if (Input.GetKeyDown(KeyCode.UpArrow))
         {
            selection.SetKeyboard(selection.KeyboardRow - 1, visibleRows.Count);
            RenderHighlight();
            UndoTmpCaretMove();
            return;
         }
         if (Input.GetKeyDown(KeyCode.DownArrow))
         {
            selection.SetKeyboard(selection.KeyboardRow + 1, visibleRows.Count);
            RenderHighlight();
            UndoTmpCaretMove();
            return;
         }
      }

      // Click outside the panel dismisses. The panel itself catches its own
      // clicks via Image.raycastTarget, so this only fires when the cursor is over
      // empty space / the game world.
      if (Input.GetMouseButtonDown(0) && !IsPointerOverPanel())
      {
         if (IsInputFocused)
            ReleaseInputFocus();
         else
            Deactivate();
         return;
      }
   }

   // Viewport gate avoids scrolled-out rows hit-testing through the mask (a
   // row scrolled above/below the visible region - e.g. the subheader.)
   // RectangleContainsScreenPoint ignores parent masks, so we filter by the
   // viewport before iterating
   [PerformanceSensitive("scope-overlay-per-frame")]
   private int? FindRowAt(Vector2 screenPos)
   {
      if (
         viewportRT == null
         || !RectTransformUtility.RectangleContainsScreenPoint(viewportRT, screenPos, null)
      )
         return null;
      for (int i = 0; i < visibleRows.Count; i++)
      {
         if (visibleRows[i].ContainsScreenPoint(screenPos))
            return i;
      }
      return null;
   }

   // Single render site for the highlight visual. eff==null is valid and
   // means no row is currently selected (mouse-attentive but cursor in a
   // gap); every row gets SetHighlighted(false).
   [PerformanceSensitive("scope-overlay-per-frame")]
   private void RenderHighlight()
   {
      var eff = selection.Effective;
      for (int i = 0; i < visibleRows.Count; i++)
         visibleRows[i].SetHighlighted(eff.HasValue && i == eff.Value);
   }

   public void LateUpdate()
   {
      if (IsInputFocused)
         caretBeforeArrowKeyOverride = inputField.caretPosition;
   }

   private void UndoTmpCaretMove()
   {
      if (caretBeforeArrowKeyOverride < 0 || inputField == null)
         return;
      int pos = Mathf.Clamp(caretBeforeArrowKeyOverride, 0, inputField.text?.Length ?? 0);
      inputField.caretPosition = pos;
      inputField.selectionAnchorPosition = pos;
      inputField.selectionFocusPosition = pos;
   }

   private void Submit()
   {
      if (visibleRows.Count == 0)
      {
         ReleaseInputFocus();
         return;
      }

      // In all cases, 'no effective selection' means <return> is a no-op.
      // Per the highlight<->return invariant, nothing renders highlighted in
      // this state, so activating "nothing" would be confusing. The user's
      // select will return when they 'finish' moving the mouse into the next
      // row up, or leave the window (where it returns to the most-recent
      // keyboard selection.)
      var eff = selection.Effective;
      if (!eff.HasValue)
      {
         Log.Debug("Submit() noop: cursor between rows in mouse-attentive mode.");
         return;
      }

      var picked = visibleRows[Mathf.Clamp(eff.Value, 0, visibleRows.Count - 1)].Action;
      // Invariant: <enter> only closes Scope in order to execute some useful task; never on failure.
      if (!picked.CanInvoke)
      {
         Log.Warn($"Submit blocked (CanInvoke=false): {picked.DisplayName}");
         return;
      }
      SubmitAction(picked);
   }

   private void SubmitAction(IQuickAction picked)
   {
      Log.Debug($"Submit: {picked?.DisplayName ?? "<null>"}");

      // Best-effort: MruStore logs but doesn't throw on disk failure.
      var key = picked?.MruKey;
      if (!string.IsNullOrEmpty(key))
      {
         Mru.Record(key);
         Mru.Save();
      }

      // Defer invocation past Input.anyKeyDown so Klei's KeyDown event
      // doesn't fall through to game hotkeys. Hosted on a persistent
      // GameObject because we're about to Destroy ourselves.
      ScopeCoroutineHost.Run(WaitThenInvoke(picked));
      Deactivate();
   }

   private static IEnumerator WaitThenInvoke(IQuickAction action)
   {
      while (Input.anyKeyDown)
         yield return null;
      try
      {
         action.Invoke();
      }
      catch (System.Exception ex)
      {
         Log.Error(() => $"Action threw: {ex}");
      }
   }

   private void UpdateResults(string query)
   {
      currentResults = ScopeSearch.Rank(query, allActions, MAX_RESULTS, Mru);
      int fp = FingerprintResults(currentResults);
      if (hasResultsFingerprint && fp == resultsFingerprint)
         return;
      resultsFingerprint = fp;
      hasResultsFingerprint = true;
      RebuildSections();
   }

   // Wired to inputField.onValueChanged. Typing is keyboard input, so we
   // claim Attention=Keyboard and snap K to the top result. Order matters:
   // UpdateResults may rebuild rows (changing visibleRows.Count); SetKeyboard
   // must run after so it clamps against the post-rebuild count. The
   // OnRowsRebuilt call inside RebuildSections deliberately doesn't touch
   // Attention (see ScopeSelection class header) — this is where typing
   // explicitly converts a heartbeat-style rebuild into a keyboard-input
   // event.
   private void HandleInputValueChanged(string query)
   {
      UpdateResults(query);
      selection.SetKeyboard(0, visibleRows.Count);
      RenderHighlight();
   }

   [PerformanceSensitive("scope-search-hot-path")]
   private static int FingerprintResults(List<RankedResult> results)
   {
      var hc = new HashCode();
      for (int i = 0; i < results.Count; i++)
      {
         var a = results[i].Action;
         hc.Add(a.MruKey);
         hc.Add(a.RenderStateHash);
      }
      return hc.ToHashCode();
   }

   // Steady-state/zero-alloc tick; every frame,
   //  - poll opt-in `OnPoll` providers so they can update their dirty;
   //  - and `RebuildAllActionsAndRank` if any (polled or evented) provider
   //    dirtied.
   //
   // (`UpdateResults` retains its fingerprinting mechanism as a
   // second-line-of-defense; UI won't rebuild even if dirtied unless the
   // dirty-state changes the fingerprint.)
   private void TickProviders()
   {
      float now = Time.unscaledTime;
      bool anyDirty = false;
      for (int i = 0; i < providerSessions.Count; i++)
      {
         providerSessions[i].TickPoll(now);
         if (providerSessions[i].Dirty)
            anyDirty = true;
      }

      if (anyDirty)
         RebuildAllActionsAndRank();
   }

   // NOTE: provider-rebuilds are explicitly NOT input: Attention must survive
   //    so a mouse hover doesn't get clobbered. User-initiated rebuilds go
   //    through `HandleInputValueChanged` which flips Attention=Keyboard.
   private void RebuildAllActionsAndRank()
   {
      for (int i = 0; i < providerSessions.Count; i++)
      {
         if (providerSessions[i].Dirty)
            providerSessions[i].RebuildCache();
      }

      allActions.Clear();
      for (int i = 0; i < providerSessions.Count; i++)
      {
         var cache = providerSessions[i].CachedActions;
         for (int j = 0; j < cache.Count; j++)
            allActions.Add(cache[j]);
      }

      UpdateResults(inputField?.text ?? "");
   }

   private void BuildUI()
   {
      // Force-open the first build category so BuildingGroupScreen gets
      // instantiated. See OniUiTokens.Warmup() for full rationale. Tokens
      // are also lazy + retried per access, so a failed warmup is
      // recoverable on subsequent overlay-opens.
      bool wasHydrated = BuildingGroupScreen.Instance != null;
      OniUiTokens.Warmup();
      bool isHydrated = BuildingGroupScreen.Instance != null;
      Log.Debug(
         $"BuildUI: BuildingGroupScreen hydrated before/after warmup = {wasHydrated}/{isHydrated}."
      );
      OniUiTokens.LogPerOpen();

      var border = gameObject.AddComponent<Image>();
      border.sprite = OniUiTokens.PanelBgSprite ?? PUITuning.Images.BoxBorder;
      border.type = Image.Type.Sliced;
      border.color = OniUiTokens.PanelBgColor;
      border.raycastTarget = true;

      var bodyGo = new GameObject("Body", typeof(RectTransform));
      bodyGo.transform.SetParent(transform, worldPositionStays: false);
      var bodyRT = (RectTransform)bodyGo.transform;
      bodyRT.anchorMin = Vector2.zero;
      bodyRT.anchorMax = Vector2.one;
      bodyRT.offsetMin = new Vector2(1f, 1f);
      bodyRT.offsetMax = new Vector2(-1f, -1f);
      var bodyBg = bodyGo.AddComponent<Image>();
      bodyBg.color = Color.white;

      var layout = bodyGo.AddComponent<VerticalLayoutGroup>();
      layout.padding = new RectOffset(0, 0, 0, 0);
      layout.spacing = 0f;
      layout.childAlignment = TextAnchor.UpperLeft;
      layout.childForceExpandWidth = true;
      layout.childForceExpandHeight = false;

      BuildHeader(bodyGo.transform);
      BuildSubheader(bodyGo.transform);
      BuildBody(bodyGo.transform);
   }

   private static void BuildHeader(Transform parent)
   {
      var go = new GameObject("Header", typeof(RectTransform), typeof(LayoutElement));
      go.transform.SetParent(parent, worldPositionStays: false);
      var le = go.GetComponent<LayoutElement>();
      le.minHeight = le.preferredHeight = OniUiTokens.HeaderHeight;

      var bg = go.AddComponent<Image>();
      bg.color = OniUiTokens.HeaderBg;

      var labelGo = new GameObject("Title", typeof(RectTransform));
      labelGo.transform.SetParent(go.transform, worldPositionStays: false);
      var label = labelGo.AddComponent<TextMeshProUGUI>();
      label.font = OniUiTokens.HeaderFont;
      label.fontSize = OniUiTokens.HeaderFontSize;
      label.color = OniUiTokens.HeaderText;
      label.alignment = TextAlignmentOptions.MidlineLeft;
      label.textWrappingMode = TextWrappingModes.NoWrap;
      label.text = "SCOPE";
      var lrt = (RectTransform)labelGo.transform;
      lrt.anchorMin = Vector2.zero;
      lrt.anchorMax = Vector2.one;
      lrt.offsetMin = new Vector2(10f, 0f);
      lrt.offsetMax = new Vector2(-10f, 0f);
   }

   private void BuildSubheader(Transform parent)
   {
      var subheader = new GameObject(
         "Subheader",
         typeof(RectTransform),
         typeof(LayoutElement),
         typeof(Image)
      );
      subheader.transform.SetParent(parent, worldPositionStays: false);
      var subheaderLE = subheader.GetComponent<LayoutElement>();
      subheaderLE.minHeight = subheaderLE.preferredHeight = OniUiTokens.SubheaderHeight;
      subheaderLE.flexibleHeight = 0f;
      subheader.GetComponent<Image>().color = OniUiTokens.SubheaderBg;

      var subheaderLayout = subheader.AddComponent<HorizontalLayoutGroup>();
      // Vertical padding leaves the input centred at its natural height
      // inside the (taller) subheader band.
      var subheaderVPad = Mathf.Max(
         2,
         (int)((OniUiTokens.SubheaderHeight - OniUiTokens.InputHeight) / 2f)
      );
      subheaderLayout.padding = new RectOffset(8, 8, subheaderVPad, subheaderVPad);
      subheaderLayout.spacing = 6f;
      subheaderLayout.childForceExpandHeight = true;
      subheaderLayout.childForceExpandWidth = false;
      subheaderLayout.childControlHeight = true;
      subheaderLayout.childControlWidth = true;

      var pField = new PTextField("ScopeInput")
      {
         Text = "",
         PlaceholderText = STRINGS.UI.BUILDMENU.SEARCH_TEXT_PLACEHOLDER,
         MaxLength = 64,
         MinWidth = (int)(PANEL_WIDTH - 96),
         FlexSize = new Vector2(1f, 0f),
         Type = PTextField.FieldType.Text,
         BackColor = OniUiTokens.InputBg,
         TextAlignment = TextAlignmentOptions.Left,
         TextStyle = PUITuning.Fonts.TextDarkStyle,
      };
      var fieldGo = pField.Build();
      fieldGo.transform.SetParent(subheader.transform, worldPositionStays: false);

      // Swap PLib's PTextFieldEvents for ours: PLib consumes ALL KButtonEvents
      // at sort 99 while editing, eating Action.ZoomIn/ZoomOut before
      // CameraController sees them. Ours lets those two pass through.
      // Looked up by string because PLib marks the type internal.
      var plibEvents = fieldGo.GetComponent("PTextFieldEvents");
      if (plibEvents != null)
         UnityEngine.Object.Destroy(plibEvents);
      fieldGo.AddComponent<ScopeInputFieldEvents>();

      var fieldLE = fieldGo.GetComponent<LayoutElement>();
      if (fieldLE == null)
         fieldLE = fieldGo.AddComponent<LayoutElement>();
      fieldLE.flexibleWidth = 1f;
      fieldLE.minHeight = fieldLE.preferredHeight = OniUiTokens.InputHeight;

      var border = fieldGo.GetComponent<Image>();
      if (border != null)
         border.color = Color.white;

      inputField = fieldGo.GetComponent<TMP_InputField>();
      inputField.onSelect.AddListener(_ => HandleInputSelect());
      inputField.onEndEdit.AddListener(HandleInputEndEdit);

      // Live filtering: PTextField's OnTextChanged delegate only fires on EndEdit
      // (Enter / blur). Wire onValueChanged directly for keystroke-by-keystroke
      // updates.
      inputField.onValueChanged.AddListener(HandleInputValueChanged);

      if (inputField.placeholder is TextMeshProUGUI placeholder)
      {
         placeholder.fontStyle = FontStyles.Italic;
         placeholder.color = OniUiTokens.InputPlaceholder;
         placeholder.font = OniUiTokens.InputFont;
         placeholder.fontSize = OniUiTokens.InputFontSize;
      }

      if (inputField.textComponent is TextMeshProUGUI text)
      {
         text.fontStyle = FontStyles.Normal;
         text.color = OniUiTokens.InputText;
         text.font = OniUiTokens.InputFont;
         text.fontSize = OniUiTokens.InputFontSize;
      }

      inputField.restoreOriginalTextOnEscape = false;

      BuildClearButton(subheader.transform);
   }

   private void BuildClearButton(Transform parent)
   {
      var clearGo = new GameObject(
         "ClearButton",
         typeof(RectTransform),
         typeof(LayoutElement),
         typeof(Image),
         typeof(Button)
      );
      clearGo.transform.SetParent(parent, worldPositionStays: false);

      var clearLE = clearGo.GetComponent<LayoutElement>();
      clearLE.minWidth = clearLE.preferredWidth = OniUiTokens.InputHeight;
      clearLE.minHeight = clearLE.preferredHeight = OniUiTokens.InputHeight;

      var clearBg = clearGo.GetComponent<Image>();
      clearBg.color = OniUiTokens.ClearButtonBgColor;
      var bgSprite = OniUiTokens.ClearButtonBgSprite;
      if (bgSprite != null)
      {
         clearBg.sprite = bgSprite;
         clearBg.type = Image.Type.Sliced;
      }

      var clearButton = clearGo.GetComponent<Button>();
      clearButton.targetGraphic = clearBg;
      clearButton.onClick.AddListener(() =>
      {
         inputField.text = "";
         FocusInput();
      });

      // Decide Image-vs-TMP up-front: Unity allows only one Graphic per
      // GameObject, and the recycle (add Image → Destroy → add TMP)
      // returns null because Destroy is deferred to end-of-frame.
      var fgSprite = OniUiTokens.ClearButtonFgSprite;

      var fgGo = new GameObject("FG", typeof(RectTransform));
      fgGo.transform.SetParent(clearGo.transform, worldPositionStays: false);

      var lrt = (RectTransform)fgGo.transform;
      lrt.anchorMin = Vector2.zero;
      lrt.anchorMax = Vector2.one;
      var fgInset = OniUiTokens.ClearButtonFgInset;
      var fgPadding = new Vector2(Mathf.Abs(fgInset.x) * 0.5f, Mathf.Abs(fgInset.y) * 0.5f);
      lrt.offsetMin = fgPadding;
      lrt.offsetMax = -fgPadding;

      if (fgSprite != null)
      {
         var fgImage = fgGo.AddComponent<Image>();
         fgImage.raycastTarget = false;
         fgImage.color = OniUiTokens.ClearButtonFgColor;
         fgImage.sprite = fgSprite;
         fgImage.preserveAspect = true;
      }
      else
      {
         // Visible-but-not-fatal degraded path: extraction missed the
         // cancel sprite, falling back to a TMP "X" glyph.
         Log.Warn("ClearButton FG fallback: no sprite extracted, using TMP X glyph.");
         var label = fgGo.AddComponent<TextMeshProUGUI>();
         if (label != null)
         {
            var inputFont = OniUiTokens.InputFont;
            if (inputFont != null)
               label.font = inputFont;
            label.fontSize = 16f;
            label.color = OniUiTokens.InputText;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "X";
         }
         else
         {
            Log.Warn(
               "ClearButton FG fallback: AddComponent<TMP> returned null; clear button has no glyph."
            );
         }
      }
   }

   private void BuildBody(Transform parent)
   {
      var body = new GameObject(
         "ResultsBody",
         typeof(RectTransform),
         typeof(LayoutElement),
         typeof(Image),
         typeof(KScrollRect)
      );
      body.transform.SetParent(parent, worldPositionStays: false);

      var bodyLE = body.GetComponent<LayoutElement>();
      bodyLE.flexibleHeight = 1f;
      bodyLE.minHeight = 120f;

      var bodyImage = body.GetComponent<Image>();
      bodyImage.color = OniUiTokens.BodyBg;

      var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
      viewport.transform.SetParent(body.transform, worldPositionStays: false);
      var vrt = (RectTransform)viewport.transform;
      viewportRT = vrt;
      vrt.anchorMin = Vector2.zero;
      vrt.anchorMax = Vector2.one;
      vrt.offsetMin = new Vector2(0f, 0f);
      // 2f gap between viewport's right edge and scrollbar's left edge.
      vrt.offsetMax = new Vector2(
         -(OniUiTokens.ScrollbarWidth + OniUiTokens.ScrollbarMargin.x + 2f),
         0f
      );
      viewport.GetComponent<Image>().color = Color.white;
      viewport.GetComponent<Mask>().showMaskGraphic = false;

      emptyState = new GameObject("EmptyState", typeof(RectTransform), typeof(TextMeshProUGUI));
      emptyState.transform.SetParent(viewport.transform, worldPositionStays: false);
      var emptyRT = (RectTransform)emptyState.transform;
      emptyRT.anchorMin = Vector2.zero;
      emptyRT.anchorMax = Vector2.one;
      emptyRT.offsetMin = new Vector2(16f, 16f);
      emptyRT.offsetMax = new Vector2(-16f, -16f);
      var emptyLabel = emptyState.GetComponent<TextMeshProUGUI>();
      emptyLabel.raycastTarget = false;
      emptyLabel.font = OniUiTokens.InputFont;
      emptyLabel.fontSize = OniUiTokens.InputFontSize;
      emptyLabel.color = new Color32(96, 102, 117, 150);
      emptyLabel.alignment = TextAlignmentOptions.Center;
      emptyLabel.text = "No results";
      emptyState.SetActive(false);

      var content = new GameObject(
         "Content",
         typeof(RectTransform),
         typeof(VerticalLayoutGroup),
         typeof(ContentSizeFitter)
      );
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

      var scrollbar = new GameObject(
         "Scrollbar",
         typeof(RectTransform),
         typeof(Image),
         typeof(Scrollbar)
      );
      scrollbar.transform.SetParent(body.transform, worldPositionStays: false);
      var srt = (RectTransform)scrollbar.transform;
      var scrollbarMargin = OniUiTokens.ScrollbarMargin;
      srt.anchorMin = new Vector2(1f, 0f);
      srt.anchorMax = new Vector2(1f, 1f);
      srt.pivot = new Vector2(1f, 1f);
      srt.sizeDelta = new Vector2(OniUiTokens.ScrollbarWidth, -2f * scrollbarMargin.y);
      srt.anchoredPosition = new Vector2(-scrollbarMargin.x, -scrollbarMargin.y);
      var trackImg = scrollbar.GetComponent<Image>();
      trackImg.color = OniUiTokens.ScrollbarTrackColor;
      if (OniUiTokens.ScrollbarTrackSprite != null)
      {
         trackImg.sprite = OniUiTokens.ScrollbarTrackSprite;
         trackImg.type = Image.Type.Sliced;
      }

      var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
      handle.transform.SetParent(scrollbar.transform, worldPositionStays: false);
      // Pre-clean the handle's RectTransform: Scrollbar.UpdateVisuals only drives the
      // handle's anchors — leftover sizeDelta gets added to the anchor-stretched size,
      // so a fresh RectTransform's default (100,100) blooms into a giant grey blob.
      var handleRT = (RectTransform)handle.transform;
      handleRT.anchorMin = Vector2.zero;
      handleRT.anchorMax = Vector2.one;
      handleRT.sizeDelta = OniUiTokens.ScrollbarHandleInset;
      handleRT.anchoredPosition = Vector2.zero;
      var handleImage = handle.GetComponent<Image>();
      handleImage.color = OniUiTokens.ScrollbarHandleColor;
      if (OniUiTokens.ScrollbarHandleSprite != null)
      {
         handleImage.sprite = OniUiTokens.ScrollbarHandleSprite;
         handleImage.type = Image.Type.Sliced;
      }

      var scrollbarComponent = scrollbar.GetComponent<Scrollbar>();
      scrollbarComponent.direction = Scrollbar.Direction.BottomToTop;
      scrollbarComponent.handleRect = handleRT;
      scrollbarComponent.targetGraphic = handleImage;
      scrollbarComponent.size = 0.25f;

      var scrollRect = body.GetComponent<ScrollRect>();
      bodyScroll = scrollRect;
      scrollRect.viewport = vrt;
      scrollRect.content = sectionsContent;
      scrollRect.horizontal = false;
      scrollRect.vertical = true;
      scrollRect.verticalScrollbar = scrollbarComponent;
      scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
      scrollRect.movementType = ScrollRect.MovementType.Elastic;
      scrollRect.elasticity = OniUiTokens.ScrollElasticity;
      scrollRect.inertia = OniUiTokens.ScrollInertia;
      scrollRect.decelerationRate = OniUiTokens.ScrollDecelerationRate;
   }

   private void RebuildSections()
   {
      for (int i = 0; i < sections.Count; i++)
      {
         UnityEngine.Object.Destroy(sections[i].Root);
      }
      sections.Clear();
      visibleRows.Clear();

      var order = new List<string>(32);
      var grouped = new Dictionary<string, List<IQuickAction>>(StringComparer.Ordinal);
      var titles = new Dictionary<string, string>(StringComparer.Ordinal);

      for (int i = 0; i < currentResults.Count; i++)
      {
         var action = currentResults[i].Action;
         var baseKey = string.IsNullOrEmpty(action.SubcategoryKey)
            ? "default"
            : action.SubcategoryKey;
         var baseTitle = string.IsNullOrEmpty(action.SubcategoryTitle)
            ? baseKey
            : action.SubcategoryTitle;

         string key = baseKey;
         string title = baseTitle;
         if (action.SortTier > SortTier.Normal)
         {
            var suffix = string.IsNullOrEmpty(action.SearchDemotionSuffix)
               ? "demoted"
               : action.SearchDemotionSuffix;
            key = baseKey + "__demoted__" + (int)action.SortTier + "__" + suffix;
            title = baseTitle + " (" + suffix + ")";
         }

         if (!grouped.TryGetValue(key, out var bucket))
         {
            bucket = new List<IQuickAction>(16);
            grouped[key] = bucket;
            order.Add(key);
            titles[key] = title;
         }
         bucket.Add(action);
      }

      for (int i = 0; i < order.Count; i++)
      {
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

      if (emptyState != null)
         emptyState.SetActive(currentResults.Count == 0);

      RefreshVisibleRows();
      // Reclamp K to the new row count without touching Attention
      selection.OnRowsRebuilt(visibleRows.Count);
      RenderHighlight();
   }

   private void FocusInput()
   {
      if (inputField == null)
         return;
      inputField.Select();
      inputField.ActivateInputField();
   }

   private void ReleaseInputFocus()
   {
      if (inputField == null)
         return;

      suppressEndEditHandling = true;
      try
      {
         inputField.DeactivateInputField();
      }
      finally
      {
         suppressEndEditHandling = false;
      }
   }

   private void HandleInputSelect()
   {
      KScreenManager.Instance?.RefreshStack();
   }

   private void HandleInputEndEdit(string _)
   {
      if (suppressEndEditHandling)
         return;

      bool submitted = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
      if (submitted)
      {
         Submit();
         return;
      }

      KScreenManager.Instance?.RefreshStack();
   }

   private bool IsInputFocused => inputField != null && inputField.isFocused;

   private bool IsSectionExpanded(string key)
   {
      if (expandedSections.TryGetValue(key, out var expanded))
         return expanded;
      expandedSections[key] = true;
      return true;
   }

   // Section collapse/expand changes visibleRows without re-ranking. Treated
   // as a layout event, not user input — the click that triggered this is
   // already accounted for through PollMouse (cursor was on the section
   // header, will continue to drive Attention via subsequent motion).
   //
   // We must refocus the input: clicking the section button makes
   // EventSystem deselect the TMP_InputField, which would otherwise drop our
   // "scope owns the keyboard" signal (IsInputFocused). Currently, the
   // canonical scope-active workflow keeps the input field focused
   // throughout intra-scope clicks; only clicks outside the panel yield it.
   private void OnSectionToggled(string key, bool expanded)
   {
      expandedSections[key] = expanded;
      RefreshVisibleRows();
      selection.OnRowsRebuilt(visibleRows.Count);
      RenderHighlight();
      FocusInput();
   }

   private void RefreshVisibleRows()
   {
      visibleRows.Clear();
      for (int i = 0; i < sections.Count; i++)
      {
         sections[i].AppendVisibleRows(visibleRows);
      }
   }

   private sealed class SectionWidget
   {
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
      )
      {
         var root = new GameObject(
            "Section_" + key,
            typeof(RectTransform),
            typeof(VerticalLayoutGroup)
         );
         root.transform.SetParent(parent, worldPositionStays: false);

         var rootLayout = root.GetComponent<VerticalLayoutGroup>();
         rootLayout.spacing = 2f;
         rootLayout.padding = new RectOffset(0, 0, 0, 0);
         rootLayout.childForceExpandWidth = true;
         rootLayout.childForceExpandHeight = false;
         rootLayout.childControlWidth = true;
         rootLayout.childControlHeight = true;

         var header = new GameObject(
            "Header",
            typeof(RectTransform),
            typeof(LayoutElement),
            typeof(Button),
            typeof(Image),
            typeof(HorizontalLayoutGroup)
         );
         header.transform.SetParent(root.transform, worldPositionStays: false);
         var headerLE = header.GetComponent<LayoutElement>();
         headerLE.minHeight = headerLE.preferredHeight = OniUiTokens.SectionHeight;
         header.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

         var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
         headerLayout.padding = new RectOffset(0, 0, 0, 0);
         headerLayout.spacing = 4f;
         headerLayout.childAlignment = TextAnchor.MiddleLeft;
         headerLayout.childForceExpandWidth = false;
         headerLayout.childForceExpandHeight = false; // critical: don't stretch the 2px bars
         headerLayout.childControlHeight = true;
         headerLayout.childControlWidth = true;

         BuildBar(
            header.transform,
            "BarLeft",
            fixedWidth: OniUiTokens.SectionBarLeftWidth,
            flexible: false
         );

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

         BuildBar(header.transform, "BarRight", fixedWidth: 0f, flexible: true);

         var rowsContainer = new GameObject(
            "Rows",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup)
         );
         rowsContainer.transform.SetParent(root.transform, worldPositionStays: false);
         var rowsLayout = rowsContainer.GetComponent<VerticalLayoutGroup>();
         rowsLayout.spacing = 2f;
         rowsLayout.padding = new RectOffset(0, 0, 0, 0);
         rowsLayout.childForceExpandWidth = true;
         rowsLayout.childForceExpandHeight = false;
         rowsLayout.childControlWidth = true;
         rowsLayout.childControlHeight = true;

         var rows = new List<RowWidget>(actions.Count);
         for (int i = 0; i < actions.Count; i++)
         {
            rows.Add(RowWidget.Create(rowsContainer.transform, actions[i], onRowClicked));
         }

         var section = new SectionWidget(
            root,
            rowsContainer,
            applyArrow,
            key,
            onToggled,
            rows,
            expanded
         );
         header.GetComponent<Button>().onClick.AddListener(section.Toggle);
         section.ApplyExpanded();
         return section;
      }

      private static void BuildBar(Transform parent, string name, float fixedWidth, bool flexible)
      {
         var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
         go.transform.SetParent(parent, worldPositionStays: false);
         var le = go.GetComponent<LayoutElement>();
         if (flexible)
         {
            le.flexibleWidth = 1f;
         }
         else
         {
            le.minWidth = le.preferredWidth = fixedWidth;
         }
         le.minHeight = le.preferredHeight = OniUiTokens.SectionRuleHeight;

         var img = go.GetComponent<Image>();
         img.color = OniUiTokens.SectionRule;
         if (OniUiTokens.SectionBarSprite != null)
         {
            img.sprite = OniUiTokens.SectionBarSprite;
            img.type = Image.Type.Sliced;
         }
      }

      private static Action<bool> BuildArrow(Transform parent, bool initiallyExpanded)
      {
         var go = new GameObject("Arrow", typeof(RectTransform), typeof(LayoutElement));
         go.transform.SetParent(parent, worldPositionStays: false);

         var size = OniUiTokens.SectionArrowSize;
         var le = go.GetComponent<LayoutElement>();
         le.minWidth = le.preferredWidth = size.x;
         le.minHeight = le.preferredHeight = size.y;

         var rt = (RectTransform)go.transform;
         rt.sizeDelta = size;

         var sprite = OniUiTokens.SectionArrowSprite;
         if (sprite != null)
         {
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = OniUiTokens.SectionText;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return e => rt.localEulerAngles = new Vector3(0f, 0f, e ? 0f : -90f);
         }
         else
         {
            // Unicode-glyph fallback. Sized to the arrow rect rather
            // than SectionFontSize so it doesn't dwarf a tiny sprite slot.
            var glyph = go.AddComponent<TextMeshProUGUI>();
            glyph.font = OniUiTokens.SectionFont;
            glyph.fontSize = size.y;
            glyph.color = OniUiTokens.SectionText;
            glyph.alignment = TextAlignmentOptions.Center;
            glyph.raycastTarget = false;
            glyph.text = initiallyExpanded ? "▼" : "▶";
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
      )
      {
         Root = root;
         this.rowsContainer = rowsContainer;
         this.applyArrow = applyArrow;
         this.key = key;
         this.onToggled = onToggled;
         this.rows = rows;
         this.expanded = expanded;
      }

      public void AppendVisibleRows(List<RowWidget> dest)
      {
         if (!expanded)
            return;
         for (int i = 0; i < rows.Count; i++)
            dest.Add(rows[i]);
      }

      private void Toggle()
      {
         expanded = !expanded;
         ApplyExpanded();
         onToggled?.Invoke(key, expanded);
      }

      private void ApplyExpanded()
      {
         rowsContainer.SetActive(expanded);
         applyArrow(expanded);
      }
   }

   private sealed class RowWidget
   {
      private readonly GameObject root;
      private readonly RectTransform rt;
      private readonly Image background;
      private readonly Image icon;
      private readonly Image techBadge;
      private readonly TextMeshProUGUI label;
      private readonly bool isAvailable;
      public IQuickAction Action { get; }

      [PerformanceSensitive("scope-overlay-per-frame")]
      public bool ContainsScreenPoint(Vector2 screenPos) =>
         rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);

      public static RowWidget Create(
         Transform parent,
         IQuickAction action,
         Action<IQuickAction> onClick
      )
      {
         var go = new GameObject(
            "ScopeRow",
            typeof(RectTransform),
            typeof(LayoutElement),
            typeof(Image),
            typeof(Button)
         );
         go.transform.SetParent(parent, worldPositionStays: false);
         var le = go.GetComponent<LayoutElement>();
         le.minHeight = le.preferredHeight = OniUiTokens.RowHeight;

         var bg = go.GetComponent<Image>();
         bg.raycastTarget = true;
         bool isAvailable = action.IsCurrentlyAvailable;
         var rowSprite = isAvailable ? OniUiTokens.RowBgSprite : OniUiTokens.RowBgDisabledSprite;
         if (rowSprite != null)
         {
            bg.sprite = rowSprite;
            bg.type = Image.Type.Sliced; // web_button is 9-sliced
         }

         var button = go.GetComponent<Button>();
         button.targetGraphic = bg;
         // Unity Button's default ColorTint fights our our SetHighlighted.
         button.transition = Selectable.Transition.None;

         var iconGo = new GameObject("Icon", typeof(RectTransform));
         iconGo.transform.SetParent(go.transform, worldPositionStays: false);
         var iconRT = (RectTransform)iconGo.transform;
         iconRT.anchorMin = new Vector2(0f, 0.5f);
         iconRT.anchorMax = new Vector2(0f, 0.5f);
         iconRT.pivot = new Vector2(0f, 0.5f);
         iconRT.sizeDelta = new Vector2(OniUiTokens.RowIconSize, OniUiTokens.RowIconSize);
         iconRT.anchoredPosition = new Vector2(8f, 0f);
         var icon = iconGo.AddComponent<Image>();
         icon.preserveAspect = true;
         var iconMat = isAvailable
            ? OniUiTokens.RowIconMaterial
            : OniUiTokens.RowIconDisabledMaterial;
         if (iconMat != null)
            icon.material = iconMat;

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
         techBadge.enabled =
            action is BuildingSelectAction buildingAction
            && buildingAction.RequirementsState == PlanScreen.RequirementsState.Tech
            && OniUiTokens.RowNeedsTechSprite != null;

         var labelGo = new GameObject("Label", typeof(RectTransform));
         labelGo.transform.SetParent(go.transform, worldPositionStays: false);
         var lrt = (RectTransform)labelGo.transform;
         lrt.anchorMin = new Vector2(0f, 0f);
         lrt.anchorMax = new Vector2(1f, 1f);
         lrt.offsetMin = new Vector2(8f + OniUiTokens.RowIconSize + 8f, 0f);
         lrt.offsetMax = new Vector2(-8f, 0f);
         var label = labelGo.AddComponent<TextMeshProUGUI>();
         label.font = OniUiTokens.RowFont;
         label.fontSize = OniUiTokens.RowFontSize;
         label.color = OniUiTokens.RowText;
         label.alignment = TextAlignmentOptions.MidlineLeft;
         label.textWrappingMode = TextWrappingModes.NoWrap;
         label.text = action.DisplayName;

         icon.sprite = action.Sprite;
         icon.enabled = action.Sprite != null;

         var row = new RowWidget(go, bg, icon, techBadge, label, action, isAvailable);
         button.onClick.AddListener(() => onClick(row.Action));
         row.SetHighlighted(false);

         return row;
      }

      private RowWidget(
         GameObject g,
         Image bg,
         Image ic,
         Image tech,
         TextMeshProUGUI lbl,
         IQuickAction action,
         bool isAvailable
      )
      {
         root = g;
         rt = (RectTransform)g.transform;
         background = bg;
         icon = ic;
         techBadge = tech;
         label = lbl;
         Action = action;
         this.isAvailable = isAvailable;
      }

      [PerformanceSensitive("scope-overlay-per-frame")]
      public void SetHighlighted(bool on)
      {
         var ovr = on ? Action.RowBgHoverColorOverride : Action.RowBgColorOverride;
         if (ovr.HasValue)
         {
            background.color = ovr.Value;
            return;
         }
         background.color = isAvailable
            ? (on ? OniUiTokens.RowBgHover : OniUiTokens.RowBgNormal)
            : (on ? OniUiTokens.RowBgDisabledHover : OniUiTokens.RowBgDisabled);
      }
   }
}

internal static class ScopeProviders
{
   public static readonly List<IActionProvider> All = new List<IActionProvider>
   {
      new BuildingActionProvider(),
      new ToolActionProvider(),
      new OverlayActionProvider(),
      new PanelActionProvider(),
      new SystemActionProvider(),
      new DuplicantActionProvider(),
      new PlanetoidActionProvider(),
   };
}

// Persistent host for transient coroutines that must outlive the overlay's GameObject.
internal sealed class ScopeCoroutineHost : MonoBehaviour
{
   private static ScopeCoroutineHost instance;

   public static void Run(IEnumerator routine)
   {
      if (instance == null)
      {
         var go = new GameObject("ScopeCoroutineHost");
         UnityEngine.Object.DontDestroyOnLoad(go);
         instance = go.AddComponent<ScopeCoroutineHost>();
      }
      instance.StartCoroutine(routine);
   }
}
