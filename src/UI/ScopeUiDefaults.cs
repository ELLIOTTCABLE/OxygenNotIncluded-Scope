using PeterHan.PLib.UI;
using TMPro;
using UnityEngine;

namespace ScopeMod.UI {
   // Hardcoded UI fallbacks. OniUiTokens supersedes any of these at runtime
   // when extraction succeeds. Re-bake from the [Scope-Tokens] log when
   // visuals diverge or extraction starts reporting (default) for things
   // that should be (extracted). Last sampled 2026-04-30.
   internal static class ScopeUiDefaults {
      // Header — Klei's TitleBar.
      public static readonly float  HeaderHeight   = 24f;
      public static readonly Color  HeaderBg       = new Color32(135,  69, 102, 255);  // sprite=web_title
      public static          TMP_FontAsset HeaderFont => PUITuning.Fonts.UILightStyle.sdfFont;
      public static readonly float  HeaderFontSize = 14f;
      public static readonly Color  HeaderText     = Color.white;

      // Subheader — Klei's Searchbar.
      public static readonly float  SubheaderHeight = 36f;
      public static readonly Color  SubheaderBg     = new Color32( 59,  61,  79, 255);

      public static readonly float  InputHeight      = 24f;
      public static readonly Color  InputBg          = Color.white;
      public static          TMP_FontAsset InputFont => PUITuning.Fonts.TextDarkStyle.sdfFont;
      public static readonly float  InputFontSize    = 14f;
      public static readonly Color  InputText        = Color.black;
      public static readonly Color  InputPlaceholder = new Color32(  0,   0,   0, 204);

      public static readonly Color  BodyBg = Color.white;

      // Section header — [BarLeft][Arrow][Label][BarRight]. Sprites null
      // by default (no PLib equivalent); overlay degrades to bar-less +
      // Unicode glyph if extraction misses.
      public static          TMP_FontAsset SectionFont => PUITuning.Fonts.TextDarkStyle.sdfFont;
      public static readonly float  SectionFontSize     = 14f;
      public static readonly Color  SectionText         = Color.black;
      public static readonly Color  SectionRule         = new Color32(135,  69, 102, 255);
      public static readonly float  SectionHeight       = 16f;
      public static readonly float  SectionRuleHeight   = 2f;
      public static readonly float  SectionBarLeftWidth = 8f;
      public static readonly Vector2 SectionArrowSize   = new Vector2(12f, 8f);
      public static readonly Sprite SectionBarSprite    = null;  // sprite=web_title
      public static readonly Sprite SectionArrowSprite  = null;  // sprite=iconDown

      // Row — see OniUiTokens for extraction paths (MultiToggle.states[0]).
      public static readonly float  RowHeight     = 36f;
      public static readonly Color  RowBgNormal   = new Color32( 62,  67,  87, 255);  // sprite=web_button
      public static readonly Color  RowBgHover    = new Color32( 80,  86, 112, 255);  // until states[0].color_on_hover lands
      public static readonly Sprite RowBgSprite   = null;
      public static          TMP_FontAsset RowFont => PUITuning.Fonts.TextLightStyle.sdfFont;
      public static readonly float  RowFontSize   = 16f;
      public static readonly Color  RowText       = Color.white;
      public static readonly float  RowIconSize   = 24f;
      public static readonly Material RowIconMaterial = null;

      // Scrollbar — Klei's Viewport/Scrollbar.
      public static readonly float   ScrollbarWidth         = 8f;
      public static readonly Color   ScrollbarTrackColor    = Color.white;
      public static readonly Sprite  ScrollbarTrackSprite   = null;  // sprite=build_menu_scrollbar_frame
      public static readonly Color   ScrollbarHandleColor   = new Color32(161, 163, 174, 255);
      public static readonly Sprite  ScrollbarHandleSprite  = null;  // sprite=build_menu_scrollbar_inner
      public static readonly Vector2 ScrollbarHandleInset   = new Vector2(-4f, -8f);

      public static readonly float ScrollElasticity       = 0.2f;
      public static readonly float ScrollSensitivity      = 1f;
      public static readonly float ScrollDecelerationRate = 0.02f;
      public static readonly bool  ScrollInertia          = true;
   }
}
