using System.Text.Json;
using System.Text.Json.Serialization;
using ChrManager.Application;

namespace ChrManager.Infrastructure.Configuration;

internal sealed class CanonicalEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
	private readonly Dictionary<string, T> _values = new(StringComparer.Ordinal);
	private readonly Dictionary<T, string> _names = new();
	public CanonicalEnumConverter ()
	{
		if (typeof(T) == typeof(InstallationPhase))
		{
			Add((T) (object) InstallationPhase.Prepared, "prepared");
			Add((T) (object) InstallationPhase.OldMoved, "old-moved");
			Add((T) (object) InstallationPhase.NewMoved, "new-moved");
			Add((T) (object) InstallationPhase.Committed, "committed");
		}
		else if (typeof(T) == typeof(SelfUpdatePhase))
		{
			Add((T) (object) SelfUpdatePhase.BackingUp, "backing-up");
			Add((T) (object) SelfUpdatePhase.Installing, "installing");
			Add((T) (object) SelfUpdatePhase.Starting, "starting");
			Add((T) (object) SelfUpdatePhase.Restoring, "restoring");
			Add((T) (object) SelfUpdatePhase.Complete, "complete");
			Add((T) (object) SelfUpdatePhase.Failed, "failed");
			Add((T) (object) SelfUpdatePhase.RolledBack, "rolled-back");
			Add((T) (object) SelfUpdatePhase.RecoveryFailed, "recovery-failed");
			Add((T) (object) SelfUpdatePhase.Aborted, "aborted");
		}
		else
		{
			throw new AppFailure(FailureKind.UnsupportedPhase);
		}
	}
	private void Add (T value, string name)
	{
		_values.Add(name, value);
		_names.Add(value, name);
	}

	public override T Read (ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.String)
		{
			throw new AppFailure(FailureKind.InvalidPhase);
		}

		if (!_values.TryGetValue(reader.GetString()!, out T value))
		{
			throw new AppFailure(FailureKind.InvalidPhase);
		}

		return value;
	}
	public override void Write (Utf8JsonWriter writer, T value, JsonSerializerOptions options)
	{
		if (!_names.TryGetValue(value, out string? name))
		{
			throw new AppFailure(FailureKind.InvalidPhase);
		}

		writer.WriteStringValue(name);
	}
}
