using System.Runtime.InteropServices;

namespace SimdJson.Internal;

/// <summary>
/// Resolves and loads the SimdJsonNative shared library from the NuGet runtimes layout.
/// Call <see cref="EnsureLoaded"/> before any P/Invoke. Thread-safe; runs at most once.
/// </summary>
internal static class NativeLoader
{
    private const string LibraryName = "SimdJsonNative";
    private static nint _handle;

    internal static void EnsureLoaded()
    {
        if (_handle != 0)
        {
            return;
        }

        // Register a DllImportResolver so that every [LibraryImport("SimdJsonNative")]
        // call in this assembly gets the handle we loaded — regardless of OS conventions
        // (lib prefix, LD_LIBRARY_PATH, etc.).  Must be registered before the first
        // P/Invoke is attempted, which is why NativeMethods' static ctor calls us.
        NativeLibrary.SetDllImportResolver(
            typeof(NativeLoader).Assembly,
            static (libraryName, _, _) =>
            {
                if (libraryName != LibraryName)
                {
                    return 0;
                }

                if (_handle == 0)
                {
                    _handle = NativeLibrary.Load(ResolveLibraryPath());
                }

                return _handle;
            });

        // Pre-load now so any load failure surfaces here with a clear exception
        // rather than at the first P/Invoke call site.
        if (_handle == 0)
        {
            _handle = NativeLibrary.Load(ResolveLibraryPath());
        }
    }

    private static string ResolveLibraryPath()
    {
        string rid = GetRuntimeIdentifier();
        string libName = GetLibraryName();

        // Search in priority order. Each directory is checked for:
        //   a) runtimes/<rid>/native/<lib>  — NuGet runtimes layout / test project / source tree
        //   b) <lib>                         — flat layout (.NET deploys NuGet native assets flat)
        foreach (string dir in GetSearchDirs())
        {
            string candidate = Path.Combine(dir, "runtimes", rid, "native", libName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(dir, libName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Fall back to the OS loader search path (LD_LIBRARY_PATH / DYLD_LIBRARY_PATH / PATH).
        return libName;
    }

    private static IEnumerable<string> GetSearchDirs()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // AppContext.BaseDirectory: the app's output folder.
        // .NET deploys NuGet native assets here (flat) at build/publish time.
        string appBase = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, '/');
        if (!string.IsNullOrEmpty(appBase) && seen.Add(appBase))
        {
            yield return appBase;
        }

        // Directory containing SimdJson.Net.dll.
        // Covers project-reference and test-output scenarios where the two dirs differ.
        string? asmDir = Path.GetDirectoryName(typeof(NativeLoader).Assembly.Location);
        if (!string.IsNullOrEmpty(asmDir) && seen.Add(asmDir))
        {
            yield return asmDir;
        }
    }

    private static string GetLibraryName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "SimdJsonNative.dll";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "SimdJsonNative.dylib";
        }

        return "SimdJsonNative.so";
    }

    private static string GetRuntimeIdentifier()
    {
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64   => "x64",
            Architecture.X86   => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm   => "arm",
            _                  => "x64"
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"win-{arch}";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return $"osx-{arch}";
        }

        // Detect musl (Alpine) vs glibc
        bool isMusl = File.Exists("/lib/libc.musl-x86_64.so.1")
                   || File.Exists("/lib/libc.musl-aarch64.so.1")
                   || File.Exists("/lib/ld-musl-x86_64.so.1")
                   || File.Exists("/lib/ld-musl-aarch64.so.1");
        return isMusl ? $"linux-musl-{arch}" : $"linux-{arch}";
    }
}
