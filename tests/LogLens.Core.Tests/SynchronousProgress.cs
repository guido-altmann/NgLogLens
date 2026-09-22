namespace LogLens.Core.Tests;

/// <summary>
/// Meldet sofort auf dem aufrufenden Thread. <see cref="Progress{T}"/> reicht jede
/// Meldung ohne Synchronisationskontext an den Threadpool weiter; dort ist die
/// Reihenfolge nicht garantiert, und „die letzte Meldung" ist Zufall.
/// </summary>
internal sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
