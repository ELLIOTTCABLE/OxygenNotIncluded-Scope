namespace ScopeMod
{
   internal readonly struct RankedResult
   {
      public readonly IQuickAction Action;
      public readonly int Score;

      public RankedResult(IQuickAction action, int score)
      {
         Action = action;
         Score = score;
      }
   }
}
