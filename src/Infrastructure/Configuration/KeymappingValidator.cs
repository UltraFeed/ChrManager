using System.Globalization;

namespace ChrManager.Infrastructure.Configuration;

internal static class KeymappingValidator
{
	private static readonly HashSet<string> SpecialKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		"Left", "Right", "Up", "Down",
		"←", "→", "↑", "↓",
		"Esc", "Escape",
		"Tab", "Backspace",
		"Enter", "Return", "Space",
		"PrtSc", "PrintScreen",
		"Scroll", "Pause",
		"Insert", "Delete", "Del",
		"Home", "End",
		"PageUp", "PgUp",
		"PageDown", "PgDn"
	};

	public static bool IsValidTarget (string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		if (value.StartsWith("command:", StringComparison.OrdinalIgnoreCase))
		{
			if (!value.StartsWith("command:", StringComparison.Ordinal))
			{
				return false;
			}

			return int.TryParse(value.AsSpan(8), NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id > 0;
		}

		return IsValidHotkey(value);
	}

	public static bool IsValidHotkey (string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
		{
			return false;
		}

		HashSet<string> modifiers = new(StringComparer.OrdinalIgnoreCase);

		bool hasKey = false;

		foreach (string part in value.Split('+'))
		{
			if (part.Length == 0 || part != part.Trim())
			{
				return false;
			}

			string? modifier = GetModifier(part);

			if (modifier is not null)
			{
				if (!modifiers.Add(modifier))
				{
					return false;
				}

				continue;
			}

			if (hasKey || !IsSupportedKey(part))
			{
				return false;
			}

			hasKey = true;
		}

		return hasKey;
	}

	private static string? GetModifier (string value)
	{
		return value.ToUpperInvariant() switch
		{
			"CTRL" or "CONTROL" => "ctrl",
			"ALT" => "alt",
			"SHIFT" => "shift",
			"WIN" => "win",
			_ => null
		};
	}

	private static bool IsSupportedKey (string value)
	{
		if (SpecialKeys.Contains(value))
		{
			return true;
		}

		if (value.Length >= 2 && (value [0] is 'F' or 'f') && int.TryParse(value.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number is >= 1 and <= 24)
		{
			return true;
		}

		if (value.Length != 1)
		{
			return false;
		}

		char key = value [0];

		if (key is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9'))
		{
			return true;
		}

		// Printable ASCII symbols are accepted syntactically.
		// Actual availability depends on the keyboard layout.
		// '+' is reserved as the combination separator.
		return key is >= '!' and <= '~' and not '+';
	}
}
