using System.Runtime.CompilerServices;

namespace UvVisMeasurement.Driver
{
    internal enum SpectrometerBackendKind
    {
        OmniDriver,
        Simulator
    }

    /// <summary>
    /// Which backend the acquisition engine opens. Chosen once by Program before the main
    /// window exists.
    ///
    /// The OmniDriver session is only constructed inside a separate non-inlined method so
    /// that demo mode never JIT-compiles anything referencing OmniDriver types - on a PC
    /// without OmniDriver that would fail with FileNotFoundException.
    /// </summary>
    internal static class SpectrometerBackend
    {
        public static SpectrometerBackendKind Current = SpectrometerBackendKind.OmniDriver;

        public static bool IsSimulated
        {
            get { return Current == SpectrometerBackendKind.Simulator; }
        }

        public static ISpectrometerSession CreateSession()
        {
            return Current == SpectrometerBackendKind.Simulator
                ? new SimulatedSpectrometerSession()
                : CreateOmniDriverSession();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ISpectrometerSession CreateOmniDriverSession()
        {
            return new SpectrometerSession();
        }
    }
}
