# Project rules

- The application version is defined in Directory.Build.props. User configuration has no version field. Require correct JSON commas; comments are allowed, trailing commas are rejected.
- Do not add migrations, deprecated formats, aliases, or backward compatibility unless the user explicitly requests it.
- Do not persist updater state, caches or cross-launch recovery journals. Temporary live-operation files and in-process rollback are allowed; do not replay old jobs on startup.
- Keep application data portable, under the executable directory. Autostart uses a Startup shortcut.
- User configuration is settings.json beside the executable. Log only to the console, never to files. Do not create a permanent service directory. Use uniquely named ChrManager temporary files beside the executable and delete them after use. Temporary extraction directories are allowed only for active archive operations and must be removed afterward.
- All user-facing messages must use validated, complete en/ru localization resources. Do not ship other translations.
- Console logs use typed DiagnosticKind events and SpectreLogSink. Never print raw exception messages or English log templates to the console. Domain failures carry FailureKind and arguments; the presentation layer selects localized text. Keep technical exception types and stack traces as diagnostic context.
- Run one update pass per launch in a standard console. Do not add resident mode, tray integration, window manipulation, disabled Close buttons or Ctrl+C interception.
- Keep one application project, organized into Application, Infrastructure and Presentation. Application scenarios depend on boundary interfaces, not concrete Windows, IPC, file or HTTP implementations.
- Do not use local functions, block-bodied lambdas or primary constructors for service classes in application sources. Small positional records are appropriate for data.
- Keep only short, pure expression lambdas for queries; exception assertions may use a single expression. Use named methods for event handlers, factories and callbacks with side effects.
- Do not block on async tasks or detach background tasks. Each component owns and awaits its workers; console questions are asynchronous and serialized.
- UI components report progress but never execute update operations supplied as delegates. User choices use typed actions, never translated labels or StringId as action identifiers.
- Persistent phase enums must serialize to the existing canonical version-1 strings. Do not change configuration, state, resource or update contracts during refactoring.
- Preserve .editorconfig formatting.

- Never call Win32 APIs or COM interfaces directly from application sources. Do not add P/Invoke declarations, native exports, COM declarations, dynamic COM activation or native function pointers. Use standard .NET APIs or maintained, Native AOT-compatible NuGet libraries. Startup shortcuts use SharpShellLink.
- Keep shared build defaults in Directory.Build.props. Store build intermediates and published binaries under the root tmp/ directory. Isolate project build outputs by project name. Portable release artifacts must be produced by dotnet publish without shell scripts.

- The release asset is a standalone Windows x64 ChrManager.exe; do not generate release archives or release metadata during build/publish. Do not commit binaries or archive fixtures.

- Publish the application with Native AOT in Debug and Release. Use source-generated JSON metadata. Do not generate user configuration; README contains the maintained English JSONC example. Missing settings waits for acknowledgement and exits with Environment.Exit(0). Debug native symbols are a separate PDB, never embedded.

- Exactly one manager may run per Windows user across portable paths. Hold the mutex on its dedicated owner thread; another launch exits without altering the running copy. Only verified self-update helpers run separately.

- Every settings.json property is required, including explicit nullable values and empty collections. Chrome++ is mandatory; no chromePlus.enabled switch. Before autostart changes, browser work or self-updates, download and validate the live upstream Chrome++ INI in memory. Missing/extra sections or keys, malformed reference or network failure block the pass and require Enter. Never cache the reference or fill omitted settings. Validate disabled browser entries too. keymapping accepts custom mappings. Ignore general.command_line, data_dir, cache_dir and managed_updates supplied by users with localized warnings; the manager supplies these values and keeps managed_updates=0.

- General settings are flat required root fields: exitDelaySeconds, startAtLogin, selfUpdateEnabled, httpTimeoutSeconds and uiLanguage (null means OS language, never "auto"). No proxy or localSource setting; use standard .NET proxy resolution and official Bush2021/chrome_plus releases. Derive presentation names and GitHub API User-Agent from application assembly metadata, not the entry assembly or worker filename. Keep IPC identity stable across versions. Self-update jobs record and validate the actual main executable filename, including renamed executables.
- Own failures use FailureKind and arguments, never descriptive string literals in thrown exceptions. Do not read Exception.Message for presentation or persistence; preserve the exception type and stack as technical context.
