using System;
using Roslyn.Utilities;
using UnityEngine;

namespace ScopeMod.UI;

/// <summary>
/// Authoritative selection state for the result list — which row
/// <c>&lt;return&gt;</c> activates, and which row renders highlighted (the
/// two are deliberately the same value).
/// </summary>
/// <remarks>
/// <para>Most-recent-input wins. Keyboard (arrows / typing) updates
/// <see cref="KeyboardRow"/>; mouse position is re-derived each frame from
/// live row rects via <see cref="PollMouse"/>'s <c>rowAt</c>.</para>
///
/// <para>Valid transitions (anything else is a bug):</para>
/// <code>
///   trigger                                              | Attention  | KeyboardRow | Armed
///   -----------------------------------------------------+------------+-------------+------
///   Reset() at scope-open                                | =Keyboard  | =0          | =false
///   PollMouse, moved, now over row i                     | =Mouse     | unchg       | =true
///   PollMouse, moved, in panel but no row (gap/header)   | unchg      | unchg       | =true
///   PollMouse, moved, outside panel                      | =Keyboard  | unchg       | =true
///   PollMouse, stationary                                | unchg      | unchg       | unchg
///   SetKeyboard(idx, n)  [arrow keys, typing-rebuild]    | =Keyboard  | clamp       | unchg
///   OnRowsRebuilt(n)     [heartbeat, section toggle]     | unchg      | clamp       | unchg
/// </code>
///
/// <para>Three non-obvious choices:</para>
/// <list type="number">
///   <item>
///      <see cref="Effective"/> is <c>int?</c>; <c>null</c> in
///      mouse-attentive mode when the cursor sits in a row gap, avoiding a
///      flash to the keyboard row on every gap-traversal.
///   </item>
///   <item>
///      Motion is detected via <c>Input.mousePosition</c> deltas, not
///      <c>IPointerEnter/Exit</c> — row rebuilds re-fire pointer events at a
///      stationary cursor (layout artifact, not user input).
///   </item>
///   <item>
///      <see cref="OnRowsRebuilt"/> intentionally does not touch <see
///      cref="Attention"/>: typing-rebuilds let <see cref="SetKeyboard"/>
///      flip it deliberately; the 0.25s heartbeat must preserve a mouse
///      hover.
///   </item>
/// </list>
/// </remarks>
internal sealed class ScopeSelection
{
   internal enum Source
   {
      Keyboard,
      Mouse,
   }

   internal Source Attention { get; private set; } = Source.Keyboard;
   internal int KeyboardRow { get; private set; }
   internal int? MouseRow { get; private set; }
   internal bool Armed { get; private set; }

   private Vector2 lastMousePos;

   /// <summary>Row to highlight and to activate on <c>&lt;return&gt;</c>;
   /// <c>null</c> ⇒ no-op.</summary>
   internal int? Effective => Attention == Source.Mouse && Armed ? MouseRow : (int?)KeyboardRow;

   /// <param name="mousePos">Stored as the baseline for the next
   /// <see cref="PollMouse"/>'s motion check, so the open frame doesn't
   /// arm when the cursor was already over a row.</param>
   internal void Reset(Vector2 mousePos)
   {
      Attention = Source.Keyboard;
      KeyboardRow = 0;
      MouseRow = null;
      Armed = false;
      lastMousePos = mousePos;
   }

   /// <summary>Sets <see cref="KeyboardRow"/> (wrapping modulo
   /// <paramref name="rowCount"/>) and flips <see cref="Attention"/> to
   /// <see cref="Source.Keyboard"/>; the only mutator that does so.</summary>
   internal void SetKeyboard(int idx, int rowCount)
   {
      KeyboardRow = Wrap(idx, rowCount);
      Attention = Source.Keyboard;
   }

   /// <summary>Re-clamps <see cref="KeyboardRow"/> against a new row count
   /// without claiming user input.</summary>
   internal void OnRowsRebuilt(int rowCount)
   {
      KeyboardRow = Wrap(KeyboardRow, rowCount);
   }

   /// <param name="rowAt">Row index under <paramref name="pos"/>, or
   /// <c>null</c>.</param>
   /// <param name="inPanel">Whether <paramref name="pos"/> is inside the
   /// scope panel.</param>
   [PerformanceSensitive("scope-overlay-per-frame")]
   internal void PollMouse(Vector2 pos, Func<Vector2, int?> rowAt, Func<Vector2, bool> inPanel)
   {
      bool moved = pos != lastMousePos;
      lastMousePos = pos;
      if (moved)
         Armed = true;

      MouseRow = rowAt(pos);

      if (moved)
      {
         if (MouseRow.HasValue)
            Attention = Source.Mouse;
         else if (!inPanel(pos))
            Attention = Source.Keyboard;
      }
   }

   private static int Wrap(int idx, int rowCount)
   {
      if (rowCount <= 0)
         return 0;
      return ((idx % rowCount) + rowCount) % rowCount;
   }
}
