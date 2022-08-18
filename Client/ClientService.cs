using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using callback.CBFSFilter;
using Client.Drivers;
using CoreLib;
using CoreLib.Diagnostics;
using SharedLib.Configuration;
using Win32API.Headers;
using Win32API.Headers.Winbase.Structures;
using Win32API.Modules;

namespace Client;

public class ClientService : ServiceBase
{
	private class WTSInfo
	{
		public int Id { get; set; }

		public string StationName { get; set; }

		public string Username { get; set; }

		public WTS_CONNECTSTATE_CLASS State { get; set; }

		public override string ToString()
		{
			return $"Session id: {Id}, State: {State}, Username: {Username} Station name: {StationName}";
		}
	}

	private Cbprocess CB_PROCESS;

	private Cbfilter CB_FILTER;

	private const int PROCESS_FILTER_CALLBACK_TIMEOUT = 5000;

	private const int FILE_FILTER_CALLBACK_TIMEOUT = 5000;

	private const long FILE_FILTER_DEFAULT_CONTROL_FLAGS = 268697605L;

	private const int ERROR_ACCESS_DENIED = 5;

	private readonly OperatingSystem OS_VERSION = Environment.OSVersion;

	private readonly object CREATE_LOCK = new object();

	private const int DEFAULT_CLIENT_START_RETRIES = 50;

	private const int RETRY_WAIT_SPAN = 100;

	private const int NORMAL_PRIORITY_CLASS = 32;

	private const bool WRITE_DEBUG_MESSAGES = true;

	private const int SHUTDOWN_TIMER_PERIOD = 10000;

	private Timer _shutdownTimer;

	private readonly SemaphoreSlim _shutdownTimerCallbackLock = new SemaphoreSlim(1, 1);

	private int? ClientProcessId { get; set; }

	private uint? ClientSessionId { get; set; }

	private uint ServiceProcessId => EntryPoint.CURRENT_PROCESS_ID;

	private bool DisableCallBacks { get; set; }

	private bool WriteDebugMessages => true;

	public ClientService()
	{
		EventLog.Source = EntryPoint.PRCOESS_FILE_NAME_WITHOUT_EXTENSION;
		base.AutoLog = false;
		base.CanHandleSessionChangeEvent = true;
		base.CanPauseAndContinue = false;
		base.CanShutdown = true;
		base.CanStop = false;
	}

