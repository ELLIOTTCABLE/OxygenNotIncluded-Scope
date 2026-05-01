using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeMod.UI {
   // The only place that walks Klei's hierarchy. On first overlay-open,
   // lifts colours / fonts / sprites / sizes off the live UI. Every failure
   // is bounded to a try/catch here so the mod falls back to ScopeUiDefaults
   // — visual drift is acceptable, NRE is not.
   //
   // Path map as of 2026-04-30:
   //   BuildingGroupScreen  ("BuildingGroups")
   //     /TitleBar                               (24h)
   //       /BGImage / CategoryLabel              header bg + title
   //     /Searchbar                              (36h)
   //       /BG / FilterInputField                bluegrey row + 24h input
   //     /Viewport/Scrollbar/Handle              build_menu_scrollbar_*
   //     /Viewport/Contents/SubCategory/Header   (16h)
   //       /BarLeft (8x2) / Arrow (12x8) / Label / BarRight (flex×2)
   //   PlanScreen.allBuildingToggles[*]/BG       row visuals (web_button)
   internal static class OniUiTokens {
      // Backing fields — null/default means "not stolen, use default".
      private static Color?         _headerBg;
      private static float?         _headerHeight;
      private static TMP_FontAsset  _headerFont;
      private static float?         _headerFontSize;
      private static Color?         _headerText;

      private static Color?         _subheaderBg;
      private static float?         _subheaderHeight;

      private static Color?         _inputBg;
      private static float?         _inputHeight;
      private static TMP_FontAsset  _inputFont;
      private static float?         _inputFontSize;
      private static Color?         _inputText;
      private static Color?         _inputPlaceholder;

      private static TMP_FontAsset  _sectionFont;
      private static float?         _sectionFontSize;
      private static Color?         _sectionText;
      private static Color?         _sectionRule;
      private static float?         _sectionHeight;
      private static float?         _sectionRuleHeight;
      private static float?         _sectionBarLeftWidth;
      private static Vector2?       _sectionArrowSize;
      private static Sprite         _sectionBarSprite;
      private static Sprite         _sectionArrowSprite;

      private static Color?         _rowBgNormal;
      private static Color?         _rowBgHover;
      private static Sprite         _rowBgSprite;
      private static TMP_FontAsset  _rowFont;
      private static float?         _rowFontSize;
      private static Color?         _rowText;
      private static Material       _rowIconMaterial;

      private static float?         _scrollbarWidth;
      private static Color?         _scrollbarTrackColor;
      private static Sprite         _scrollbarTrackSprite;
      private static Color?         _scrollbarHandleColor;
      private static Sprite         _scrollbarHandleSprite;
      private static Vector2?       _scrollbarHandleInset;

      private static float?         _scrollElasticity;
      private static float?         _scrollSensitivity;
      private static float?         _scrollDecelerationRate;
      private static bool?          _scrollInertia;

      // Public surface — stolen value if present, else default.
      public static Color         HeaderBg            => _headerBg            ?? ScopeUiDefaults.HeaderBg;
      public static float         HeaderHeight        => _headerHeight        ?? ScopeUiDefaults.HeaderHeight;
      public static TMP_FontAsset HeaderFont          => _headerFont          ?? ScopeUiDefaults.HeaderFont;
      public static float         HeaderFontSize      => _headerFontSize      ?? ScopeUiDefaults.HeaderFontSize;
      public static Color         HeaderText          => _headerText          ?? ScopeUiDefaults.HeaderText;

      public static Color         SubheaderBg         => _subheaderBg         ?? ScopeUiDefaults.SubheaderBg;
      public static float         SubheaderHeight     => _subheaderHeight     ?? ScopeUiDefaults.SubheaderHeight;

      public static Color         InputBg             => _inputBg             ?? ScopeUiDefaults.InputBg;
      public static float         InputHeight         => _inputHeight         ?? ScopeUiDefaults.InputHeight;
      public static TMP_FontAsset InputFont           => _inputFont           ?? ScopeUiDefaults.InputFont;
      public static float         InputFontSize       => _inputFontSize       ?? ScopeUiDefaults.InputFontSize;
      public static Color         InputText           => _inputText           ?? ScopeUiDefaults.InputText;
      public static Color         InputPlaceholder    => _inputPlaceholder    ?? ScopeUiDefaults.InputPlaceholder;

      public static Color         BodyBg              => ScopeUiDefaults.BodyBg;

      public static TMP_FontAsset SectionFont         => _sectionFont         ?? ScopeUiDefaults.SectionFont;
      public static float         SectionFontSize     => _sectionFontSize     ?? ScopeUiDefaults.SectionFontSize;
      public static Color         SectionText         => _sectionText         ?? ScopeUiDefaults.SectionText;
      public static Color         SectionRule         => _sectionRule         ?? ScopeUiDefaults.SectionRule;
      public static float         SectionHeight       => _sectionHeight       ?? ScopeUiDefaults.SectionHeight;
      public static float         SectionRuleHeight   => _sectionRuleHeight   ?? ScopeUiDefaults.SectionRuleHeight;
      public static float         SectionBarLeftWidth => _sectionBarLeftWidth ?? ScopeUiDefaults.SectionBarLeftWidth;
      public static Vector2       SectionArrowSize    => _sectionArrowSize    ?? ScopeUiDefaults.SectionArrowSize;
      public static Sprite        SectionBarSprite    => _sectionBarSprite    ?? ScopeUiDefaults.SectionBarSprite;
      public static Sprite        SectionArrowSprite  => _sectionArrowSprite  ?? ScopeUiDefaults.SectionArrowSprite;

      public static float         RowHeight           => ScopeUiDefaults.RowHeight;
      public static Color         RowBgNormal         => _rowBgNormal         ?? ScopeUiDefaults.RowBgNormal;
      // Lerp fallback so a stolen RowBgNormal still drives a matching hover.
      public static Color         RowBgHover          => _rowBgHover          ?? Color.Lerp(RowBgNormal, Color.white, 0.18f);
      public static Sprite        RowBgSprite         => _rowBgSprite         ?? ScopeUiDefaults.RowBgSprite;
      public static TMP_FontAsset RowFont             => _rowFont             ?? ScopeUiDefaults.RowFont;
      public static float         RowFontSize         => _rowFontSize         ?? ScopeUiDefaults.RowFontSize;
      public static Color         RowText             => _rowText             ?? ScopeUiDefaults.RowText;
      public static float         RowIconSize         => ScopeUiDefaults.RowIconSize;
      public static Material      RowIconMaterial     => _rowIconMaterial     ?? ScopeUiDefaults.RowIconMaterial;

      public static float         ScrollbarWidth        => _scrollbarWidth        ?? ScopeUiDefaults.ScrollbarWidth;
      public static Color         ScrollbarTrackColor   => _scrollbarTrackColor   ?? ScopeUiDefaults.ScrollbarTrackColor;
      public static Sprite        ScrollbarTrackSprite  => _scrollbarTrackSprite  ?? ScopeUiDefaults.ScrollbarTrackSprite;
      public static Color         ScrollbarHandleColor  => _scrollbarHandleColor  ?? ScopeUiDefaults.ScrollbarHandleColor;
      public static Sprite        ScrollbarHandleSprite => _scrollbarHandleSprite ?? ScopeUiDefaults.ScrollbarHandleSprite;
      public static Vector2       ScrollbarHandleInset  => _scrollbarHandleInset  ?? ScopeUiDefaults.ScrollbarHandleInset;

      public static float         ScrollElasticity       => _scrollElasticity       ?? ScopeUiDefaults.ScrollElasticity;
      public static float         ScrollSensitivity      => _scrollSensitivity      ?? ScopeUiDefaults.ScrollSensitivity;
      public static float         ScrollDecelerationRate => _scrollDecelerationRate ?? ScopeUiDefaults.ScrollDecelerationRate;
      public static bool          ScrollInertia          => _scrollInertia          ?? ScopeUiDefaults.ScrollInertia;

      // Gates the [Scope-Tokens] summary + [Scope-UI-Dump] hierarchy logs.
      // The dump alone is ~4000 lines; leave off in normal use.
      private const bool LOG_DEBUG = false;

      private static bool extracted;
      private static bool dumped;

      public static void EnsureExtracted() {
         if (extracted) return;
         extracted = true; // even on partial failure, don't keep retrying every open

         try {
            ExtractFromLiveScene();
         } catch (Exception ex) {
            Debug.LogWarning($"[Scope] Token extraction outer failure; using fallbacks: {ex}");
         }

#pragma warning disable CS0162 // unreachable when LOG_DEBUG is const false
         if (LOG_DEBUG) {
            try { LogResolvedTokens(); }
            catch (Exception ex) { Debug.LogWarning($"[Scope] Token log failed: {ex.Message}"); }

            if (!dumped) {
               dumped = true;
               try { DumpHierarchies(); }
               catch (Exception ex) { Debug.LogWarning($"[Scope] Hierarchy dump failed: {ex.Message}"); }
            }
         }
#pragma warning restore CS0162
      }

      private static void ExtractFromLiveScene() {
         var ps  = PlanScreen.Instance;
         var bgs = BuildingGroupScreen.Instance;

         if (bgs == null) {
            Debug.LogWarning("[Scope] BuildingGroupScreen.Instance is null at extraction time; using fallbacks.");
         } else {
            TryExtract("TitleBar", () => {
               var titleBar = bgs.transform.Find("TitleBar");
               if (titleBar == null) return;
               if (titleBar is RectTransform tbRT) _headerHeight = tbRT.sizeDelta.y;

               var bgImg = titleBar.Find("BGImage")?.GetComponent<Image>();
               if (bgImg != null) _headerBg = bgImg.color;

               var label = titleBar.Find("CategoryLabel")?.GetComponent<TextMeshProUGUI>();
               if (label != null) {
                  _headerFont     = label.font;
                  _headerFontSize = label.fontSize;
                  _headerText     = label.color;
               }
            });

            TryExtract("Searchbar", () => {
               var sb = bgs.transform.Find("Searchbar");
               if (sb == null) return;
               if (sb is RectTransform sbRT) _subheaderHeight = sbRT.sizeDelta.y;

               var sbBg = sb.Find("BG")?.GetComponent<Image>();
               if (sbBg != null) _subheaderBg = sbBg.color;

               var fif = sb.Find("FilterInputField");
               if (fif is RectTransform fifRT) _inputHeight = fifRT.sizeDelta.y;
               var fifImg = fif?.GetComponent<Image>();
               if (fifImg != null) _inputBg = fifImg.color;

               var ph = fif?.Find("Text Area/Placeholder")?.GetComponent<TextMeshProUGUI>();
               if (ph != null) _inputPlaceholder = ph.color;

               var txt = fif?.Find("Text Area/Text")?.GetComponent<TextMeshProUGUI>();
               if (txt != null) {
                  _inputFont     = txt.font;
                  _inputFontSize = txt.fontSize;
                  _inputText     = txt.color;
               }
            });

            TryExtract("ScrollRect feel", () => {
               var scroll = PlanScreen.Instance?.BuildingGroupContentsRect?.GetComponent<ScrollRect>();
               if (scroll == null) return;
               _scrollElasticity       = scroll.elasticity;
               _scrollSensitivity      = scroll.scrollSensitivity;
               _scrollDecelerationRate = scroll.decelerationRate;
               _scrollInertia          = scroll.inertia;
            });

            TryExtract("Scrollbar", () => {
               var sbTrack = bgs.transform.Find("Viewport/Scrollbar");
               if (sbTrack == null) return;

               if (sbTrack is RectTransform sbRT) _scrollbarWidth = sbRT.sizeDelta.x;

               var trackImg = sbTrack.GetComponent<Image>();
               if (trackImg != null) {
                  _scrollbarTrackColor  = trackImg.color;
                  if (trackImg.sprite != null) _scrollbarTrackSprite = trackImg.sprite;
               }

               var handleT = sbTrack.Find("Handle");
               if (handleT == null) return;

               var handleImg = handleT.GetComponent<Image>();
               if (handleImg != null) {
                  _scrollbarHandleColor  = handleImg.color;
                  if (handleImg.sprite != null) _scrollbarHandleSprite = handleImg.sprite;
               }
               if (handleT is RectTransform handleRT) {
                  _scrollbarHandleInset = handleRT.sizeDelta;
               }
            });

            TryExtract("SubCategory header", () => {
               var contents = bgs.transform.Find("Viewport/Contents");
               if (contents == null) return;

               Transform headerT = null;
               for (int i = 0; i < contents.childCount; i++) {
                  var child = contents.GetChild(i);
                  if (child.name != "SubCategory") continue;
                  headerT = child.Find("Header");
                  if (headerT != null) break;
               }
               if (headerT == null) return;

               if (headerT is RectTransform hRT) _sectionHeight = hRT.sizeDelta.y;

               var arrow = headerT.Find("Arrow")?.GetComponent<Image>();
               if (arrow != null) {
                  // arrow.color isn't extracted: it's just SectionText (black) under a different name.
                  _sectionArrowSprite = arrow.sprite;
                  if (arrow.transform is RectTransform aRT) _sectionArrowSize = aRT.sizeDelta;
               }

               var barL = headerT.Find("BarLeft")?.GetComponent<Image>();
               if (barL != null) {
                  _sectionRule      = barL.color;
                  _sectionBarSprite = barL.sprite;
                  if (barL.transform is RectTransform blRT) {
                     _sectionRuleHeight   = blRT.sizeDelta.y;
                     _sectionBarLeftWidth = blRT.sizeDelta.x;
                  }
               }

               var label = headerT.Find("Label")?.GetComponent<TextMeshProUGUI>();
               if (label != null) {
                  _sectionFont     = label.font;
                  _sectionFontSize = label.fontSize;
                  _sectionText     = label.color;
               }
            });
         }

         if (ps == null) {
            Debug.LogWarning("[Scope] PlanScreen.Instance is null at extraction time; using fallbacks.");
         } else {
            TryExtract("Default icon material", () => {
               if (ps.defaultUIMaterial != null) _rowIconMaterial = ps.defaultUIMaterial;
            });

            TryExtract("Row visuals", () => {
               if (ps.allBuildingToggles == null || ps.allBuildingToggles.Count == 0) return;
               var pbt = ps.allBuildingToggles.Values.FirstOrDefault(x => x != null);
               if (pbt == null) return;

               var bgT = pbt.transform.Find("BG");

               // Sample states[0] (canonical buildable+unselected) — sampling
               // the live Image.color picks up whichever MultiToggle state
               // happens to be active, so a fresh save before anything's
               // researched bakes the "not buildable" grey instead.
               var mt = pbt.toggle;
               if (mt != null && mt.states != null && mt.states.Length > 0) {
                  var s0 = mt.states[0];
                  _rowBgNormal = s0.color;
                  if (s0.sprite != null) _rowBgSprite = s0.sprite;
                  if (s0.use_color_on_hover) _rowBgHover = s0.color_on_hover;
               } else {
                  var bgImg = bgT?.GetComponent<Image>();
                  if (bgImg != null) {
                     _rowBgNormal = bgImg.color;
                     if (bgImg.sprite != null) _rowBgSprite = bgImg.sprite;
                  }
               }

               var lbl = pbt.text_listView
                         ?? bgT?.Find("NameLabel_ListView")?.GetComponent<TextMeshProUGUI>();
               if (lbl != null) {
                  _rowFont     = lbl.font;
                  _rowFontSize = lbl.fontSize;
                  _rowText     = lbl.color;
               }
            });
         }
      }

      private static void TryExtract(string what, System.Action a) {
         try { a(); }
         catch (Exception ex) { Debug.LogWarning($"[Scope] Failed to extract {what}: {ex.Message}"); }
      }

      // Compact token summary; Each line shows the resolved value and
      // where it came from (extracted vs default).
      //
      // TODO: format to be copy-pasteable into ScopeUiDefaults.cs when
      // we want to bake current game values.
      private static void LogResolvedTokens() {
         var sb = new StringBuilder();
         sb.AppendLine("[Scope-Tokens] BEGIN  (paste into ScopeUiDefaults.cs to bake current game values)");

         LogColor (sb, "HeaderBg",            _headerBg.HasValue,            HeaderBg);
         LogFloat (sb, "HeaderHeight",        _headerHeight.HasValue,        HeaderHeight);
         LogFont  (sb, "HeaderFont",          _headerFont != null,           HeaderFont);
         LogFloat (sb, "HeaderFontSize",      _headerFontSize.HasValue,      HeaderFontSize);
         LogColor (sb, "HeaderText",          _headerText.HasValue,          HeaderText);

         LogColor (sb, "SubheaderBg",         _subheaderBg.HasValue,         SubheaderBg);
         LogFloat (sb, "SubheaderHeight",     _subheaderHeight.HasValue,     SubheaderHeight);

         LogColor (sb, "InputBg",             _inputBg.HasValue,             InputBg);
         LogFloat (sb, "InputHeight",         _inputHeight.HasValue,         InputHeight);
         LogFont  (sb, "InputFont",           _inputFont != null,            InputFont);
         LogFloat (sb, "InputFontSize",       _inputFontSize.HasValue,       InputFontSize);
         LogColor (sb, "InputText",           _inputText.HasValue,           InputText);
         LogColor (sb, "InputPlaceholder",    _inputPlaceholder.HasValue,    InputPlaceholder);

         LogColor (sb, "BodyBg",              false,                         BodyBg);

         LogFont  (sb, "SectionFont",         _sectionFont != null,          SectionFont);
         LogFloat (sb, "SectionFontSize",     _sectionFontSize.HasValue,     SectionFontSize);
         LogColor (sb, "SectionText",         _sectionText.HasValue,         SectionText);
         LogColor (sb, "SectionRule",         _sectionRule.HasValue,         SectionRule);
         LogFloat (sb, "SectionHeight",       _sectionHeight.HasValue,       SectionHeight);
         LogFloat (sb, "SectionRuleHeight",   _sectionRuleHeight.HasValue,   SectionRuleHeight);
         LogFloat (sb, "SectionBarLeftWidth", _sectionBarLeftWidth.HasValue, SectionBarLeftWidth);
         LogVec2  (sb, "SectionArrowSize",    _sectionArrowSize.HasValue,    SectionArrowSize);
         LogSprite(sb, "SectionBarSprite",    _sectionBarSprite != null,     SectionBarSprite);
         LogSprite(sb, "SectionArrowSprite",  _sectionArrowSprite != null,   SectionArrowSprite);

         LogFloat (sb, "RowHeight",           false,                         RowHeight);
         LogColor (sb, "RowBgNormal",         _rowBgNormal.HasValue,         RowBgNormal);
         LogColor (sb, "RowBgHover",          _rowBgHover.HasValue,          RowBgHover);
         LogSprite(sb, "RowBgSprite",         _rowBgSprite != null,          RowBgSprite);
         LogFont  (sb, "RowFont",             _rowFont != null,              RowFont);
         LogFloat (sb, "RowFontSize",         _rowFontSize.HasValue,         RowFontSize);
         LogColor (sb, "RowText",             _rowText.HasValue,             RowText);
         LogFloat (sb, "RowIconSize",         false,                         RowIconSize);
         LogMat   (sb, "RowIconMaterial",     _rowIconMaterial != null,      RowIconMaterial);

         LogFloat (sb, "ScrollbarWidth",         _scrollbarWidth.HasValue,         ScrollbarWidth);
         LogColor (sb, "ScrollbarTrackColor",    _scrollbarTrackColor.HasValue,    ScrollbarTrackColor);
         LogSprite(sb, "ScrollbarTrackSprite",   _scrollbarTrackSprite != null,    ScrollbarTrackSprite);
         LogColor (sb, "ScrollbarHandleColor",   _scrollbarHandleColor.HasValue,   ScrollbarHandleColor);
         LogSprite(sb, "ScrollbarHandleSprite",  _scrollbarHandleSprite != null,   ScrollbarHandleSprite);
         LogVec2  (sb, "ScrollbarHandleInset",   _scrollbarHandleInset.HasValue,   ScrollbarHandleInset);

         LogFloat (sb, "ScrollElasticity",       _scrollElasticity.HasValue,       ScrollElasticity);
         LogFloat (sb, "ScrollSensitivity",      _scrollSensitivity.HasValue,      ScrollSensitivity);
         LogFloat (sb, "ScrollDecelerationRate", _scrollDecelerationRate.HasValue, ScrollDecelerationRate);
         LogBool  (sb, "ScrollInertia",          _scrollInertia.HasValue,          ScrollInertia);

         sb.AppendLine("[Scope-Tokens] END");
         Debug.Log(sb.ToString());
      }

      private static void LogColor(StringBuilder sb, string name, bool extracted, Color c) {
         var src = extracted ? "extracted" : "default";
         var c32 = (Color32)c;
         sb.AppendLine(
            $"[Scope-Tokens] {name,-22} = new Color32({c32.r,3}, {c32.g,3}, {c32.b,3}, {c32.a,3})"
            + $"  // #{ColorUtility.ToHtmlStringRGBA(c)} ({src})"
         );
      }
      private static void LogFloat(StringBuilder sb, string name, bool extracted, float v) {
         var src = extracted ? "extracted" : "default";
         sb.AppendLine($"[Scope-Tokens] {name,-22} = {v}f;  // ({src})");
      }
      private static void LogBool(StringBuilder sb, string name, bool extracted, bool v) {
         var src = extracted ? "extracted" : "default";
         sb.AppendLine($"[Scope-Tokens] {name,-22} = {(v ? "true" : "false")};  // ({src})");
      }
      private static void LogVec2(StringBuilder sb, string name, bool extracted, Vector2 v) {
         var src = extracted ? "extracted" : "default";
         sb.AppendLine($"[Scope-Tokens] {name,-22} = new Vector2({v.x}f, {v.y}f);  // ({src})");
      }
      private static void LogFont(StringBuilder sb, string name, bool extracted, TMP_FontAsset f) {
         var src = extracted ? "extracted" : "default (PLib)";
         var n = f != null ? f.name : "<null>";
         sb.AppendLine($"[Scope-Tokens] {name,-22} = {n}  // ({src})");
      }
      private static void LogMat(StringBuilder sb, string name, bool extracted, Material m) {
         var src = extracted ? "extracted" : "default (null)";
         var n = m != null ? m.name : "<null>";
         sb.AppendLine($"[Scope-Tokens] {name,-22} = {n}  // ({src})");
      }
      private static void LogSprite(StringBuilder sb, string name, bool extracted, Sprite s) {
         var src = extracted ? "extracted" : "default (null)";
         var n = s != null ? s.name : "<null>";
         sb.AppendLine($"[Scope-Tokens] {name,-22} = {n}  // ({src})");
      }

      // One-shot debug dump. Logged once per game session on first
      // extraction so the user can paste it back to refine extraction
      // paths.
      public static void DumpHierarchies() {
         var sb = new StringBuilder();
         sb.AppendLine("[Scope-UI-Dump] BEGIN");
         var ps = PlanScreen.Instance;
         if (ps != null) {
            sb.AppendLine("[Scope-UI-Dump] --- PlanScreen ---");
            DumpRec(ps.transform, 0, sb);
         } else {
            sb.AppendLine("[Scope-UI-Dump] PlanScreen.Instance == null");
         }
         var bgs = BuildingGroupScreen.Instance;
         if (bgs != null) {
            sb.AppendLine("[Scope-UI-Dump] --- BuildingGroupScreen ---");
            DumpRec(bgs.transform, 0, sb);
         } else {
            sb.AppendLine("[Scope-UI-Dump] BuildingGroupScreen.Instance == null");
         }
         sb.AppendLine("[Scope-UI-Dump] END");
         Debug.Log(sb.ToString());
      }

      private static void DumpRec(Transform t, int depth, StringBuilder sb) {
         if (t == null) return;
         var indent = new string(' ', depth * 2);
         sb.Append("[Scope-UI-Dump] ").Append(indent).Append(t.name);

         if (t is RectTransform rt) sb.Append($" rect={rt.sizeDelta}");

         var img = t.GetComponent<Image>();
         if (img != null) {
            var sprite = img.sprite != null ? img.sprite.name : "<null>";
            sb.Append($" Image[color=#{ColorUtility.ToHtmlStringRGBA(img.color)} sprite={sprite}]");
         }

         var tmp = t.GetComponent<TextMeshProUGUI>();
         if (tmp != null) {
            var fontName = tmp.font != null ? tmp.font.name : "<null>";
            sb.Append($" TMP[font={fontName} size={tmp.fontSize} color=#{ColorUtility.ToHtmlStringRGBA(tmp.color)}]");
         }

         sb.AppendLine();

         // Bound depth — these trees can be deep; 8 is enough to find the
         // visually-meaningful nodes without flooding logs.
         if (depth >= 8) return;
         for (int i = 0; i < t.childCount; i++) DumpRec(t.GetChild(i), depth + 1, sb);
      }
   }
}
