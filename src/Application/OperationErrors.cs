using System.ComponentModel;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;

namespace ChrManager.Application;

internal static class OperationErrors
{
	public static bool IsExpected (Exception exception)
	{
		return exception is IOException or InvalidDataException or HttpRequestException
			or UnauthorizedAccessException or SecurityException or Win32Exception
			or TimeoutException or OperationCanceledException or JsonException
			or CryptographicException or ArgumentException or InvalidOperationException
			or NotSupportedException;
	}

	// Terminal error boundaries may report unexpected failures, but must not attempt
	// to recover from corrupted process state or resource exhaustion.
	public static bool CanReport (Exception exception)
	{
		return exception is not (OutOfMemoryException or StackOverflowException or AccessViolationException);
	}
}
