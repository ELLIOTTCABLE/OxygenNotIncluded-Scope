using Xunit;

namespace ScopeMod.Tests
{
   // Sanity-check that the test harness is wired up
   public class HarnessSmokeTests
   {
      [Fact]
      public void Harness_runs() => Assert.True(true);
   }
}
