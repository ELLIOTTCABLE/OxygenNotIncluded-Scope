using PeterHan.PLib.Actions;

namespace ScopeMod
{
   internal static class ScopeStrings
   {
      public const string ActionKey = "SCOPE.ACTION.OPEN";
      public static LocString ActionTitle = "Open Scope command palette";
      public static LocString PlaceholderText = "Type to search…";

      public static readonly KKeyCode DefaultKey = KKeyCode.N;
      public static readonly Modifier DefaultModifier = Modifier.None;
   }
}
