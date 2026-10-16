Run the CPU-only refresh regression from the repository root with .NET 8:

```powershell
dotnet run --project tests/BridgeRefreshChecks.csproj -- 'C:/path/to/recognized/OptiScaler/version.dll'
```

The argument must be the pinned OptiScaler proxy that `BridgeSettings` recognises.
The check copies it into a disposable directory. It never loads or modifies the
supplied file. All INI edits, backups and placeholder dependency files stay in
that directory, and the check removes it afterwards.

CI runs `tests/Invoke-BridgeRefreshChecks.ps1` instead. The script downloads the
locked OptiScaler archive, verifies its SHA-256 and extracts only the proxy
fixture. It never loads the DLL. To use an existing dependency cache instead of
downloading, pass `-DependencyDirectory 'C:/path/to/cache'`. Use `-Dotnet` to
select a .NET 8 executable.

The check compiles the production bridge, feature profiles and INI editor with
small path and GPU presentation stubs. It verifies that:

- saved settings refresh when Home opens and while Home stays visible
- unrelated F10 settings and new Neural Rendering styles survive provider changes
- startup snapshots stay unchanged
- a save that races a drawn profile is refused and refreshed for a deliberate retry
- writes still validate the optional proxy

This also covers OptiScaler saves made automatically after the managed plugin
takes its startup snapshot.
