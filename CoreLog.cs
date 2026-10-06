/* ----- ----- ----- ----- */
// CoreLog.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>Severity of a <see cref="CoreLog"/> line.</summary>
    public enum CoreLogLevel
    {
        Debug,
        Info,
        Warn,
        Error,
    }

    /// <summary>
    /// The rules layer's log: Core writes its debug and warning lines here and the program using
    /// it decides where they go (<see cref="Sink"/>) - the game client hands them to its engine
    /// logger, the rules host to standard error. This keeps Core free of any engine dependency.
    /// Lines are dropped while no sink is set.
    /// </summary>
    public static class CoreLog
    {
        /// <summary>Where the lines go (message, level); null drops them.</summary>
        public static Action<string, CoreLogLevel> Sink { get; set; }

        /// <summary>Writes <paramref name="message"/> at <paramref name="level"/> to the <see cref="Sink"/>.</summary>
        public static void Log(string message, CoreLogLevel level) => Sink?.Invoke(message, level);
    }
}
