using System.Collections.Generic;

namespace ScopeMod {
    internal interface IActionProvider {
        // Enumerated on every overlay open. Keep cheap or cache internally.
        IEnumerable<IQuickAction> Enumerate();
    }
}