	protected override async void OnStart(string[] args)
	{
		try
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog($"Service is starting, service process id {ServiceProcessId}.", null, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 118);
			}
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("Adjusting service priveleges.", null, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 123);
			}
			CoreProcess.SetPrivelege(Kernel32.GetCurrentProcess(), "SeTcbPrivilege", SE.SE_PRIVILEGE_ENABLED);
			if (Kernel32.SetDllDirectory(EntryPoint.DEPENDENCY_FULL_PATH))
			{
				try
				{
					using (CBProcessHelper cBProcessHelper = new CBProcessHelper())
					{
						if (WriteDebugMessages)
						{
							EntryPoint.TryWriteToCacheLog("Executing Initialize", null, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 142);
						}
						cBProcessHelper.Initialize("{b222bb8c-d45b-4c2d-aac1-b5968f2fbec6}");
					}
					try
					{
						using CBFilterHelper cBFilterHelper = new CBFilterHelper();
						if (WriteDebugMessages)
						{
							EntryPoint.TryWriteToCacheLog("Executing Initialize", null, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 154);
						}
						cBFilterHelper.Initialize("{b222bb8c-d45b-4c2d-aac1-b5968f2fbec6}");
					}
					catch
					{
						throw;
					}
				}
				catch
				{
					throw;
				}
				finally
				{
					Kernel32.SetDllDirectory();
				}
			}
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("Starting process filter.", null, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 182);
			}
			if (CBHelperBase<Cbprocess>.IsInitialized)
			{
				CB_PROCESS = new Cbprocess
				{
					RuntimeLicense = "43464E4641444E585246323032323034313136474E393234353500000000000000000000000000004D555A394341575400005641314758484A34474D56310000"
				};
				CB_PROCESS.OnProcessTermination += OnProcessTermination;
				CB_PROCESS.OnProcessHandleOperation += OnProcessHandleOperation;
				CB_PROCESS.OnThreadHandleOperation += OnProcessThreadHandleOperation;
				CB_PROCESS.StartFilter(5000);
				CB_PROCESS.AddFilteredProcessById(-1, includeChildren: true);
			}
			ClientSettings.TryGetFileFilterSettings(out var disabled);
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("File filter is " + (disabled ? "Disabled" : "Enabled") + ".", null, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 207);
			}
			if (!disabled)
			{
				if (WriteDebugMessages)
				{
					EntryPoint.TryWriteToCacheLog("Starting file filter.", null, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 213);
				}
				if (CBHelperBase<Cbfilter>.IsInitialized)
				{
					try
					{
						CB_FILTER = new Cbfilter
						{
							RuntimeLicense = "43464E4641444E585246323032323034313136474E393234353500000000000000000000000000004D555A394341575400005641314758484A34474D56310000"
						};
						CB_FILTER.OnBeforeCreateFile += OnBeforeCreateFile;
						CB_FILTER.OnBeforeOpenFile += OnBeforeOpenFile;
						CB_FILTER.OnBeforeRenameOrMoveFile += OnBeforeRenameOrMoveFile;
						CB_FILTER.OnBeforeSetFileSecurity += OnBeforeSetFileSecurity;
						CB_FILTER.StartFilter(5000);
						string text = EntryPoint.PROCESS_DIRECTORY + Path.DirectorySeparatorChar + "*";
						if (!CB_FILTER.AddFilterRule(text, 0, 268697605L, 0L) && WriteDebugMessages)
						{
							EntryPoint.TryWriteToCacheLog("Failed to add filter for path " + text + ".", null, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 240);
						}
					}
					catch (CBFSFilterException)
					{
					}
					catch (Exception)
					{
					}
				}
			}
			uint num = Kernel32.WTSGetActiveConsoleSessionId();
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog($"Active console session id {num}.", null, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 261);
			}
			if (num == uint.MaxValue)
			{
				return;
			}
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("Current client process id " + (ClientProcessId?.ToString() ?? "Null") + ".", null, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 268);
			}
			if (ClientProcessId.HasValue)
			{
				Monitor.Enter(CREATE_LOCK);
				try
				{
					if (ClientProcessId.HasValue)
					{
						Process.GetProcessById(ClientProcessId.Value);
					}
				}
				catch (ArgumentException)
				{
					if (WriteDebugMessages)
					{
						EntryPoint.TryWriteToCacheLog("Client process has exited, process id " + (ClientProcessId?.ToString() ?? "Null") + ".", null, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 283);
					}
					ClientProcessId = null;
				}
				finally
				{
					Monitor.Exit(CREATE_LOCK);
				}
			}
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("Starting client process.", null, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 296);
			}
			await CreateProcessAsync();
		}
		catch (Exception ex4)
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("Error.", ex4.Message, "OnStart", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 303);
			}
			throw;
		}
	}

	protected override void OnCustomCommand(int command)
	{
		try
		{
			if (command == 233)
			{
				Stop();
			}
		}
		catch (Exception ex)
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("Error.", ex.Message, "OnCustomCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 323);
			}
		}
	}

	protected override void OnStop()
	{
		if (WriteDebugMessages)
		{
			EntryPoint.TryWriteToCacheLog("Service is stopping.", null, "OnStop", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 330);
		}
		DisableCallBacks = true;
		try
		{
			StopShutdownTimer();
		}
		catch (Exception ex)
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("StopShutdownTimer Error.", ex.Message, "OnStop", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 341);
			}
		}
		try
		{
			CB_PROCESS?.StopFilter();
			CB_PROCESS?.Dispose();
		}
		catch (Exception ex2)
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("Error.", ex2.Message, "OnStop", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 354);
			}
		}
	}

	protected override async void OnSessionChange(SessionChangeDescription changeDescription)
	{
		try
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog($"Session id: {changeDescription.SessionId} reason: {changeDescription.Reason}", null, "OnSessionChange", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 363);
			}
			SessionChangeReason reason = changeDescription.Reason;
			if (reason == SessionChangeReason.ConsoleConnect || reason == SessionChangeReason.SessionLogon)
			{
				await CreateProcessAsync();
			}
		}
		catch (Exception ex)
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("Error.", ex.Message, "OnSessionChange", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 380);
			}
		}
	}

	protected override void OnShutdown()
	{
		DisableCallBacks = true;
		if (WriteDebugMessages)
		{
			EntryPoint.TryWriteToCacheLog("Service is shutting down.", null, "OnShutdown", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 389);
		}
	}

	private void CreateProcess(int maxRetries = 50)
	{
		if (!Monitor.TryEnter(CREATE_LOCK))
		{
			return;
		}
		if (WriteDebugMessages)
		{
			EntryPoint.TryWriteToCacheLog("Creating client process.", null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 401);
		}
		uint? num = 0u;
		try
		{
			if (ClientProcessId.HasValue)
			{
				if (WriteDebugMessages)
				{
					EntryPoint.TryWriteToCacheLog($"Client process is already tracked, client process id : {ClientProcessId}.", null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 411);
				}
				return;
			}
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("ListSessions Listing sessions.", null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 417);
			}
			WTSInfo wTSInfo = (from SESSION in ListSessions()
				where SESSION.State == WTS_CONNECTSTATE_CLASS.WTSActive
				select SESSION).FirstOrDefault();
			if (wTSInfo == null)
			{
				if (WriteDebugMessages)
				{
					EntryPoint.TryWriteToCacheLog("No active console session found.", null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 426);
				}
				return;
			}
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog($"Found active console session {wTSInfo}.", null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 433);
			}
			num = (uint)wTSInfo.Id;
			IntPtr phToken = IntPtr.Zero;
			IntPtr lpEnvironment = IntPtr.Zero;
			IntPtr intPtr = IntPtr.Zero;
			try
			{
				try
				{
					if (WriteDebugMessages)
					{
						EntryPoint.TryWriteToCacheLog("Executing WTSQueryUserToken.", null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 447);
					}
					if (!Wtsapi32.WTSQueryUserToken(num.Value, out phToken))
					{
						throw new Win32Exception();
					}
				}
				catch (Win32Exception ex)
				{
					if (WriteDebugMessages)
					{
						EntryPoint.TryWriteToCacheLog(string.Format("{0} Console session id {1} error {2} , native error {3}", "WTSQueryUserToken", num, ex.ErrorCode, ex.NativeErrorCode), null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 455);
					}
					throw;
				}
				try
				{
					if (WriteDebugMessages)
					{
						EntryPoint.TryWriteToCacheLog("Executing CreateEnvironmentBlock.", null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 463);
					}
					if (!Userenv.CreateEnvironmentBlock(out lpEnvironment, phToken, bInherit: true))
					{
						throw new Win32Exception();
					}
				}
				catch (Win32Exception ex2)
				{
					if (WriteDebugMessages)
					{
						EntryPoint.TryWriteToCacheLog(string.Format("{0} Error {1} , native error {2}", "CreateEnvironmentBlock", ex2.ErrorCode, ex2.NativeErrorCode), null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 471);
					}
					throw;
				}
				try
				{
					if (WriteDebugMessages)
					{
						EntryPoint.TryWriteToCacheLog("Executing GetLinkedTokeIfRequiered.", null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 479);
					}
					intPtr = CoreProcess.GetLinkedTokeIfRequiered(phToken);
				}
				catch (Win32Exception ex3)
				{
					if (WriteDebugMessages)
					{
						EntryPoint.TryWriteToCacheLog(string.Format("{0} Error {1} , native error {2}", "GetLinkedTokeIfRequiered", ex3.ErrorCode, ex3.NativeErrorCode), null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 486);
					}
					throw;
				}
				string pROCESS_FULL_FILE_NAME = EntryPoint.PROCESS_FULL_FILE_NAME;
				string pROCESS_DIRECTORY = EntryPoint.PROCESS_DIRECTORY;
				StringBuilder strCommandLine = new StringBuilder(Environment.GetCommandLineArgs().Skip(1).DefaultIfEmpty()
					.Aggregate((string first, string next) => " " + first + " " + next));
				STARTUPINFO lpStartupInfo = default(STARTUPINFO);
				lpStartupInfo.cb = Marshal.SizeOf(lpStartupInfo);
				lpStartupInfo.lpDesktop = "winsta0\\default";
				PROCESS_INFORMATION lpProcessInformation = default(PROCESS_INFORMATION);
				uint dwCreationFlags = 1072u;
				try
				{
					if (WriteDebugMessages)
					{
						EntryPoint.TryWriteToCacheLog("Executing CreateProcessAsUser.", null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 516);
					}
					if (!AdvApi32.CreateProcessAsUser(intPtr, pROCESS_FULL_FILE_NAME, strCommandLine, IntPtr.Zero, IntPtr.Zero, bInheritHandles: true, dwCreationFlags, lpEnvironment, pROCESS_DIRECTORY, ref lpStartupInfo, out lpProcessInformation))
					{
						throw new Win32Exception();
					}
					if (lpProcessInformation.hThread != IntPtr.Zero)
					{
						ClientProcessId = lpProcessInformation.dwProcessId;
						ClientSessionId = num;
						if (WriteDebugMessages)
						{
							EntryPoint.TryWriteToCacheLog(string.Format("{0} Created porocess {1} in session {2}.", "CreateProcessAsUser", ClientProcessId, num), null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 537);
						}
					}
				}
				catch (Win32Exception ex4)
				{
					if (WriteDebugMessages)
					{
						EntryPoint.TryWriteToCacheLog(string.Format("{0} Error {1} , native error {2}", "CreateProcessAsUser", ex4.ErrorCode, ex4.NativeErrorCode), null, "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 543);
					}
					throw;
				}
			}
			catch (Win32Exception ex5)
			{
				int nativeErrorCode = ex5.NativeErrorCode;
				if (nativeErrorCode == 5 || nativeErrorCode == 995 || nativeErrorCode == 1008)
				{
					maxRetries--;
					if (maxRetries <= 0)
					{
						throw;
					}
					Thread.Sleep(100);
					if (!DisableCallBacks)
					{
						CreateProcess(maxRetries);
					}
					return;
				}
				throw;
			}
			catch
			{
				throw;
			}
			finally
			{
				Userenv.DestroyEnvironmentBlock(lpEnvironment);
				Kernel32.CloseHandle(phToken);
				if (phToken != intPtr)
				{
					Kernel32.CloseHandle(intPtr);
				}
			}
		}
		catch (Exception ex6)
		{
			if (WriteDebugMessages)
			{
				int num2 = 50 - maxRetries;
				EntryPoint.TryWriteToCacheLog(string.Format("Failed after {0} retries, console seesion id {1}.", num2, num.HasValue ? num.ToString() : "Unobtained"), ex6.ToString(), "CreateProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 593);
			}
		}
		finally
		{
			Monitor.Exit(CREATE_LOCK);
		}
	}

	private async Task CreateProcessAsync()
	{
		await Task.Run(delegate
		{
			CreateProcess();
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	private IEnumerable<WTSInfo> ListSessions()
	{
		IntPtr SERVER_HANDLE = Wtsapi32.WTSOpenServer(Environment.MachineName);
		if (SERVER_HANDLE == IntPtr.Zero)
		{
			throw new Win32Exception();
		}
		try
		{
			IntPtr ppSessionInfo = IntPtr.Zero;
			int SESSION_COUNT = 0;
			if (Wtsapi32.WTSEnumerateSessions(SERVER_HANDLE, 0, 1, ref ppSessionInfo, ref SESSION_COUNT) == 0)
			{
				throw new Win32Exception();
			}
			int DATA_SIZE = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
			IntPtr CURRENT_POSITION = ppSessionInfo;
			for (int SESSION_NUMBER = 0; SESSION_NUMBER < SESSION_COUNT; SESSION_NUMBER++)
			{
				WTS_SESSION_INFO wTS_SESSION_INFO = Marshal.PtrToStructure<WTS_SESSION_INFO>(CURRENT_POSITION);
				CURRENT_POSITION += DATA_SIZE;
				string username = null;
				if (Wtsapi32.WTSQuerySessionInformation(SERVER_HANDLE, wTS_SESSION_INFO.SessionID, Wtsapi32._WTS_INFO_CLASS.WTSUserName, out var ppBuffer, out var _))
				{
					username = Marshal.PtrToStringAuto(ppBuffer);
					Wtsapi32.WTSFreeMemory(ppBuffer);
				}
				yield return new WTSInfo
				{
					Id = wTS_SESSION_INFO.SessionID,
					State = wTS_SESSION_INFO.State,
					StationName = wTS_SESSION_INFO.pWinStationName,
					Username = username
				};
			}
			Wtsapi32.WTSFreeMemory(ppSessionInfo);
		}
		finally
		{
			Wtsapi32.WTSCloseServer(SERVER_HANDLE);
		}
	}

	private bool IsCriticalProcess(int processId)
	{
		if (processId <= 4)
		{
			return true;
		}
		try
		{
			string processName = CB_PROCESS.GetProcessName(processId);
			if (!string.IsNullOrWhiteSpace(processName))
			{
				return Path.GetFileName(processName).Compare("csrss.exe", ignoreCase: true);
			}
		}
		catch (Exception arg)
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog($"Critical process check failed for process id {processId}, exception : {arg}", null, "IsCriticalProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 686);
			}
		}
		return false;
	}

	private bool IsProcessOperationAllowed(int processId, int originatorProcessId, int accessFlag, bool process = true)
	{
		if (DisableCallBacks)
		{
			return true;
		}
		if (originatorProcessId == ServiceProcessId)
		{
			return true;
		}
		if (originatorProcessId == ClientProcessId)
		{
			return true;
		}
		if (processId != ClientProcessId && processId != ServiceProcessId)
		{
			return true;
		}
		if (IsCriticalProcess(originatorProcessId))
		{
			return true;
		}
		if (process)
		{
			if ((accessFlag & 1) != 1 && (accessFlag & 0x800) != 2048 && (accessFlag & 0x1000) != 4096 && (accessFlag & 2) != 2 && (accessFlag & 0x40) != 64)
			{
				return true;
			}
		}
		else if ((accessFlag & 1) != 1 && (accessFlag & 2) != 2 && (accessFlag & 8) != 8 && (accessFlag & 0x40) != 64)
		{
			return true;
		}
		return false;
	}

	private bool IsProcessFileOperationAllowed(int originatorProcessId)
	{
		if (DisableCallBacks)
		{
			return true;
		}
		if (originatorProcessId == ServiceProcessId)
		{
			return true;
		}
		if (originatorProcessId == ClientProcessId)
		{
			return true;
		}
		if (IsCriticalProcess(originatorProcessId))
		{
			return true;
		}
		return false;
	}

	private bool IsAllowedFile(string fullFileName)
	{
		if (string.IsNullOrWhiteSpace(fullFileName))
		{
			return true;
		}
		try
		{
			string fileName = Path.GetFileName(fullFileName);
			if (fileName.Compare("cachelog.txt", ignoreCase: true) || fileName.Compare("desktop.ini", ignoreCase: true))
			{
				return true;
			}
		}
		catch (Exception arg)
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog(string.Format("{0} error {1}, file name {2}", "IsAllowedFile", arg, fullFileName), null, "IsAllowedFile", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 793);
			}
			return true;
		}
		return false;
	}

	private bool IsDirectory(int attributes)
	{
		return (attributes & 0x10) == 16;
	}

	private void StartShutdownTimer()
	{
		if (WriteDebugMessages)
		{
			EntryPoint.TryWriteToCacheLog("Starting shutdown timer.", null, "StartShutdownTimer", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 818);
		}
		if (_shutdownTimer == null)
		{
			_shutdownTimer = new Timer(OnShutdownTimerCallback, null, 10000, 10000);
		}
		_shutdownTimer.Change(10000, 10000);
	}

	private void StopShutdownTimer()
	{
		if (WriteDebugMessages)
		{
			EntryPoint.TryWriteToCacheLog("Stopping shutdown timer.", null, "StopShutdownTimer", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 830);
		}
		_shutdownTimer?.Dispose();
		_shutdownTimer = null;
	}

	private async void OnProcessTermination(object sender, CbprocessProcessTerminationEventArgs e)
	{
		if (DisableCallBacks || ClientProcessId != e.ProcessId)
		{
			return;
		}
		ClientProcessId = null;
		ClientSessionId = null;
		IntPtr hProcess = Kernel32.OpenProcess(PROCESS_SECURITY.PROCESS_QUERY_INFORMATION, bInheritHandle: false, e.ProcessId);
		try
		{
			if (Kernel32.GetExitCodeProcess(hProcess, out var ExitCode))
			{
				ProcessExitCode processExitCode = (ProcessExitCode)ExitCode;
				if (WriteDebugMessages)
				{
					EntryPoint.TryWriteToCacheLog($"Client process exited {e.ProcessId} with exit code {ExitCode}.", null, "OnProcessTermination", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 866);
				}
				switch (processExitCode)
				{
				case (ProcessExitCode)(-1073741819):
				case (ProcessExitCode)(-1073741510):
				case (ProcessExitCode)(-1073740771):
				case ProcessExitCode.SystemPowerEvent:
				case ProcessExitCode.SessionEnded:
				case (ProcessExitCode)1073807364:
					StartShutdownTimer();
					break;
				case (ProcessExitCode)(-1073741502):
					Process.Start(new ProcessStartInfo
					{
						FileName = EntryPoint.PROCESS_FULL_FILE_NAME,
						Arguments = "-restart-service",
						UseShellExecute = false
					});
					break;
				case ProcessExitCode.AnotherInstanceRunning:
				case ProcessExitCode.Update:
				case ProcessExitCode.ShutDown:
					DisableCallBacks = true;
					await Task.Run(delegate
					{
						Stop();
					}).ConfigureAwait(continueOnCapturedContext: false);
					break;
				default:
					await CreateProcessAsync().ConfigureAwait(continueOnCapturedContext: false);
					break;
				}
			}
			else if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog($"Client process exited {e.ProcessId} with unknown exit code.", null, "OnProcessTermination", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 914);
			}
		}
		catch (Exception ex)
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("Error " + ex.Message + ".", null, "OnProcessTermination", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 920);
			}
		}
		finally
		{
			Kernel32.CloseHandle(hProcess);
		}
	}

	private void OnProcessHandleOperation(object sender, CbprocessProcessHandleOperationEventArgs e)
	{
		if (!IsProcessOperationAllowed(e.ProcessId, e.OriginatorProcessId, e.OriginalDesiredAccess))
		{
			e.DesiredAccess &= -2;
			e.DesiredAccess &= -2049;
			e.DesiredAccess &= -4097;
			e.DesiredAccess &= -65;
			e.DesiredAccess &= -3;
		}
	}

	private void OnProcessThreadHandleOperation(object sender, CbprocessThreadHandleOperationEventArgs e)
	{
		if (!IsProcessOperationAllowed(e.ProcessId, e.OriginatorProcessId, e.OriginalDesiredAccess, process: false))
		{
			e.DesiredAccess &= -2;
			e.DesiredAccess &= -3;
			e.DesiredAccess &= -9;
			e.DesiredAccess &= -65;
		}
	}

	private void OnBeforeCreateFile(object sender, CbfilterBeforeCreateFileEventArgs e)
	{
		int originatorProcessId = CB_FILTER.GetOriginatorProcessId();
		if (!IsProcessFileOperationAllowed(originatorProcessId) && (IsDirectory(e.Attributes) || !IsAllowedFile(e.FileName)))
		{
			FILE_ACCESS_RIGHTS desiredAccess = (FILE_ACCESS_RIGHTS)e.DesiredAccess;
			FILE_CREATION_FLAGS options = (FILE_CREATION_FLAGS)e.Options;
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog($"Create file request {e.FileName} File attributes {e.Attributes} Desired access rights {desiredAccess} Open flags {options} Originator process id {originatorProcessId}", null, "OnBeforeCreateFile", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 995);
			}
			e.ProcessRequest = false;
			e.ResultCode = 5;
		}
	}

	private void OnBeforeOpenFile(object sender, CbfilterBeforeOpenFileEventArgs e)
	{
		int originatorProcessId = CB_FILTER.GetOriginatorProcessId();
		if (IsProcessFileOperationAllowed(originatorProcessId) || IsAllowedFile(e.FileName))
		{
			return;
		}
		FILE_ACCESS_RIGHTS desiredAccess = (FILE_ACCESS_RIGHTS)e.DesiredAccess;
		FILE_CREATION_FLAGS options = (FILE_CREATION_FLAGS)e.Options;
		if (desiredAccess.HasFlag(FILE_ACCESS_RIGHTS.FILE_WRITE_DATA) || desiredAccess.HasFlag(FILE_ACCESS_RIGHTS.FILE_WRITE_EA) || desiredAccess.HasFlag(FILE_ACCESS_RIGHTS.FILE_WRITE_ATTRIBUTES) || desiredAccess.HasFlag(FILE_ACCESS_RIGHTS.DELETE) || options.HasFlag(FILE_CREATION_FLAGS.CREATE_ALWAYS) || options.HasFlag(FILE_CREATION_FLAGS.TRUNCATE_EXISTING) || options.HasFlag(FILE_CREATION_FLAGS.FILE_FLAG_DELETE_ON_CLOSE))
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog($"Open file request {e.FileName} File attributes {e.Attributes} Desired access rights {desiredAccess} Open flags {options} Originator process id {originatorProcessId}", null, "OnBeforeOpenFile", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 1040);
			}
			e.ProcessRequest = false;
			e.ResultCode = 5;
		}
	}

	private void OnBeforeRenameOrMoveFile(object sender, CbfilterBeforeRenameOrMoveFileEventArgs e)
	{
		int originatorProcessId = CB_FILTER.GetOriginatorProcessId();
		if (!IsProcessFileOperationAllowed(originatorProcessId) && !IsAllowedFile(e.FileName))
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog($"RenameOrMove file request {e.FileName} Originator process id {originatorProcessId}", null, "OnBeforeRenameOrMoveFile", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 1065);
			}
			e.ProcessRequest = false;
			e.ResultCode = 5;
		}
	}

	private void OnBeforeSetFileSecurity(object sender, CbfilterBeforeSetFileSecurityEventArgs e)
	{
		int originatorProcessId = CB_FILTER.GetOriginatorProcessId();
		if (!IsProcessFileOperationAllowed(originatorProcessId))
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog($"Set file security request {e.FileName} Originator process id {originatorProcessId}", null, "OnBeforeSetFileSecurity", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 1085);
			}
			e.ProcessRequest = false;
			e.ResultCode = 5;
		}
	}

	private async void OnShutdownTimerCallback(object state)
	{
		if (!(await _shutdownTimerCallbackLock.WaitAsync(TimeSpan.Zero)))
		{
			return;
		}
		try
		{
			await CreateProcessAsync();
		}
		catch (Exception ex)
		{
			if (WriteDebugMessages)
			{
				EntryPoint.TryWriteToCacheLog("Starting shutdown timer error " + ex.Message + ".", null, "OnShutdownTimerCallback", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Service\\ClientService.cs", 1107);
			}
		}
		finally
		{
			StopShutdownTimer();
			_shutdownTimerCallbackLock.Release();
		}
	}
}
