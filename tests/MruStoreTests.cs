using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ScopeMod.Mru;
using Xunit;

namespace ScopeMod.Tests
{
   public class MruStoreTests
   {
      // In-memory store with no disk IO. Saves go into a string ref we can
      // inspect; loads pull from the same string. Default is silent (no
      // warn handler) for the bulk of the tests; tests that exercise warn
      // behaviour use StoreWithBoxAndWarnings.
      private static MruStore InMemoryStore(int max = MruStore.DEFAULT_MAX)
      {
         var box = new[] { "" };
         return new MruStore(loader: () => box[0], saver: s => box[0] = s, maxPerList: max);
      }

      private static (MruStore store, string[] box, List<string> warnings) StoreWithBoxAndWarnings(
         int max = MruStore.DEFAULT_MAX
      )
      {
         var box = new[] { "" };
         var warnings = new List<string>();
         var store = new MruStore(
            loader: () => box[0],
            saver: s => box[0] = s,
            warn: warnings.Add,
            maxPerList: max
         );
         return (store, box, warnings);
      }

      // --- Record / IndexOf / Keys (global namespace) ---

      [Fact]
      public void Record_inserts_at_front()
      {
         var s = InMemoryStore();
         s.Record("a");
         s.Record("b");
         s.Record("c");
         Assert.Equal(new[] { "c", "b", "a" }, s.Keys);
      }

      [Fact]
      public void Record_dedupes_existing_key_to_front()
      {
         var s = InMemoryStore();
         s.Record("a");
         s.Record("b");
         s.Record("c");
         s.Record("a"); // moves to front, no duplicate
         Assert.Equal(new[] { "a", "c", "b" }, s.Keys);
      }

      [Fact]
      public void Record_truncates_to_max_entries()
      {
         var s = InMemoryStore(max: 3);
         s.Record("a");
         s.Record("b");
         s.Record("c");
         s.Record("d"); // evicts "a"
         Assert.Equal(new[] { "d", "c", "b" }, s.Keys);
      }

      [Fact]
      public void Record_ignores_null_and_empty_keys()
      {
         var s = InMemoryStore();
         s.Record("a");
         s.Record(null);
         s.Record("");
         Assert.Equal(new[] { "a" }, s.Keys);
      }

      [Fact]
      public void IndexOf_returns_position_or_minus_one()
      {
         var s = InMemoryStore();
         s.Record("a");
         s.Record("b");
         Assert.Equal(0, s.IndexOf("b"));
         Assert.Equal(1, s.IndexOf("a"));
         Assert.Equal(-1, s.IndexOf("c"));
      }

      [Fact]
      public void IndexOf_returns_minus_one_for_null_or_empty()
      {
         var s = InMemoryStore();
         s.Record("a");
         Assert.Equal(-1, s.IndexOf(null));
         Assert.Equal(-1, s.IndexOf(""));
      }

      // --- Namespace isolation (sub-MRU shape) ---

      [Fact]
      public void Record_in_namespace_does_not_perturb_global()
      {
         var s = InMemoryStore();
         s.Record("a");
         s.Record("material:LadderConfig", "Sandstone");
         Assert.Equal(new[] { "a" }, s.Keys);
         Assert.Equal(new[] { "Sandstone" }, s.KeysIn("material:LadderConfig"));
      }

      [Fact]
      public void Two_namespaces_track_independently()
      {
         var s = InMemoryStore();
         s.Record("material:Ladder", "Sandstone");
         s.Record("material:Tile", "Wheezewort");
         s.Record("material:Ladder", "Granite");
         Assert.Equal(new[] { "Granite", "Sandstone" }, s.KeysIn("material:Ladder"));
         Assert.Equal(new[] { "Wheezewort" }, s.KeysIn("material:Tile"));
      }

      [Fact]
      public void IndexOf_with_namespace_scopes_lookup()
      {
         var s = InMemoryStore();
         s.Record("material:Ladder", "Sandstone");
         Assert.Equal(0, s.IndexOf("material:Ladder", "Sandstone"));
         Assert.Equal(-1, s.IndexOf("material:Tile", "Sandstone"));
         Assert.Equal(-1, s.IndexOf("Sandstone")); // global is empty
      }

      [Fact]
      public void KeysIn_returns_empty_for_unknown_namespace()
      {
         var s = InMemoryStore();
         Assert.Empty(s.KeysIn("material:Nonexistent"));
      }

