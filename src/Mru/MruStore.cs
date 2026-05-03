using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ScopeMod.Mru
{
   // MRU keyed by namespace. "" is the global MRU; sub-namespaces
   // ("material:<building>") for per-parent sub-MRUs.
   internal sealed class MruStore
   {
      public const int DEFAULT_MAX = 50;

      private sealed class State
      {
         [JsonProperty("lists")]
         public Dictionary<string, List<string>> Lists { get; set; } =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
      }

      private readonly Func<string> loader;
      private readonly Action<string> saver;
      private readonly Action<string> warn;
      private readonly int maxPerList;

      private State state = new State();
      private bool dirty;
      private bool warnedSaveFailure; // throttle: warn-then-quiet

      public MruStore(
         Func<string> loader,
         Action<string> saver,
         Action<string> warn = null,
         int maxPerList = DEFAULT_MAX
      )
      {
         this.loader = loader;
         this.saver = saver;
         this.warn = warn ?? (_ => { });
         this.maxPerList = maxPerList > 0 ? maxPerList : DEFAULT_MAX;
      }

      public static MruStore ForFile(
         string path,
         Action<string> warn = null,
         int maxPerList = DEFAULT_MAX
      )
      {
         Func<string> loader = () => File.Exists(path) ? File.ReadAllText(path) : "";
         Action<string> saver = contents =>
         {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
               Directory.CreateDirectory(dir);
            File.WriteAllText(path, contents);
         };
         return new MruStore(loader, saver, warn, maxPerList);
      }

      public IReadOnlyList<string> Keys => KeysIn("");

      public int IndexOf(string key) => IndexOf("", key);

      public void Record(string key) => Record("", key);

      public IReadOnlyList<string> KeysIn(string ns) =>
         state.Lists.TryGetValue(ns ?? "", out var list) ? (IReadOnlyList<string>)list : [];

      public int IndexOf(string ns, string key)
      {
         if (string.IsNullOrEmpty(key))
            return -1;
         var list = KeysIn(ns);
         for (int i = 0; i < list.Count; i++)
         {
            if (string.Equals(list[i], key, StringComparison.Ordinal))
               return i;
         }
         return -1;
      }

      public void Record(string ns, string key)
      {
         if (string.IsNullOrEmpty(key))
            return;
         var nsKey = ns ?? "";
         if (!state.Lists.TryGetValue(nsKey, out var list))
         {
            list = new List<string>(Math.Min(maxPerList, 16));
            state.Lists[nsKey] = list;
         }
         list.RemoveAll(k => string.Equals(k, key, StringComparison.Ordinal));
         list.Insert(0, key);
         if (list.Count > maxPerList)
            list.RemoveRange(maxPerList, list.Count - maxPerList);
         dirty = true;
      }

      public bool IsDirty => dirty;

      // No defense against hand-edited corruption; next Record dedupes naturally.
      public void Load()
      {
         dirty = false;
         string raw;
         try
         {
            raw = loader() ?? "";
         }
         catch (Exception ex)
         {
            warn($"MRU load failed; starting with empty state: {ex.Message}");
            state = new State();
            return;
         }
         if (string.IsNullOrWhiteSpace(raw))
         {
            state = new State();
            return;
         }
         try
         {
            state = JsonConvert.DeserializeObject<State>(raw) ?? new State();
            state.Lists ??= new Dictionary<string, List<string>>(StringComparer.Ordinal);
         }
         catch (Exception ex)
         {
            warn($"MRU parse failed; starting with empty state: {ex.Message}");
            state = new State();
         }
      }

      public void Save()
      {
         if (!dirty)
            return;
         string contents;
         try
         {
            contents = JsonConvert.SerializeObject(state, Formatting.Indented);
         }
         catch (Exception ex)
         {
            warn($"MRU serialize failed; in-memory state retained: {ex.Message}");
            return;
         }
         try
         {
            saver(contents);
            dirty = false;
            warnedSaveFailure = false;
         }
         catch (Exception ex)
         {
            if (!warnedSaveFailure)
            {
               warn($"MRU save failed; in-memory state retained: {ex.Message}");
               warnedSaveFailure = true;
            }
         }
      }
   }
}
