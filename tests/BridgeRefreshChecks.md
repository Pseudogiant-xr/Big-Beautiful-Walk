Run the CPU-only refresh regression from the repository root with .NET 8:

```powershell
dotnet run --project tests/BridgeRefreshChecks.csproj -- 'C:/path/to/recognized/OptiScaler/version.dll'
```

CI uses `tests/Invoke-BridgeRefreshChecks.ps1` to download the locked OptiScaler
archive, verify its SHA-256 and extract only the proxy fixture. It never loads
the DLL. To use an existing dependency cache instead, pass
`-DependencyDirectory 'C:/path/to/cache'`; `-Dotnet` can select a .NET 8 executable.

The argument must be the pinned OptiScaler proxy recognized by `BridgeSettings`.
The check copies it into a disposable directory and never loads it or modifies
the supplied file. All INI edits, backups and placeholder dependency files stay
in that disposable directory, which the check removes afterward.

The check compiles the production bridge, feature profiles and INI editor with
small path and GPU presentation stubs. It verifies saved settings refresh when
Home opens and while Home remains visible, unrelated F10 settings and new Neural
Rendering styles survive provider changes, startup snapshots remain unchanged,
a save racing a drawn profile is refused and refreshed for a deliberate retry,
and writes still validate the optional proxy. This also covers OptiScaler saves
made automatically after the managed plugin takes its startup snapshot.