      // --- Save / Load round-trip (JSON) ---

      [Fact]
      public void Save_then_Load_round_trips_global()
      {
         var (s1, box, _) = StoreWithBoxAndWarnings();
         s1.Record("a");
         s1.Record("b");
         s1.Record("c");
         s1.Save();

         var s2 = new MruStore(loader: () => box[0], saver: _ => { });
         s2.Load();
         Assert.Equal(new[] { "c", "b", "a" }, s2.Keys);
      }

      [Fact]
      public void Save_then_Load_round_trips_with_namespaces()
      {
         var (s1, box, _) = StoreWithBoxAndWarnings();
         s1.Record("building:LadderConfig");
         s1.Record("material:LadderConfig", "Sandstone");
         s1.Record("material:LadderConfig", "Granite");
         s1.Save();

         var s2 = new MruStore(loader: () => box[0], saver: _ => { });
         s2.Load();
         Assert.Equal(new[] { "building:LadderConfig" }, s2.Keys);
         Assert.Equal(new[] { "Granite", "Sandstone" }, s2.KeysIn("material:LadderConfig"));
      }

      [Fact]
      public void Save_skips_write_when_not_dirty()
      {
         int writes = 0;
         var s = new MruStore(loader: () => "", saver: _ => writes++);
         s.Save();
         Assert.Equal(0, writes);

         s.Record("a");
         s.Save();
         Assert.Equal(1, writes);

         s.Save();
         Assert.Equal(1, writes);
      }

      [Fact]
      public void Save_writes_json_with_lists_object()
      {
         string written = null;
         var s = new MruStore(loader: () => "", saver: x => written = x);
         s.Record("foo");
         s.Record("bar:baz");
         s.Save();
         Assert.NotNull(written);
         var parsed = JObject.Parse(written);
         var globalList = parsed["lists"][""].ToObject<string[]>();
         Assert.Equal(new[] { "bar:baz", "foo" }, globalList);
      }

      // --- Load tolerance ---

      [Fact]
      public void Load_tolerates_empty_input()
      {
         var s = new MruStore(loader: () => "", saver: _ => { });
         s.Load();
         Assert.Empty(s.Keys);
      }

      [Fact]
      public void Load_tolerates_loader_throwing_and_warns()
      {
         var warnings = new List<string>();
         var s = new MruStore(
            loader: () => throw new Exception("disk gone"),
            saver: _ => { },
            warn: warnings.Add
         );
         s.Load();
         Assert.Empty(s.Keys);
         Assert.Single(warnings);
         Assert.Contains("MRU load failed", warnings[0]);
         Assert.Contains("disk gone", warnings[0]);
      }

      [Fact]
      public void Load_tolerates_malformed_json_and_warns()
      {
         var warnings = new List<string>();
         var s = new MruStore(loader: () => "{not valid json", saver: _ => { }, warn: warnings.Add);
         s.Load();
         Assert.Empty(s.Keys);
         Assert.Single(warnings);
         Assert.Contains("MRU parse failed", warnings[0]);
      }

      [Fact]
      public void Load_handles_missing_lists_field()
      {
         var s = new MruStore(loader: () => "{}", saver: _ => { });
         s.Load();
         Assert.Empty(s.Keys);
      }

      // --- Save error handling (warn-then-throttle) ---

      [Fact]
      public void Save_warns_once_on_failure_then_throttles()
      {
         var warnings = new List<string>();
         var s = new MruStore(
            loader: () => "",
            saver: _ => throw new Exception("disk full"),
            warn: warnings.Add
         );
         s.Record("a");
         s.Save();
         s.Record("b");
         s.Save();
         s.Record("c");
         s.Save();
         Assert.Single(warnings);
         Assert.Contains("MRU save failed", warnings[0]);
      }

      [Fact]
      public void Save_re_arms_warning_after_recovery()
      {
         var warnings = new List<string>();
         bool fail = true;
         var s = new MruStore(
            loader: () => "",
            saver: _ =>
            {
               if (fail)
                  throw new Exception("boom");
            },
            warn: warnings.Add
         );
         s.Record("a");
         s.Save(); // fails, warns
         fail = false;
         s.Record("b");
         s.Save(); // succeeds, re-arms
         fail = true;
         s.Record("c");
         s.Save(); // fails again, warns again
         Assert.Equal(2, warnings.Count);
      }
   }
}
