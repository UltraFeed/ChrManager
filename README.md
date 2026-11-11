<div align="center">

![CERTIFIED AI SLOP](https://img.shields.io/badge/CERTIFIED-AI_SLOP-ff00ff?style=for-the-badge&logo=probot&logoColor=white)

[![CodeFactor](https://www.codefactor.io/repository/github/ultrafeed/chrmanager/badge?style=for-the-badge)](https://www.codefactor.io/repository/github/ultrafeed/chrmanager)

# ChrManager

[![Build Status](https://img.shields.io/github/actions/workflow/status/UltraFeed/ChrManager/ci.yml.svg?branch=master&label=Build&logo=github&style=for-the-badge)](https://github.com/UltraFeed/ChrManager/actions/workflows/ci.yml)
[![Last commit date](https://img.shields.io/github/last-commit/UltraFeed/ChrManager.svg?label=Updated&logo=github&style=for-the-badge)](https://github.com/UltraFeed/ChrManager/commits/master)
[![Total downloads](https://img.shields.io/github/downloads/UltraFeed/ChrManager/total.svg?label=Downloads&logo=github&style=for-the-badge)](https://github.com/UltraFeed/ChrManager/releases)

[![GitHub stable release version](https://img.shields.io/github/v/release/UltraFeed/ChrManager.svg?label=Stable&logo=github&style=for-the-badge)](https://github.com/UltraFeed/ChrManager/releases/latest)
[![GitHub stable release date](https://img.shields.io/github/release-date/UltraFeed/ChrManager.svg?display_date=published_at&label=Released&logo=github&style=for-the-badge)](https://github.com/UltraFeed/ChrManager/releases/latest)

[![Repobeats analytics image](https://repobeats.axiom.co/api/embed/e2d7cdfd54253a49b44a67d32cbb1784514f7e87.svg)](https://github.com/UltraFeed/ChrManager/pulse)

</div>

---

> [!NOTE]
> Portable updater for portable chromium builds on Windows x64.
> Inspired by [chrlauncher](https://github.com/henrypp/chrlauncher), but developed from scratch to suit my own requirements and preferences.
> Only one manager runs per Windows user, regardless of the portable directory.
> Autostart uses a shortcut in shell:startup. Browser registration is handled by the browser itself. Choose default apps in Windows Settings.

## Configuration

Create settings.json beside the executable using this example.

```jsonc
{
	// Delay after a successful pass, in seconds; range: -1..86400.
	// -1 waits for Enter; 0 exits immediately. Errors always require acknowledgement.
	"exitDelaySeconds": 10,
	// Maintain a shortcut in the current user's Windows Startup folder.
	// Launch manually after moving the portable folder to refresh an enabled shortcut.
	// Shortcut targets support up to 259 UTF-16 characters, including the EXE name.
	"startAtLogin": false,
	// Apply verified stable manager updates from GitHub Releases.
	"selfUpdateEnabled": true,
	// HTTP attempt/connect timeout in seconds; range: 1..300. Metadata requests have
	// bounded retries and a total deadline. Package transfer duration is not limited.
	// The standard .NET system/environment proxy configuration is used automatically.
	"httpTimeoutSeconds": 30,
	// null selects Russian for a Russian Windows UI culture, English otherwise.
	// Explicit tags: "ru" or "en". Unknown nonempty tags warn and select English.
	// Empty strings and the old "auto" value are rejected.
	"uiLanguage": null,
	"browsers":
	[
		{
			// A unique Windows folder name relative to the executable, ignoring case.
			// No absolute paths, separators, '..' or reserved device names.
			// Renaming this selects another installation; old files/profiles are not moved.
			"folder": "chromium",
			// Exact ID from https://chromiumbuilds.org/api/v1/windows.json
			"buildId": "snapshot-win-x64",
			// Disabled entries are neither updated nor configured, but settings are validated.
			"enabled": true,
			// Persistent flags are applied by the mandatory Chrome++; [] adds no user flags.
			// --user-data-dir and --disk-cache-dir are managed and cannot be supplied here.
			"flags":
			[
				"--no-default-browser-check",
				"--disable-domain-reliability",
				"--disable-logging",
				"--no-pings",
				"--metrics-recording-only"
			],
			"chromePlus":
			{
				// Chrome++ is mandatory. Only compatible browser builds can load its DLL;
				// upstream officially supports latest stable Google Chrome. No EXE patching.
				// The matching stable DLL is always downloaded from Bush2021/chrome_plus.
				// All upstream sections and non-managed keys must be explicitly present.
				// Values are INI strings. Switches: "0" off, "1" on (some also accept "2").
				// general.command_line, data_dir, cache_dir and managed_updates are managed:
				// omit them; supplying them warns and ignores the supplied values.
				"settings":
				{
					"general":
					{
						// Empty strings disable programs executed at browser startup/exit.
						"launch_on_startup": "",
						"launch_on_exit": "",
						// Empty strings disable the hide-window and page-translation hotkeys.
						"boss_key": "",
						"translate_key": "",
						// Leave the win32k workaround disabled unless troubleshooting Chrome++.
						"win32k": "0",
						// Keep normal enterprise-policy handling and upgrade notifications.
						"ignore_policies": "0",
						"suppress_false_upgrade_notification": "0"
					},
					"tabs":
					{
						// Disable double-click/right-click tab closing and keeping the last tab.
						"double_click_close": "0",
						"right_click_close": "0",
						"keep_last_tab": "0",
						// Disable both forms of wheel-based tab switching.
						"wheel_tab": "0",
						"wheel_tab_when_press_rbutton": "0",
						// Do not redirect address-bar entries or bookmarks into new tabs.
						"open_url_new_tab": "0",
						"open_bookmark_new_tab": "0",
						// New-tab exceptions are inactive when the preceding features are off.
						"new_tab_disable": "0",
						"new_tab_disable_name": "",
						// Disable hover activation; its explicit delay is milliseconds (0..5000).
						"hover_tab": "0",
						"hover_tab_delay": "400"
					},
					// Chrome++ keyboard shortcut remapping.
					// Format: "SourceKey": "TargetKey" or "SourceKey": "command:ID".
					// Combine keys with '+', without spaces.
					// Supported modifiers: Ctrl, Alt, Shift, Win.
					// Supported keys include A-Z, 0-9, F1-F24, arrows,
					// navigation keys, and other common keyboard keys.
					// Command IDs refer to Chromium's chrome_command_ids
					// https://github.com/chromium/chromium/blob/main/chrome/app/chrome_command_ids.h
					// The "command:" prefix must be lowercase.
					// Existing browser shortcuts can be overridden.
					// Invalid key combinations may be ignored by Chrome++.
					// Use {} to disable custom mappings.
					"keymapping":
					{
						// "F2": "Ctrl+PageUp",
						// "F3": "Ctrl+PageDown",
						// "Alt+Shift+S": "command:40015"
					}
				}
			}
		}
	]
}
```

## Files

```text
ChrManager.exe
settings.json
chromium/
├── bin/
│   ├── chrome.exe
│   ├── version.dll
│   ├── chrome++.ini
│   ├── {browserVersion}/
│   │   └── ...                  # Version-specific browser files
│   └── ...                      # Other Chromium binaries and resources
├── profile/                     # Browser user data
└── cache/                       # Browser cache
```
