using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using ChrManager.Application;
using ChrManager.Infrastructure.Startup;

namespace ChrManager.Infrastructure.Ipc;

// Mutex ownership is thread-affine: this worker acquires and releases on the same thread.
internal sealed class UserInstanceLease : IDisposable
{
	private readonly Mutex _mutex;
	private readonly ManualResetEventSlim _ready = new();
	private readonly ManualResetEventSlim _release = new();
	private readonly Thread _thread;
	private Exception? _failure;
	public bool IsOwner { get; private set; }
	public static string UserKey { get; } = ReadUserKey();
	internal static string MutexName => "Global\\" + InternalProtocol.InstanceIdentity + "-" + UserKey;

	public UserInstanceLease ()
	{
		_mutex = new Mutex(false, MutexName);
		_thread = new Thread(Hold) { IsBackground = true, Name = AppInfo.Name + " instance lease" };
		_thread.Start();
		_ready.Wait();
		if (_failure is not null)
		{
			Dispose();
			throw new AppFailure(FailureKind.InstanceLeaseFailed, _failure);
		}
	}
	private static string ReadUserKey ()
	{
		using WindowsIdentity identity = WindowsIdentity.GetCurrent();
		string sid = identity.User?.Value ?? throw new AppFailure(FailureKind.WindowsUserUnavailable);
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sid))) [..24];
	}
	private void Hold ()
	{
		try
		{
			try
			{
				IsOwner = _mutex.WaitOne(0);
			}
			catch (AbandonedMutexException)
			{
				IsOwner = true;
			}
		}
		catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
		{
			_failure = ex;
		}
		finally
		{
			_ready.Set();
		}

		if (IsOwner)
		{
			_release.Wait();
			_mutex.ReleaseMutex();
		}
	}
	public void Dispose ()
	{
		_release.Set();
		_thread.Join();
		_mutex.Dispose();
		_ready.Dispose();
		_release.Dispose();
	}
}
