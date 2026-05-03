using System;
using System.Collections;
using System.Linq;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeMod.UI
{
   // Lifts colours / fonts / sprites / sizes off Klei's live UI. Every
   // token is lazy and retried per access until extraction succeeds; misses
   // fall back to ScopeUiDefaults. I'd rather visual drift than everyone get
   // hard crashes and have to bisect mods lol.
   //
   // (Why did I decide to not just write this in F#, again?)
   //
   // Path map (last sampled 2026-04-30):
   //   BuildingGroupScreen ("BuildingGroups")
   //     /TitleBar                               24h
   //       /BGImage / CategoryLabel
   //     /Searchbar                              36h
   //       /BG / FilterInputField                bluegrey row + 24h input
   //     /Viewport/Scrollbar/Handle              build_menu_scrollbar_*
   //     /Viewport/Contents/SubCategory/Header   16h
   //       /BarLeft (8x2) / Arrow (12x8) / Label / BarRight (flex×2)
   //   PlanScreen.allBuildingToggles[*]/BG       row visuals (web_button)
   //
   // BuildingGroupScreen lives on PlanScreen.buildingGroupsRoot which
   // PlanScreen.OnPrefabInit deactivates; its Instance is therefore null
   // until the user opens a build category. Warmup() force-opens the
   // first category to fix that.
   internal static class OniUiTokens
   {
      #region Caches

      private static Color? _headerBg;
      private static float? _headerHeight;
      private static TMP_FontAsset _headerFont;
      private static float? _headerFontSize;
      private static Color? _headerText;

      private static Color? _subheaderBg;
      private static float? _subheaderHeight;

      private static Color? _inputBg;
      private static float? _inputHeight;
      private static TMP_FontAsset _inputFont;
      private static float? _inputFontSize;
      private static Color? _inputText;
      private static Color? _inputPlaceholder;
      private static Color? _clearButtonBgColor;
      private static Sprite _clearButtonBgSprite;
      private static Color? _clearButtonFgColor;
      private static Sprite _clearButtonFgSprite;
      private static Vector2? _clearButtonFgInset;
      private static Color? _panelBgColor;
      private static Sprite _panelBgSprite;

      private static TMP_FontAsset _sectionFont;
      private static float? _sectionFontSize;
      private static Color? _sectionText;
      private static Color? _sectionRule;
      private static float? _sectionHeight;
      private static float? _sectionRuleHeight;
      private static float? _sectionBarLeftWidth;
      private static Vector2? _sectionArrowSize;
      private static Sprite _sectionBarSprite;
      private static Sprite _sectionArrowSprite;

      private static Color? _rowBgNormal;
      private static Color? _rowBgHover;
      private static Color? _rowBgDisabled;
      private static Color? _rowBgDisabledHover;
      private static Sprite _rowBgSprite;
      private static Sprite _rowBgDisabledSprite;
      private static TMP_FontAsset _rowFont;
      private static float? _rowFontSize;
      private static Color? _rowText;
      private static float? _rowIconSize;
      private static Material _rowIconMaterial;
      private static Material _rowIconDisabledMaterial;
      private static Sprite _rowNeedsTechSprite;
      private static Color? _rowNeedsTechColor;
      private static Vector2? _rowNeedsTechSize;

      private static float? _scrollbarWidth;
      private static Color? _scrollbarTrackColor;
      private static Sprite _scrollbarTrackSprite;
      private static Color? _scrollbarHandleColor;
      private static Sprite _scrollbarHandleSprite;
      private static Vector2? _scrollbarHandleInset;

      private static float? _scrollElasticity;
      private static float? _scrollSensitivity;
      private static float? _scrollDecelerationRate;
      private static bool? _scrollInertia;

      #endregion

      #region Public surface

      public static Color HeaderBg =>
         CacheOpt(ref _headerBg, Lift.HeaderBg, ScopeUiDefaults.HeaderBg);
      public static float HeaderHeight =>
         CacheOpt(ref _headerHeight, Lift.HeaderHeight, ScopeUiDefaults.HeaderHeight);
      public static TMP_FontAsset HeaderFont =>
         CacheRef(ref _headerFont, Lift.HeaderFont, ScopeUiDefaults.HeaderFont);
      public static float HeaderFontSize =>
         CacheOpt(ref _headerFontSize, Lift.HeaderFontSize, ScopeUiDefaults.HeaderFontSize);
      public static Color HeaderText =>
         CacheOpt(ref _headerText, Lift.HeaderText, ScopeUiDefaults.HeaderText);

      public static Color SubheaderBg =>
         CacheOpt(ref _subheaderBg, Lift.SubheaderBg, ScopeUiDefaults.SubheaderBg);
      public static float SubheaderHeight =>
         CacheOpt(ref _subheaderHeight, Lift.SubheaderHeight, ScopeUiDefaults.SubheaderHeight);

      public static Color InputBg => CacheOpt(ref _inputBg, Lift.InputBg, ScopeUiDefaults.InputBg);
      public static float InputHeight =>
         CacheOpt(ref _inputHeight, Lift.InputHeight, ScopeUiDefaults.InputHeight);
      public static TMP_FontAsset InputFont =>
         CacheRef(ref _inputFont, Lift.InputFont, ScopeUiDefaults.InputFont);
      public static float InputFontSize =>
         CacheOpt(ref _inputFontSize, Lift.InputFontSize, ScopeUiDefaults.InputFontSize);
      public static Color InputText =>
         CacheOpt(ref _inputText, Lift.InputText, ScopeUiDefaults.InputText);
      public static Color InputPlaceholder =>
         CacheOpt(ref _inputPlaceholder, Lift.InputPlaceholder, ScopeUiDefaults.InputPlaceholder);
      public static Color ClearButtonBgColor =>
         CacheOpt(
            ref _clearButtonBgColor,
            Lift.ClearButtonBgColor,
            ScopeUiDefaults.ClearButtonBgColor
         );
      public static Sprite ClearButtonBgSprite =>
         CacheRef(
            ref _clearButtonBgSprite,
            Lift.ClearButtonBgSprite,
            ScopeUiDefaults.ClearButtonBgSprite
         );
      public static Color ClearButtonFgColor =>
         CacheOpt(
            ref _clearButtonFgColor,
            Lift.ClearButtonFgColor,
            ScopeUiDefaults.ClearButtonFgColor
         );
      public static Sprite ClearButtonFgSprite =>
         CacheRef(
            ref _clearButtonFgSprite,
            Lift.ClearButtonFgSprite,
            ScopeUiDefaults.ClearButtonFgSprite
         );
      public static Vector2 ClearButtonFgInset =>
         CacheOpt(
            ref _clearButtonFgInset,
            Lift.ClearButtonFgInset,
            ScopeUiDefaults.ClearButtonFgInset
         );
      public static Color PanelBgColor =>
         CacheOpt(ref _panelBgColor, Lift.PanelBgColor, ScopeUiDefaults.PanelBgColor);
      public static Sprite PanelBgSprite =>
         CacheRef(ref _panelBgSprite, Lift.PanelBgSprite, ScopeUiDefaults.PanelBgSprite);

      public static Color BodyBg => ScopeUiDefaults.BodyBg;

      public static TMP_FontAsset SectionFont =>
         CacheRef(ref _sectionFont, Lift.SectionFont, ScopeUiDefaults.SectionFont);
      public static float SectionFontSize =>
         CacheOpt(ref _sectionFontSize, Lift.SectionFontSize, ScopeUiDefaults.SectionFontSize);
      public static Color SectionText =>
         CacheOpt(ref _sectionText, Lift.SectionText, ScopeUiDefaults.SectionText);
      public static Color SectionRule =>
         CacheOpt(ref _sectionRule, Lift.SectionRule, ScopeUiDefaults.SectionRule);
      public static float SectionHeight =>
         CacheOpt(ref _sectionHeight, Lift.SectionHeight, ScopeUiDefaults.SectionHeight);
      public static float SectionRuleHeight =>
         CacheOpt(
            ref _sectionRuleHeight,
            Lift.SectionRuleHeight,
            ScopeUiDefaults.SectionRuleHeight
         );
      public static float SectionBarLeftWidth =>
         CacheOpt(
            ref _sectionBarLeftWidth,
            Lift.SectionBarLeftWidth,
            ScopeUiDefaults.SectionBarLeftWidth
         );
      public static Vector2 SectionArrowSize =>
         CacheOpt(ref _sectionArrowSize, Lift.SectionArrowSize, ScopeUiDefaults.SectionArrowSize);
      public static Sprite SectionBarSprite =>
         CacheRef(ref _sectionBarSprite, Lift.SectionBarSprite, ScopeUiDefaults.SectionBarSprite);
      public static Sprite SectionArrowSprite =>
         CacheRef(
            ref _sectionArrowSprite,
            Lift.SectionArrowSprite,
            ScopeUiDefaults.SectionArrowSprite
         );

      public static float RowHeight => ScopeUiDefaults.RowHeight;
      public static Color RowBgNormal =>
         CacheOpt(ref _rowBgNormal, Lift.RowBgNormal, ScopeUiDefaults.RowBgNormal);

      // Lerp default keeps hover in step with a stolen RowBgNormal.
      public static Color RowBgHover =>
         CacheOpt(ref _rowBgHover, Lift.RowBgHover, Color.Lerp(RowBgNormal, Color.white, 0.18f));
      public static Color RowBgDisabled =>
         CacheOpt(ref _rowBgDisabled, Lift.RowBgDisabled, ScopeUiDefaults.RowBgDisabled);
      public static Color RowBgDisabledHover =>
         CacheOpt(
            ref _rowBgDisabledHover,
            Lift.RowBgDisabledHover,
            ScopeUiDefaults.RowBgDisabledHover
         );
      public static Sprite RowBgSprite =>
         CacheRef(ref _rowBgSprite, Lift.RowBgSprite, ScopeUiDefaults.RowBgSprite);
      public static Sprite RowBgDisabledSprite =>
         CacheRef(ref _rowBgDisabledSprite, Lift.RowBgDisabledSprite, RowBgSprite);
      public static TMP_FontAsset RowFont =>
         CacheRef(ref _rowFont, Lift.RowFont, ScopeUiDefaults.RowFont);
      public static float RowFontSize =>
         CacheOpt(ref _rowFontSize, Lift.RowFontSize, ScopeUiDefaults.RowFontSize);
      public static Color RowText => CacheOpt(ref _rowText, Lift.RowText, ScopeUiDefaults.RowText);
      public static float RowIconSize =>
         CacheOpt(ref _rowIconSize, Lift.RowIconSize, ScopeUiDefaults.RowIconSize);
      public static Material RowIconMaterial =>
         CacheRef(ref _rowIconMaterial, Lift.RowIconMaterial, ScopeUiDefaults.RowIconMaterial);
      public static Material RowIconDisabledMaterial =>
         CacheRef(ref _rowIconDisabledMaterial, Lift.RowIconDisabledMaterial, RowIconMaterial);
      public static Sprite RowNeedsTechSprite =>
         CacheRef(
            ref _rowNeedsTechSprite,
            Lift.RowNeedsTechSprite,
            ScopeUiDefaults.RowNeedsTechSprite
         );
      public static Color RowNeedsTechColor =>
         CacheOpt(
            ref _rowNeedsTechColor,
            Lift.RowNeedsTechColor,
            ScopeUiDefaults.RowNeedsTechColor
         );
      public static Vector2 RowNeedsTechSize =>
         CacheOpt(ref _rowNeedsTechSize, Lift.RowNeedsTechSize, ScopeUiDefaults.RowNeedsTechSize);

      public static float ScrollbarWidth =>
         CacheOpt(ref _scrollbarWidth, Lift.ScrollbarWidth, ScopeUiDefaults.ScrollbarWidth);
      public static Color ScrollbarTrackColor =>
         CacheOpt(
            ref _scrollbarTrackColor,
            Lift.ScrollbarTrackColor,
            ScopeUiDefaults.ScrollbarTrackColor
         );
      public static Sprite ScrollbarTrackSprite =>
         CacheRef(
            ref _scrollbarTrackSprite,
            Lift.ScrollbarTrackSprite,
            ScopeUiDefaults.ScrollbarTrackSprite
         );
      public static Color ScrollbarHandleColor =>
         CacheOpt(
            ref _scrollbarHandleColor,
            Lift.ScrollbarHandleColor,
            ScopeUiDefaults.ScrollbarHandleColor
         );
      public static Sprite ScrollbarHandleSprite =>
         CacheRef(
            ref _scrollbarHandleSprite,
            Lift.ScrollbarHandleSprite,
            ScopeUiDefaults.ScrollbarHandleSprite
         );
      public static Vector2 ScrollbarHandleInset =>
         CacheOpt(
            ref _scrollbarHandleInset,
            Lift.ScrollbarHandleInset,
            ScopeUiDefaults.ScrollbarHandleInset
         );

      public static float ScrollElasticity =>
         CacheOpt(ref _scrollElasticity, Lift.ScrollElasticity, ScopeUiDefaults.ScrollElasticity);
      public static float ScrollSensitivity =>
         CacheOpt(
            ref _scrollSensitivity,
            Lift.ScrollSensitivity,
            ScopeUiDefaults.ScrollSensitivity
         );
      public static float ScrollDecelerationRate =>
         CacheOpt(
            ref _scrollDecelerationRate,
            Lift.ScrollDecelerationRate,
            ScopeUiDefaults.ScrollDecelerationRate
         );
      public static bool ScrollInertia =>
         CacheOpt(ref _scrollInertia, Lift.ScrollInertia, ScopeUiDefaults.ScrollInertia);

      #endregion

      #region Cache helpers

      // Once extract returns non-null we cache forever; null returns
      // mean "not ready, retry next access". Exceptions are logged once
      // per extractor name and treated as a miss.
      private static T CacheOpt<T>(ref T? cache, Func<T?> extract, T def)
         where T : struct
      {
         if (cache.HasValue)
            return cache.Value;
         T? v;
         try
         {
            v = extract();
         }
         catch (Exception ex)
         {
            ReportExtractFailure(extract.Method.Name, ex);
            return def;
         }
         if (v.HasValue)
         {
            cache = v;
            return v.Value;
         }
         return def;
      }

      private static T CacheRef<T>(ref T cache, Func<T> extract, T def)
         where T : UnityEngine.Object
      {
         if (cache != null)
            return cache;
         T v;
         try
         {
            v = extract();
         }
         catch (Exception ex)
         {
            ReportExtractFailure(extract.Method.Name, ex);
            return def;
         }
         if (v != null)
         {
            cache = v;
            return v;
         }
         return def;
      }

      private static readonly System.Collections.Generic.HashSet<string> _reportedFailures =
         new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

      private static void ReportExtractFailure(string extractorName, Exception ex)
      {
         if (_reportedFailures.Add(extractorName))
            Debug.LogWarning(
               $"[Scope] Extractor {extractorName} threw (suppressing further reports): {ex}"
            );
      }

      #endregion

      #region Extractors
      // Each Lift.* token returns null on any miss; CacheOpt/CacheRef retry
      // next access. .Live() converts Unity fake-null to real null so `?.`
      // chains short-circuit on destroyed-but-not-yet-GC'd refs.
      private static class Lift
      {
         // Singletons.
         private static BuildingGroupScreen Bgs() => BuildingGroupScreen.Instance.Live();

         private static PlanScreen Plan() => PlanScreen.Instance.Live();

         // Anchors — shared subtrees that several tokens read off.
         private static Image BgPanel() => Bgs()?.transform.Find("BGPanel")?.GetComponent<Image>();

         private static Transform TitleBar() => Bgs()?.transform.Find("TitleBar");

         private static TextMeshProUGUI HeaderLabel() =>
            TitleBar()?.Find("CategoryLabel")?.GetComponent<TextMeshProUGUI>();

         private static Transform Searchbar() => Bgs()?.transform.Find("Searchbar");

         private static Transform FilterInputField() => Searchbar()?.Find("FilterInputField");

         // Renamed from "InputText" to avoid colliding with the InputText (Color) token below.
         private static TextMeshProUGUI InputLabel() =>
            FilterInputField()?.Find("Text Area/Text")?.GetComponent<TextMeshProUGUI>();

         private static Transform ClearSearchButton() => Searchbar()?.Find("ClearSearchButton");

         private static Image ClearBg() => ClearSearchButton()?.Find("BG")?.GetComponent<Image>();

         private static Image ClearFg() => ClearSearchButton()?.Find("FG")?.GetComponent<Image>();

         private static ScrollRect MainScroll() =>
            Plan()?.BuildingGroupContentsRect.Live()?.GetComponent<ScrollRect>();

         private static Transform ScrollbarTrack() => Bgs()?.transform.Find("Viewport/Scrollbar");

         private static Transform FirstSubcategoryHeader()
         {
            var contents = Bgs()?.transform.Find("Viewport/Contents");
            if (contents == null)
               return null;
            for (int i = 0; i < contents.childCount; i++)
            {
               var child = contents.GetChild(i);
               if (child == null || child.name != "SubCategory")
                  continue;
               var h = child.Find("Header");
               if (h != null)
                  return h;
            }
            return null;
         }

         private static Image SectionBarLeft() =>
            FirstSubcategoryHeader()?.Find("BarLeft")?.GetComponent<Image>();

         private static TextMeshProUGUI SectionLabel() =>
            FirstSubcategoryHeader()?.Find("Label")?.GetComponent<TextMeshProUGUI>();

         // MultiToggle.states[0] is canonical "enabled, unselected"; sampling the live
         // Image.color instead picks up whichever state happens to be active.
         private static PlanBuildingToggle SamplePlanToggle()
         {
            var toggles = Plan()?.allBuildingToggles;
            if (toggles == null || toggles.Count == 0)
               return null;
            return toggles.Values.FirstOrDefault(x => x != null);
         }

         private static MultiToggle SampleMultiToggle() => SamplePlanToggle()?.toggle;

         private static Image SampleToggleBgImage() =>
            SamplePlanToggle()?.transform.Find("BG")?.GetComponent<Image>();

         private static TextMeshProUGUI SampleListLabel()
         {
            var pbt = SamplePlanToggle();
            return pbt?.text_listView
               ?? pbt?.transform.Find("BG/NameLabel_ListView")?.GetComponent<TextMeshProUGUI>();
         }

         private static Image NeedTechBadge() =>
            SamplePlanToggle()?.transform.Find("FG/ImageContainer/Image")?.GetComponent<Image>();

         // sizeDelta is unreliable on stretched-anchor RTs (zero ≠ height);
         // prefer LayoutElement intent → post-layout rect → sizeDelta. Returns
         // null when no source is positive, so CacheOpt uses the default and
         // retries next access (gives layout time to settle).
         private static float? Height(RectTransform rt)
         {
            if (rt == null)
               return null;
            var le = rt.GetComponent<LayoutElement>();
            if (le != null && le.preferredHeight > 0f)
               return le.preferredHeight;
            if (rt.rect.height > 0f)
               return rt.rect.height;
            if (rt.sizeDelta.y > 0f)
               return rt.sizeDelta.y;
            return null;
         }

         private static float? Width(RectTransform rt)
         {
            if (rt == null)
               return null;
            var le = rt.GetComponent<LayoutElement>();
            if (le != null && le.preferredWidth > 0f)
               return le.preferredWidth;
            if (rt.rect.width > 0f)
               return rt.rect.width;
            if (rt.sizeDelta.x > 0f)
               return rt.sizeDelta.x;
            return null;
         }

         private static Vector2? Size(RectTransform rt)
         {
            var w = Width(rt);
            var h = Height(rt);
            return (w.HasValue && h.HasValue) ? new Vector2(w.Value, h.Value) : (Vector2?)null;
         }

         // sizeDelta-as-inset semantic; reject (0,0) as uninformative.
         private static Vector2? Inset(RectTransform rt)
         {
            if (rt == null)
               return null;
            var v = rt.sizeDelta;
            return (v.x != 0f || v.y != 0f) ? v : (Vector2?)null;
         }

         // Reject zero/negative font sizes — never sane.
         private static float? PositiveFont(TMP_Text t) =>
            t != null && t.fontSize > 0f ? t.fontSize : (float?)null;

         // Reject default(Color) = (0,0,0,0). None of ScopeUiDefaults' colours are
         // default(Color), so extracting it is always a "value not yet initialized" miss.
         private static Color? Visible(Color? c) =>
            c.HasValue && c.Value != default ? c : (Color?)null;

         // Tokens.
         public static Color? PanelBgColor() => Visible(BgPanel()?.color);

         public static Sprite PanelBgSprite() => BgPanel()?.sprite;

         public static Color? HeaderBg() =>
            Visible(TitleBar()?.Find("BGImage")?.GetComponent<Image>()?.color);

         public static float? HeaderHeight() => Height(TitleBar() as RectTransform);

         public static TMP_FontAsset HeaderFont() => HeaderLabel()?.font;

         public static float? HeaderFontSize() => PositiveFont(HeaderLabel());

         public static Color? HeaderText() => Visible(HeaderLabel()?.color);

         public static float? SubheaderHeight() => Height(Searchbar() as RectTransform);

         public static Color? SubheaderBg() =>
            Visible(Searchbar()?.Find("BG")?.GetComponent<Image>()?.color);

         public static float? InputHeight() => Height(FilterInputField() as RectTransform);

         public static Color? InputBg() =>
            Visible(FilterInputField()?.GetComponent<Image>()?.color);

         public static Color? InputPlaceholder() =>
            Visible(
               FilterInputField()
                  ?.Find("Text Area/Placeholder")
                  ?.GetComponent<TextMeshProUGUI>()
                  ?.color
            );

         public static TMP_FontAsset InputFont() => InputLabel()?.font;

         public static float? InputFontSize() => PositiveFont(InputLabel());

         public static Color? InputText() => Visible(InputLabel()?.color);

         public static Color? ClearButtonBgColor() => Visible(ClearBg()?.color);

         public static Sprite ClearButtonBgSprite() => ClearBg()?.sprite;

         public static Color? ClearButtonFgColor() => Visible(ClearFg()?.color);

         public static Sprite ClearButtonFgSprite() => ClearFg()?.sprite;

         public static Vector2? ClearButtonFgInset() =>
            Inset(ClearFg()?.transform as RectTransform);

         public static float? ScrollElasticity() => MainScroll()?.elasticity;

         public static float? ScrollSensitivity() => MainScroll()?.scrollSensitivity;

         public static float? ScrollDecelerationRate() => MainScroll()?.decelerationRate;

         public static bool? ScrollInertia() => MainScroll()?.inertia;

         public static float? ScrollbarWidth() => Width(ScrollbarTrack() as RectTransform);

         public static Color? ScrollbarTrackColor() =>
            Visible(ScrollbarTrack()?.GetComponent<Image>()?.color);

         public static Sprite ScrollbarTrackSprite() =>
            ScrollbarTrack()?.GetComponent<Image>()?.sprite;

         public static Color? ScrollbarHandleColor() =>
            Visible(ScrollbarTrack()?.Find("Handle")?.GetComponent<Image>()?.color);

         public static Sprite ScrollbarHandleSprite() =>
            ScrollbarTrack()?.Find("Handle")?.GetComponent<Image>()?.sprite;

         public static Vector2? ScrollbarHandleInset() =>
            Inset(ScrollbarTrack()?.Find("Handle") as RectTransform);

         public static float? SectionHeight() => Height(FirstSubcategoryHeader() as RectTransform);

         public static Sprite SectionArrowSprite() =>
            FirstSubcategoryHeader()?.Find("Arrow")?.GetComponent<Image>()?.sprite;

         public static Vector2? SectionArrowSize() =>
            Size(FirstSubcategoryHeader()?.Find("Arrow") as RectTransform);

         public static Color? SectionRule() => Visible(SectionBarLeft()?.color);

         public static Sprite SectionBarSprite() => SectionBarLeft()?.sprite;

         public static float? SectionRuleHeight() =>
            Height(SectionBarLeft()?.transform as RectTransform);

         public static float? SectionBarLeftWidth() =>
            Width(SectionBarLeft()?.transform as RectTransform);

         public static TMP_FontAsset SectionFont() => SectionLabel()?.font;

         public static float? SectionFontSize() => PositiveFont(SectionLabel());

         public static Color? SectionText() => Visible(SectionLabel()?.color);

         public static Material RowIconMaterial() => Plan()?.defaultUIMaterial;

         public static Material RowIconDisabledMaterial() => Plan()?.desaturatedUIMaterial;

         public static Sprite RowNeedsTechSprite() => Plan()?.Overlay_NeedTech;

         public static Color? RowBgNormal()
         {
            var mt = SampleMultiToggle();
            if (mt?.states != null && mt.states.Length > 0)
               return Visible(mt.states[0].color);
            return Visible(SampleToggleBgImage()?.color);
         }

         public static Sprite RowBgSprite()
         {
            var mt = SampleMultiToggle();
            var s0 = mt?.states != null && mt.states.Length > 0 ? mt.states[0].sprite : null;
            return s0 != null ? s0 : SampleToggleBgImage()?.sprite;
         }

         public static Color? RowBgHover()
         {
            var mt = SampleMultiToggle();
            if (mt?.states == null || mt.states.Length == 0)
               return null;
            var s0 = mt.states[0];
            return s0.use_color_on_hover ? Visible(s0.color_on_hover) : (Color?)null;
         }

         public static Color? RowBgDisabled()
         {
            var mt = SampleMultiToggle();
            return mt?.states != null && mt.states.Length > 2
               ? Visible(mt.states[2].color)
               : (Color?)null;
         }

         public static Sprite RowBgDisabledSprite()
         {
            var mt = SampleMultiToggle();
            return mt?.states != null && mt.states.Length > 2 ? mt.states[2].sprite : null;
         }

         public static Color? RowBgDisabledHover()
         {
            var mt = SampleMultiToggle();
            return mt?.states != null && mt.states.Length > 3
               ? Visible(mt.states[3].color)
               : (Color?)null;
         }

         public static TMP_FontAsset RowFont() => SampleListLabel()?.font;

         public static float? RowFontSize() => PositiveFont(SampleListLabel());

         public static Color? RowText() => Visible(SampleListLabel()?.color);

         public static float? RowIconSize()
         {
            var s = Size(SamplePlanToggle()?.transform.Find("BG/Image_ListView") as RectTransform);
            return s.HasValue ? Mathf.Max(s.Value.x, s.Value.y) : (float?)null;
         }

         public static Color? RowNeedsTechColor() => Visible(NeedTechBadge()?.color);

         public static Vector2? RowNeedsTechSize() =>
            Size(NeedTechBadge()?.transform as RectTransform);
      }
      #endregion

      #region Warmup

      // BuildingGroupScreen.Instance is null until the user opens a build
      // category (PlanScreen.OnPrefabInit deactivates buildingGroupsRoot).
      // Click the first category to trigger Klei's onSelect → OnClickCategory
      // → OpenCategoryPanel chain; the menu opens visibly and stays open.
      public static void Warmup()
      {
         try
         {
            if (BuildingGroupScreen.Instance != null)
               return;

            var ps = PlanScreen.Instance;
            if (ps == null)
            {
               Debug.LogWarning("[Scope] Warmup: PlanScreen.Instance null; skipping.");
               return;
            }

            var togglesField = AccessTools.Field(typeof(KIconToggleMenu), "toggles");
            var togglesObj = togglesField?.GetValue(ps);
            if (!(togglesObj is IList toggles) || toggles.Count == 0)
            {
               Debug.LogWarning("[Scope] Warmup: PlanScreen.toggles empty/inaccessible.");
               return;
            }

            if (!(toggles[0] is KToggle first))
            {
               Debug.LogWarning("[Scope] Warmup: first toggle is null/non-KToggle.");
               return;
            }

            Mod.Log("[Scope] Warmup: clicking first build-category toggle.");
            first.Click();

            if (BuildingGroupScreen.Instance == null)
               Debug.LogWarning(
                  "[Scope] Warmup: Click() returned but Instance still null (focus/EventSystem gate?)."
               );
         }
         catch (Exception ex)
         {
            Debug.LogWarning($"[Scope] Warmup outer failure: {ex}");
         }
      }

      #endregion

      #region Debug

      private static bool _dumped;

      // Per-overlay-open diagnostic. First call: full hierarchy dump + token
      // summary. Subsequent calls: token summary only. No-op when LogDebug
      // is off. Cheap enough for the steady-state path (cached fields are
      // simple field reads).
      public static void LogPerOpen()
      {
         if (!Mod.LogDebug)
            return;
         if (!_dumped)
         {
            _dumped = true;
            DumpHierarchies();
         }
         LogResolvedTokens();
      }

      // Forces evaluation of every token; logs resolved values + extracted-vs-default per token.
      public static void LogResolvedTokens()
      {
         try
         {
            LogResolvedTokensInner();
         }
         catch (Exception ex)
         {
            Debug.LogWarning($"[Scope] Token log failed: {ex.Message}");
         }
      }

      private static void LogResolvedTokensInner()
      {
         var sb = new StringBuilder();
         sb.AppendLine("[Scope-Tokens] BEGIN");

         LogColor(sb, "HeaderBg", HeaderBg, _headerBg.HasValue);
         LogFloat(sb, "HeaderHeight", HeaderHeight, _headerHeight.HasValue);
         LogFont(sb, "HeaderFont", HeaderFont, _headerFont != null);
         LogFloat(sb, "HeaderFontSize", HeaderFontSize, _headerFontSize.HasValue);
         LogColor(sb, "HeaderText", HeaderText, _headerText.HasValue);

         LogColor(sb, "SubheaderBg", SubheaderBg, _subheaderBg.HasValue);
         LogFloat(sb, "SubheaderHeight", SubheaderHeight, _subheaderHeight.HasValue);

         LogColor(sb, "InputBg", InputBg, _inputBg.HasValue);
         LogFloat(sb, "InputHeight", InputHeight, _inputHeight.HasValue);
         LogFont(sb, "InputFont", InputFont, _inputFont != null);
         LogFloat(sb, "InputFontSize", InputFontSize, _inputFontSize.HasValue);
         LogColor(sb, "InputText", InputText, _inputText.HasValue);
         LogColor(sb, "InputPlaceholder", InputPlaceholder, _inputPlaceholder.HasValue);
         LogColor(sb, "ClearButtonBgColor", ClearButtonBgColor, _clearButtonBgColor.HasValue);
         LogSprite(sb, "ClearButtonBgSprite", ClearButtonBgSprite, _clearButtonBgSprite != null);
         LogColor(sb, "ClearButtonFgColor", ClearButtonFgColor, _clearButtonFgColor.HasValue);
         LogSprite(sb, "ClearButtonFgSprite", ClearButtonFgSprite, _clearButtonFgSprite != null);
         LogVec2(sb, "ClearButtonFgInset", ClearButtonFgInset, _clearButtonFgInset.HasValue);
         LogColor(sb, "PanelBgColor", PanelBgColor, _panelBgColor.HasValue);
         LogSprite(sb, "PanelBgSprite", PanelBgSprite, _panelBgSprite != null);

         LogColor(sb, "BodyBg", BodyBg, false);

         LogFont(sb, "SectionFont", SectionFont, _sectionFont != null);
         LogFloat(sb, "SectionFontSize", SectionFontSize, _sectionFontSize.HasValue);
         LogColor(sb, "SectionText", SectionText, _sectionText.HasValue);
         LogColor(sb, "SectionRule", SectionRule, _sectionRule.HasValue);
         LogFloat(sb, "SectionHeight", SectionHeight, _sectionHeight.HasValue);
         LogFloat(sb, "SectionRuleHeight", SectionRuleHeight, _sectionRuleHeight.HasValue);
         LogFloat(sb, "SectionBarLeftWidth", SectionBarLeftWidth, _sectionBarLeftWidth.HasValue);
         LogVec2(sb, "SectionArrowSize", SectionArrowSize, _sectionArrowSize.HasValue);
         LogSprite(sb, "SectionBarSprite", SectionBarSprite, _sectionBarSprite != null);
         LogSprite(sb, "SectionArrowSprite", SectionArrowSprite, _sectionArrowSprite != null);

         LogFloat(sb, "RowHeight", RowHeight, false);
         LogColor(sb, "RowBgNormal", RowBgNormal, _rowBgNormal.HasValue);
         LogColor(sb, "RowBgHover", RowBgHover, _rowBgHover.HasValue);
         LogColor(sb, "RowBgDisabled", RowBgDisabled, _rowBgDisabled.HasValue);
         LogColor(sb, "RowBgDisabledHover", RowBgDisabledHover, _rowBgDisabledHover.HasValue);
         LogSprite(sb, "RowBgSprite", RowBgSprite, _rowBgSprite != null);
         LogSprite(sb, "RowBgDisabledSprite", RowBgDisabledSprite, _rowBgDisabledSprite != null);
         LogFont(sb, "RowFont", RowFont, _rowFont != null);
         LogFloat(sb, "RowFontSize", RowFontSize, _rowFontSize.HasValue);
         LogColor(sb, "RowText", RowText, _rowText.HasValue);
         LogFloat(sb, "RowIconSize", RowIconSize, _rowIconSize.HasValue);
         LogMat(sb, "RowIconMaterial", RowIconMaterial, _rowIconMaterial != null);
         LogMat(
            sb,
            "RowIconDisabledMaterial",
            RowIconDisabledMaterial,
            _rowIconDisabledMaterial != null
         );
         LogSprite(sb, "RowNeedsTechSprite", RowNeedsTechSprite, _rowNeedsTechSprite != null);
         LogColor(sb, "RowNeedsTechColor", RowNeedsTechColor, _rowNeedsTechColor.HasValue);
         LogVec2(sb, "RowNeedsTechSize", RowNeedsTechSize, _rowNeedsTechSize.HasValue);

         LogFloat(sb, "ScrollbarWidth", ScrollbarWidth, _scrollbarWidth.HasValue);
         LogColor(sb, "ScrollbarTrackColor", ScrollbarTrackColor, _scrollbarTrackColor.HasValue);
         LogSprite(sb, "ScrollbarTrackSprite", ScrollbarTrackSprite, _scrollbarTrackSprite != null);
         LogColor(sb, "ScrollbarHandleColor", ScrollbarHandleColor, _scrollbarHandleColor.HasValue);
         LogSprite(
            sb,
            "ScrollbarHandleSprite",
            ScrollbarHandleSprite,
            _scrollbarHandleSprite != null
         );
         LogVec2(sb, "ScrollbarHandleInset", ScrollbarHandleInset, _scrollbarHandleInset.HasValue);

         LogFloat(sb, "ScrollElasticity", ScrollElasticity, _scrollElasticity.HasValue);
         LogFloat(sb, "ScrollSensitivity", ScrollSensitivity, _scrollSensitivity.HasValue);
         LogFloat(
            sb,
            "ScrollDecelerationRate",
            ScrollDecelerationRate,
            _scrollDecelerationRate.HasValue
         );
         LogBool(sb, "ScrollInertia", ScrollInertia, _scrollInertia.HasValue);

         sb.AppendLine("[Scope-Tokens] END");
         Debug.Log(sb.ToString());
      }

      private static void LogColor(StringBuilder sb, string name, Color c, bool extracted)
      {
         var c32 = (Color32)c;
         sb.AppendLine(
            $"[Scope-Tokens] {name, -22} = new Color32({c32.r, 3}, {c32.g, 3}, {c32.b, 3}, {c32.a, 3})"
               + $"  // #{ColorUtility.ToHtmlStringRGBA(c)} ({(extracted ? "extracted" : "default")})"
         );
      }

      private static void LogFloat(StringBuilder sb, string name, float v, bool extracted) =>
         sb.AppendLine(
            $"[Scope-Tokens] {name, -22} = {v}f;  // ({(extracted ? "extracted" : "default")})"
         );

      private static void LogBool(StringBuilder sb, string name, bool v, bool extracted) =>
         sb.AppendLine(
            $"[Scope-Tokens] {name, -22} = {(v ? "true" : "false")};  // ({(extracted ? "extracted" : "default")})"
         );

      private static void LogVec2(StringBuilder sb, string name, Vector2 v, bool extracted) =>
         sb.AppendLine(
            $"[Scope-Tokens] {name, -22} = new Vector2({v.x}f, {v.y}f);  // ({(extracted ? "extracted" : "default")})"
         );

      private static void LogFont(StringBuilder sb, string name, TMP_FontAsset f, bool extracted) =>
         sb.AppendLine(
            $"[Scope-Tokens] {name, -22} = {(f != null ? f.name : "<null>")}  // ({(extracted ? "extracted" : "default (PLib)")})"
         );

      private static void LogMat(StringBuilder sb, string name, Material m, bool extracted) =>
         sb.AppendLine(
            $"[Scope-Tokens] {name, -22} = {(m != null ? m.name : "<null>")}  // ({(extracted ? "extracted" : "default (null)")})"
         );

      private static void LogSprite(StringBuilder sb, string name, Sprite s, bool extracted) =>
         sb.AppendLine(
            $"[Scope-Tokens] {name, -22} = {(s != null ? s.name : "<null>")}  // ({(extracted ? "extracted" : "default (null)")})"
         );

      // Hierarchy dump for tracking down a renamed Klei path.
      public static void DumpHierarchies()
      {
         try
         {
            var sb = new StringBuilder();
            sb.AppendLine("[Scope-UI-Dump] BEGIN");
            var ps = PlanScreen.Instance;
            if (ps != null)
            {
               sb.AppendLine("[Scope-UI-Dump] --- PlanScreen ---");
               DumpRec(ps.transform, 0, sb);
            }
            else
               sb.AppendLine("[Scope-UI-Dump] PlanScreen.Instance == null");
            var bgs = BuildingGroupScreen.Instance;
            if (bgs != null)
            {
               sb.AppendLine("[Scope-UI-Dump] --- BuildingGroupScreen ---");
               DumpRec(bgs.transform, 0, sb);
            }
            else
               sb.AppendLine("[Scope-UI-Dump] BuildingGroupScreen.Instance == null");
            sb.AppendLine("[Scope-UI-Dump] END");
            Debug.Log(sb.ToString());
         }
         catch (Exception ex)
         {
            Debug.LogWarning($"[Scope] Hierarchy dump failed: {ex.Message}");
         }
      }

      private static void DumpRec(Transform t, int depth, StringBuilder sb)
      {
         if (t == null)
            return;
         sb.Append("[Scope-UI-Dump] ").Append(new string(' ', depth * 2)).Append(t.name);
         if (t is RectTransform rt)
         {
            sb.Append($" sizeDelta={rt.sizeDelta} rect={rt.rect.size}");
            var le = rt.GetComponent<LayoutElement>();
            if (le != null)
               sb.Append(
                  $" LE[pref=({le.preferredWidth},{le.preferredHeight}) min=({le.minWidth},{le.minHeight})]"
               );
         }
         var img = t.GetComponent<Image>();
         if (img != null)
            sb.Append(
               $" Image[color=#{ColorUtility.ToHtmlStringRGBA(img.color)} sprite={(img.sprite != null ? img.sprite.name : "<null>")}]"
            );
         var tmp = t.GetComponent<TextMeshProUGUI>();
         if (tmp != null)
            sb.Append(
               $" TMP[font={(tmp.font != null ? tmp.font.name : "<null>")} size={tmp.fontSize} color=#{ColorUtility.ToHtmlStringRGBA(tmp.color)}]"
            );
         sb.AppendLine();
         if (depth >= 8)
            return;
         for (int i = 0; i < t.childCount; i++)
            DumpRec(t.GetChild(i), depth + 1, sb);
      }

      #endregion
   }

   internal static class UnityExt
   {
      // Unity overloads `==` on UE.Object to detect destroyed-but-not-GC'd refs;
      // `?.` doesn't, so `obj?.x` reads `x` off a "dead" reference. `.Live()` converts
      // the fake-null to real null so subsequent `?.` short-circuits correctly.
      internal static T Live<T>(this T x)
         where T : UnityEngine.Object => x == null ? null : x;
   }
}
