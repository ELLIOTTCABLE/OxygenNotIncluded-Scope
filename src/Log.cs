using System.Runtime.CompilerServices;

namespace ScopeMod
{
   /// <summary>
   /// Four-level logger: <c>Warn</c>/<c>Info</c> gate at runtime via <see
   /// cref="RuntimeLevel"/>, while <c>Debug</c>/<c>Trace</c> evaporate at every
   /// call site (argument expressions included) when their compile-time symbol
   /// is undefined.
   ///
   /// Every call is auto-tagged with <c>File.Member</c> (via <see
   /// cref="CallerFilePathAttribute"/> + <see
   /// cref="CallerMemberNameAttribute"/>); the compiler injects literal strings
   /// at the call site, no reflection or stack walking.
   /// </summary>
   internal static class Log
   {
      public enum Level
      {
         None = 0,
         Error = 1,
         Warn = 2,
         Info = 3,
      }

      public static Level RuntimeLevel { get; set; } = Level.Warn;

      private static string Format(string file, string member, string level, string msg)
      {
         var cls = string.IsNullOrEmpty(file)
            ? null
            : System.IO.Path.GetFileNameWithoutExtension(file);
         var hasC = !string.IsNullOrEmpty(cls);
         var hasM = !string.IsNullOrEmpty(member);
         var tag =
            hasC && hasM ? cls + "." + member
            : hasC ? cls
            : hasM ? member
            : null;
         var hasL = !string.IsNullOrEmpty(level);

         if (hasL && tag != null)
            return "[ScopeMod " + level + " " + tag + "] " + msg;
         if (hasL)
            return "[ScopeMod " + level + "] " + msg;
         if (tag != null)
            return "[ScopeMod " + tag + "] " + msg;
         return "[ScopeMod] " + msg;
      }

      public static void Error(
         string msg,
         [CallerFilePath] string file = null,
         [CallerMemberName] string member = null
      )
      {
         if (RuntimeLevel >= Level.Error)
            UnityEngine.Debug.LogError(Format(file, member, null, msg));
      }

      public static void Error(
         System.Func<string> fmt,
         [CallerFilePath] string file = null,
         [CallerMemberName] string member = null
      )
      {
         if (RuntimeLevel >= Level.Error)
            UnityEngine.Debug.LogError(Format(file, member, null, fmt()));
      }

      public static void Warn(
         string msg,
         [CallerFilePath] string file = null,
         [CallerMemberName] string member = null
      )
      {
         if (RuntimeLevel >= Level.Warn)
            UnityEngine.Debug.LogWarning(Format(file, member, null, msg));
      }

      public static void Warn(
         System.Func<string> fmt,
         [CallerFilePath] string file = null,
         [CallerMemberName] string member = null
      )
      {
         if (RuntimeLevel >= Level.Warn)
            UnityEngine.Debug.LogWarning(Format(file, member, null, fmt()));
      }

      public static void Info(
         string msg,
         [CallerFilePath] string file = null,
         [CallerMemberName] string member = null
      )
      {
         if (RuntimeLevel >= Level.Info)
            UnityEngine.Debug.Log(Format(file, member, null, msg));
      }

      public static void Info(
         System.Func<string> fmt,
         [CallerFilePath] string file = null,
         [CallerMemberName] string member = null
      )
      {
         if (RuntimeLevel >= Level.Info)
            UnityEngine.Debug.Log(Format(file, member, null, fmt()));
      }

      [System.Diagnostics.Conditional("DEBUG_LOGGING")]
      public static void Debug(
         string msg,
         [CallerFilePath] string file = null,
         [CallerMemberName] string member = null
      ) => UnityEngine.Debug.Log(Format(file, member, "DEBUG", msg));

      [System.Diagnostics.Conditional("TRACE_LOGGING")]
      public static void Trace(
         string msg,
         [CallerFilePath] string file = null,
         [CallerMemberName] string member = null
      ) => UnityEngine.Debug.Log(Format(file, member, "TRACE", msg));
   }
}
