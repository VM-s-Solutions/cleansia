using System.Runtime.CompilerServices;

namespace Cleansia.HostTests.Infrastructure;

/// <summary>
/// Every booted host watches its appsettings files. On macOS a watcher is an FSEvents stream, and after a few hundred
/// hosts the system refuses new ones; the refused watcher reports a change at once, the configuration re-registers
/// synchronously and the test host dies of a stack overflow (a local run stopped at test 289 of 428). Polling
/// watchers never open a stream, so they are switched on before the first host is built. Linux CI is unaffected.
/// → /architecture/local-orchestration#colima
/// </summary>
internal static class PollingFileWatchers
{
    [ModuleInitializer]
    internal static void Use() => Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
}
