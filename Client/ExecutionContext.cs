using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Timers;
using Client.Exceptions;
using CoreLib;
using CoreLib.Diagnostics;
using CoreLib.Registry;
using CoreLib.Threading;
using CyClone;
using CyClone.Core;
using GizmoShell;
using IntegrationLib;
using SharedLib;
using SharedLib.Applications;
using SharedLib.Deployment;
using SharedLib.Dispatcher.Exceptions;
using SharedLib.Tasks;

namespace Client;

public class ExecutionContext : SharedLib.PropertyChangedBase, IExecutionContext
{
	private class DeploymentPaths
	{
		public string Source { get; set; }

		public string Destination { get; set; }
	}

	private sealed class EnvironmentVariableContext : IDisposable
	{
		private bool _isDisposed;

		private readonly ExecutionContext _executionContext;

		public EnvironmentVariableContext(ExecutionContext executionContext)
		{
			_executionContext = executionContext;
			_executionContext?.Application?.SetVariables();
			_executionContext?.Executable?.SetVariables();
			Environment.SetEnvironmentVariable("CUR_EXE_PATH", Environment.ExpandEnvironmentVariables(_executionContext?.Executable?.ExecutablePath ?? string.Empty));
			Environment.SetEnvironmentVariable("CUR_EXE_ARGUMENTS", Environment.ExpandEnvironmentVariables(_executionContext?.Executable?.Arguments ?? string.Empty));
			Environment.SetEnvironmentVariable("CUR_EXE_WORKING_DIRECTORY", Environment.ExpandEnvironmentVariables(_executionContext?.Executable?.WorkingDirectory ?? string.Empty));
		}

		public void Dispose()
		{
			if (!_isDisposed)
			{
				_executionContext?.Application?.UnsetVariables();
				_executionContext?.Executable?.UnsetVariables();
				Environment.SetEnvironmentVariable("CUR_EXE_PATH", string.Empty);
				Environment.SetEnvironmentVariable("CUR_EXE_ARGUMENTS", string.Empty);
				Environment.SetEnvironmentVariable("CUR_EXE_WORKING_DIRECTORY", string.Empty);
				_isDisposed = true;
			}
		}
	}

	private sealed class ExecutionContextProcessInfo
	{
		public int ProcessId { get; set; }

		public bool IsMain { get; set; }

		public string ProcessFileName { get; set; }
	}

	private readonly ConcurrentDictionary<int, ExecutionContextProcessInfo> _processes = new ConcurrentDictionary<int, ExecutionContextProcessInfo>();

	private Dictionary<ILicenseManagerPlugin, IApplicationLicense> plugins;

	private ContextExecutionState state;

	private GizmoClient client;

	private Executable executable;

	private IApplicationProfile application;

	private IAbortHandle abortHandle;

	private LicenseReservation licenseReservation;

	private System.Timers.Timer licenseReleaseTimer;

	private int waitFinalizeTime = 3000;

	private bool isExecuting;

	private bool isAborting;

	private bool hasCompleted;

	private bool isWaitingFinalization;

	private bool isFinalized = true;

	private bool isDestroyed;

	private bool isReleased;

	private bool autoLaunch;

	private bool haltOnError;

	private bool monitorChildren;

	private bool terminateChildren;

	private readonly object CONTEXT_OP_LOCK = new object();

	private readonly object FINALIZE_OP_LOCK = new object();

	private readonly object LICENSE_OP_LOCK = new object();

	private DateTime? startTime;

	private DateTime? endTime;

	IExecutable IExecutionContext.Executable => Executable;

	IApplicationProfile IExecutionContext.Profile => Application;

	IClient IExecutionContext.Client => Client;

	private ConcurrentDictionary<int, ExecutionContextProcessInfo> Processes => _processes;

	private IAbortHandle AbortHandle
	{
		get
		{
			if (abortHandle == null)
			{
				return new AbortHandle();
			}
			return abortHandle;
		}
		set
		{
			abortHandle = value;
		}
	}

	private LicenseReservation LicenseReservation
	{
		get
		{
			return licenseReservation;
		}
		set
		{
			licenseReservation = value;
		}
	}

	private System.Timers.Timer LicenseReleaseTimer
	{
		get
		{
			return licenseReleaseTimer;
		}
		set
		{
			licenseReleaseTimer = value;
		}
	}

	private Dictionary<ILicenseManagerPlugin, IApplicationLicense> Plugins
	{
		get
		{
			if (plugins == null)
			{
				plugins = new Dictionary<ILicenseManagerPlugin, IApplicationLicense>();
			}
			return plugins;
		}
	}

	private ManualResetEvent FinalizeWaitHandle { get; set; }

	private bool IsDestructionInitiated { get; set; }

	public bool AutoLaunch
	{
		get
		{
			return autoLaunch;
		}
		set
		{
			SetProperty(ref autoLaunch, value, "AutoLaunch");
		}
	}

	public int WaitFinalizeTime
	{
		get
		{
			return waitFinalizeTime;
		}
		set
		{
			if (value <= 0)
			{
				throw new ArgumentException("Ammount may not be less or equal to zero.", "WaitFinalizeTime");
			}
			SetProperty(ref waitFinalizeTime, value, "WaitFinalizeTime");
		}
	}

	public bool HaltOnError
	{
		get
		{
			return haltOnError;
		}
		set
		{
			SetProperty(ref haltOnError, value, "HaltOnError");
		}
	}

	public bool MonitorChildren
	{
		get
		{
			return monitorChildren;
		}
		protected set
		{
			SetProperty(ref monitorChildren, value, "MonitorChildren");
		}
	}

	public bool TerminateChildren
	{
		get
		{
			return terminateChildren;
		}
		set
		{
			SetProperty(ref terminateChildren, value, "TerminateChildren");
		}
	}

	public GizmoClient Client
	{
		get
		{
			return client;
		}
		protected set
		{
			SetProperty(ref client, value, "Client");
		}
	}

	public Executable Executable
	{
		get
		{
			return executable;
		}
		protected set
		{
			SetProperty(ref executable, value, "Executable");
		}
	}

	public IApplicationProfile Application
	{
		get
		{
			return application;
		}
		protected set
		{
			SetProperty(ref application, value, "Application");
		}
	}

	public ContextExecutionState State
	{
		get
		{
			return state;
		}
		protected set
		{
			SetProperty(ref state, value, "State");
		}
	}

	public bool IsAlive => Processes.Count > 0;

	public TimeSpan TotalExecutionSpan
	{
		get
		{
			if (!startTime.HasValue)
			{
				return TimeSpan.Zero;
			}
			DateTime value = startTime.Value;
			TimeSpan result = (endTime ?? DateTime.UtcNow).Subtract(value);
			if (!(result.TotalSeconds <= 0.0))
			{
				return result;
			}
			return TimeSpan.FromSeconds(0.0);
		}
	}

	public bool IsExecuting
	{
		get
		{
			return isExecuting;
		}
		protected set
		{
			SetProperty(ref isExecuting, value, "IsExecuting");
		}
	}

	public bool IsAborting
	{
		get
		{
			return isAborting;
		}
		protected set
		{
			SetProperty(ref isAborting, value, "IsAborting");
		}
	}

	public bool HasCompleted
	{
		get
		{
			return hasCompleted;
		}
		protected set
		{
			SetProperty(ref hasCompleted, value, "HasCompleted");
		}
	}

	public bool IsReady => (HasCompleted | IsExecuting) & (State != ContextExecutionState.Finalized) & (State != ContextExecutionState.Aborted) & (State != ContextExecutionState.Failed) & (State != ContextExecutionState.Destroyed);

	public bool IsWaitingFinalization
	{
		get
		{
			return isWaitingFinalization;
		}
		protected set
		{
			SetProperty(ref isWaitingFinalization, value, "IsWaitingFinalization");
		}
	}

	public bool IsFinalized
	{
		get
		{
			return isFinalized;
		}
		protected set
		{
			SetProperty(ref isFinalized, value, "IsFinalized");
		}
	}

	public bool IsReleased
	{
		get
		{
			return isReleased;
		}
		protected set
		{
			SetProperty(ref isReleased, value, "IsReleased");
		}
	}

	public bool IsDestroyed
	{
		get
		{
			return isDestroyed;
		}
		protected set
		{
			SetProperty(ref isDestroyed, value, "IsDestroyed");
		}
	}

	public bool IsUsable
	{
		get
		{
			if (!IsDestroyed)
			{
				return !IsReleased;
			}
			return false;
		}
	}

	public event EventHandler<ExecutionContextStateArgs> ExecutionStateChaged;

	internal ExecutionContext(Executable executable, IApplicationProfile application, GizmoClient client)
	{
		Executable = executable ?? throw new ArgumentNullException("executable", "Executable profile instance may not be null.");
		Application = application ?? throw new ArgumentNullException("application", "Application profile instance may not be null.");
		Client = client ?? throw new ArgumentNullException("client", "Client instance may not be null.");
		AutoLaunch = executable.AutoLaunch;
		MonitorChildren = executable.MonitorChildren;
		TerminateChildren = executable.KillChildren;
		HaltOnError = application.HaltOnError;
		Client.ProcessPostCreating += OnProcessPostCreating;
		Client.ProcessTerminated += OnProcessTerminated;
	}

	public void WriteMessage(string message)
	{
		Client.Log.AddInformation(message, LogCategories.Trace);
	}

	public void Trace(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
	{
		Client.TraceWrite(message, (string)null, memberName, sourceFilePath, sourceLineNumber);
	}

	public void Trace(Exception ex, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
	{
		Client.TraceWrite(ex, memberName, sourceFilePath, sourceLineNumber);
	}

	public void Trace(string message, Exception ex, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
	{
		Client.TraceWrite(message, ex, memberName, sourceFilePath, sourceLineNumber);
	}

	public void Execute(bool reprocess = false)
	{
		if (IsExecuting)
		{
			throw new ArgumentException("Already executing.");
		}
		Action<IAbortHandle, bool> action = OnExecution;
		AbortHandle abortHandle = new AbortHandle(action);
		abortHandle.AsyncResult = action.BeginInvoke(abortHandle, reprocess, OnExecutionCallBack, abortHandle);
		AbortHandle = abortHandle;
		IsExecuting = true;
	}

	public void Abort(bool async)
	{
		OnAbort(async);
	}

	public bool TryActivate()
	{
		ICollection<int> keys = Processes.Keys;
		int num = 0;
		foreach (int item in keys)
		{
			try
			{
				if (CoreProcess.SafeHasExited(item))
				{
					continue;
				}
				foreach (WindowInfo item2 in WindowEnumerator.ListVisibleWindows(item))
				{
					item2.SwitchTo(altTab: true);
					item2.Activate();
					num++;
				}
			}
			catch (Win32Exception ex)
			{
				Trace(ex, "TryActivate", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 532);
			}
		}
		return num > 0;
	}

	public void Kill()
	{
		Trace("Entering.", "Kill", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 549);
		lock (CONTEXT_OP_LOCK)
		{
			ICollection<int> keys = Processes.Keys;
			Trace($"Executing, total processes {keys.Count}.", "Kill", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 559);
			foreach (int item in keys)
			{
				try
				{
					Trace($"Trying to kill process, process id {item}.", "Kill", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 565);
					if (!CoreProcess.SafeHasExited(item))
					{
						CoreProcess.Kill(item);
						Trace($"Killed process, process id {item}.", "Kill", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 570);
					}
					else
					{
						Trace($"Process have exited, process id {item}.", "Kill", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 574);
					}
				}
				catch (Exception ex)
				{
					Trace($"Failed to kill process, process id {item}", ex, "Kill", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 582);
				}
			}
			Trace("Terminating tasks.", "Kill", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 590);
			Task[] array = Executable.Tasks.ToArray();
			foreach (Task task in array)
			{
				try
				{
					if (task.IsActive)
					{
						CoreProcess.Kill(task.InternalProcess.Id);
					}
				}
				catch (Exception ex2)
				{
					Trace(ex2, "Kill", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 604);
				}
			}
			Trace("Terminated tasks.", "Kill", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 608);
		}
		Trace("Exiting.", "Kill", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 613);
	}

	public bool AddProcessIfStarted(Process process, bool isMain)
	{
		if (process == null)
		{
			throw new ArgumentNullException("process");
		}
		string text = process.StartInfo?.FileName;
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new ArgumentNullException("processFileName", "Process file name not specified in Process.StartInfo.");
		}
		bool flag = false;
		ExecutionContextProcessInfo processInfo = null;
		lock (CONTEXT_OP_LOCK)
		{
			process.Start();
			int? num = null;
			try
			{
				num = process.Id;
			}
			catch (InvalidOperationException)
			{
			}
			if (num.HasValue && AddProcess(num.Value, text, isMain, raiseContextEvent: false, out processInfo))
			{
				flag = true;
			}
		}
		if (flag)
		{
			SetState(ContextExecutionState.ProcessCreated, processInfo);
		}
		return flag;
	}

	public void AddProcess(Process process, bool isMain)
	{
		if (process == null)
		{
			throw new ArgumentNullException("process");
		}
		if (process.Id == 0)
		{
			throw new ArgumentOutOfRangeException("Id", "Not started processes not allowed.");
		}
		string processFileName = null;
		if (!CoreProcess.SafeHasExited(process))
		{
			try
			{
				processFileName = process.MainModule?.FileName;
			}
			catch (Exception ex)
			{
				Trace("Process file name detection failed.", ex, "AddProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 701);
				try
				{
					processFileName = new CoreProcessModule(process.Id).FileName;
				}
				catch (Exception ex2)
				{
					Trace("Secondary process file name detection failed.", ex2, "AddProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 711);
				}
			}
		}
		AddProcess(process.Id, processFileName, isMain, raiseContextEvent: true, out var _);
	}

	private bool AddProcess(int processId, string processFileName, bool isMain, bool raiseContextEvent, out ExecutionContextProcessInfo processInfo)
	{
		processInfo = new ExecutionContextProcessInfo
		{
			IsMain = isMain,
			ProcessFileName = processFileName,
			ProcessId = processId
		};
		lock (CONTEXT_OP_LOCK)
		{
			if (!Processes.TryAdd(processId, processInfo))
			{
				Trace(string.Format("Process is already added, process id: {0}, process file name {1}", processId, processFileName ?? "Unknown"), "AddProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 729);
				processInfo = null;
				return false;
			}
			Trace(string.Format("Added process to context, process id: {0}, process file name {1}", processId, processFileName ?? "Unknown"), "AddProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 738);
			Trace($"New tracked process count (add) {Processes.Count}.", "AddProcess", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 739);
			DateTime valueOrDefault = startTime.GetValueOrDefault();
			if (!startTime.HasValue)
			{
				valueOrDefault = DateTime.UtcNow;
				startTime = valueOrDefault;
			}
			IsFinalized = false;
		}
		if (raiseContextEvent)
		{
			SetState(ContextExecutionState.ProcessCreated, processInfo);
		}
		return true;
	}

	public bool TryGetProcessFileName(int processId, out string processFileName)
	{
		processFileName = null;
		if (Processes.TryGetValue(processId, out var value) && !string.IsNullOrWhiteSpace(value?.ProcessFileName))
		{
			processFileName = value.ProcessFileName;
			return true;
		}
		return false;
	}

	public void Destroy()
	{
		Trace("Entering.", "Destroy", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 783);
		if (!IsDestroyed)
		{
			try
			{
				Trace("Executing.", "Destroy", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 790);
				Trace($"Executable id: {Executable.ID}", "Destroy", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 791);
				IsDestructionInitiated = true;
				Client.ProcessPostCreating -= OnProcessPostCreating;
				Client.ProcessTerminated -= OnProcessTerminated;
				try
				{
					if (IsExecuting)
					{
						Abort(async: false);
					}
				}
				catch (ArgumentException)
				{
				}
				Kill();
				OnFinalized();
			}
			catch (Exception ex2)
			{
				Trace(ex2, "Destroy", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 818);
			}
			finally
			{
				IsDestroyed = true;
				SetState(ContextExecutionState.Destroyed);
			}
		}
		else
		{
			Trace("Execution context is already destroyed.", "Destroy", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 828);
		}
		Trace("Exiting.", "Destroy", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 831);
	}

	public void Release()
	{
		Trace("Entering.", "Release", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 839);
		if (!IsReleased)
		{
			try
			{
				Trace("Executing.", "Release", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 845);
				Trace($"Executable id: {Executable.ID}", "Release", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 846);
				Client.ProcessPostCreating -= OnProcessPostCreating;
				Client.ProcessTerminated -= OnProcessTerminated;
				if (IsExecuting)
				{
					Abort(async: false);
				}
				OnFinalized();
			}
			catch (Exception ex)
			{
				Trace(ex, "Release", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 860);
			}
			finally
			{
				IsReleased = true;
				SetState(ContextExecutionState.Released);
			}
		}
		else
		{
			Trace("Execution context already released.", "Release", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 870);
		}
		Trace("Exiting", "Release", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 873);
	}

	public void Reset()
	{
		Trace("Entering.", "Reset", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 881);
		lock (CONTEXT_OP_LOCK)
		{
			Trace("Executing.", "Reset", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 885);
			Trace($"Executable id: {Executable.ID}", "Reset", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 886);
			startTime = null;
			endTime = null;
			Processes.Clear();
		}
		Trace("Exiting.", "Reset", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 896);
	}

	public Process CreateProcess()
	{
		Process processForExecutable = Executable.GetProcessForExecutable(Application);
		processForExecutable.StartInfo.ErrorDialogParentHandle = Client.ShellWindowHandle;
		processForExecutable.StartInfo.ErrorDialog = true;
		AddProcessIfStarted(processForExecutable, isMain: true);
		return processForExecutable;
	}

	public bool WaitFinalize(int time = -1)
	{
		if (IsWaitingFinalization)
		{
			return FinalizeWaitHandle?.WaitOne(time) ?? false;
		}
		return false;
	}

	private void OnExecution(IAbortHandle abortHandle, bool reProcess)
	{
		if (reProcess)
		{
			HasCompleted = false;
			SetState(ContextExecutionState.Reprocessing);
		}
		else
		{
			SetState(ContextExecutionState.Processing);
		}
		WaitFinalize();
		bool isAlive = IsAlive;
		bool flag = false;
		_ = Client.Dispatcher;
		if (!abortHandle.IsAborted & !HasCompleted)
		{
			OnProcessTasks(ActivationType.PreDeploy, abortHandle);
		}
		if (!abortHandle.IsAborted)
		{
			lock (Executable.DeploymentProfiles)
			{
				Dictionary<DeploymentProfile, DeploymentPaths> dictionary = Executable.DeploymentProfiles.AsQueryable().ToDictionary((DeploymentProfile profile) => profile, delegate(DeploymentProfile profile)
				{
					DeploymentPaths deploymentPaths = new DeploymentPaths();
					string source = profile.Source;
					if (string.IsNullOrWhiteSpace(source))
					{
						throw new ArgumentException("Deployment profile " + profile.Name + " source is invalid.", "Source");
					}
					string destination2 = profile.Destination;
					if (string.IsNullOrWhiteSpace(destination2))
					{
						throw new ArgumentException("Deployment profile " + profile.Name + " destination is invalid.", "Destination");
					}
					source = source.Replace("%ENTRYPUBLISHER%", Application?.PublisherName ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
					source = source.Replace("%ENTRYDEVELOPER%", Application?.DeveloperName ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
					source = source.Replace("%ENTRYTITLE%", Application?.Title ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
					if (profile.DirectAccess)
					{
						source = Environment.ExpandEnvironmentVariables(source);
						source = Path.GetFullPath(source);
					}
					destination2 = destination2.Replace("%ENTRYPUBLISHER%", Application?.PublisherName ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
					destination2 = destination2.Replace("%ENTRYDEVELOPER%", Application?.DeveloperName ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
					destination2 = destination2.Replace("%ENTRYTITLE%", Application?.Title ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
					destination2 = Environment.ExpandEnvironmentVariables(destination2);
					destination2 = Path.GetFullPath(destination2);
					deploymentPaths.Source = source;
					deploymentPaths.Destination = destination2;
					return deploymentPaths;
				});
				List<string> list = dictionary.Values.Select((DeploymentPaths PATHS) => PATHS.Destination).ToList();
				foreach (HashSet<string> item in (from PUFLIST in Client.GetPersonalUserFilePathList()
					where Client.IsMarkedPersonalUserFile(PUFLIST.Key)
					select PUFLIST.Value).ToList())
				{
					list.AddRange(item);
				}
				list = list.Distinct().ToList();
				List<KeyValuePair<DeploymentProfile, DeploymentPaths>> list2 = dictionary.ToList();
				if (!reProcess)
				{
					list2 = dictionary.Where((KeyValuePair<DeploymentProfile, DeploymentPaths> a) => !Client.IsMarkedDeploymentProfile(a.Key.ID)).ToList();
				}
				foreach (KeyValuePair<DeploymentProfile, DeploymentPaths> item2 in list2)
				{
					DeploymentProfile key = item2.Key;
					try
					{
						if (abortHandle.IsAborted)
						{
							return;
						}
						string text = item2.Value.Source;
						string destination = item2.Value.Destination;
						bool directAccess = key.DirectAccess;
						if (!directAccess)
						{
							try
							{
								text = Client.ExpandRemoteVariable(text);
							}
							catch (OperationNotSupportedException)
							{
							}
						}
						IcyDirectoryInfo icyDirectoryInfo = (directAccess ? new cyDirectoryInfo(text) : new cyRemoteDirectoryInfo(text, Client.Dispatcher));
						if (!icyDirectoryInfo.Exists)
						{
							throw new DirectoryNotFoundException($"Deployment Profile source directory not found {text}");
						}
						cyDirectoryInfo cyDirectoryInfo = new cyDirectoryInfo(destination);
						if (!reProcess && key.RepairDeploy && cyDirectoryInfo.Exists && !cyDirectoryInfo.IsEmpty)
						{
							continue;
						}
						if (!cyDirectoryInfo.Exists)
						{
							cyDirectoryInfo.Create();
						}
						string text2 = key.IncludeFiles;
						string text3 = key.IncludeDirectories;
						string text4 = key.ExcludeFiles;
						string text5 = key.ExcludeDirectories;
						if (key.DirectAccess)
						{
							if (!string.IsNullOrWhiteSpace(text2))
							{
								text2 = Environment.ExpandEnvironmentVariables(text2);
							}
							if (!string.IsNullOrWhiteSpace(text4))
							{
								text4 = Environment.ExpandEnvironmentVariables(text4);
							}
							if (!string.IsNullOrWhiteSpace(text3))
							{
								text3 = Environment.ExpandEnvironmentVariables(text3);
							}
							if (!string.IsNullOrWhiteSpace(text5))
							{
								text5 = Environment.ExpandEnvironmentVariables(text5);
							}
						}
						else
						{
							try
							{
								if (!string.IsNullOrWhiteSpace(text2))
								{
									text2 = Client.ExpandRemoteVariable(text2);
								}
								if (!string.IsNullOrWhiteSpace(text4))
								{
									text4 = Client.ExpandRemoteVariable(text4);
								}
								if (!string.IsNullOrWhiteSpace(text3))
								{
									text3 = Client.ExpandRemoteVariable(text3);
								}
								if (!string.IsNullOrWhiteSpace(text5))
								{
									text5 = Client.ExpandRemoteVariable(text5);
								}
							}
							catch (OperationNotSupportedException)
							{
							}
						}
						string fileFilter = Client.BuildFilter(text2, text4);
						string directoryFilter = Client.BuildFilter(text3, text5);
						bool includeSubDirectories = key.IncludeSubDirectories;
						FileInfoLevel fileInfoLevel = key.ComparisonLevel;
						if (key.MirrorDestination)
						{
							fileInfoLevel ^= FileInfoLevel.Mirror;
						}
						cyStructure cyStructure = new cyStructure(icyDirectoryInfo)
						{
							InfoLevel = fileInfoLevel,
							FileFilter = fileFilter,
							DirectoryFilter = directoryFilter
						};
						SetState(ContextExecutionState.Validating, cyStructure);
						cyStructure.Get(includeSubDirectories, abortHandle);
						if (abortHandle.IsAborted)
						{
							return;
						}
						cyDiffList<int, FileInfoLevel> diffList = cyStructure.Compare(cyDirectoryInfo, fileInfoLevel, abortHandle);
						if (abortHandle.IsAborted)
						{
							return;
						}
						cyStructure cyStructure2 = cyStructure.FromDiffList(diffList);
						IEnumerable<IcyFileSystemInfo> enumerable = cyStructure2.Entries.Where((IcyFileSystemInfo a) => !a.Flags.HasFlag(FileInfoLevel.Missing));
						long num = 0L;
						foreach (IcyFileSystemInfo item3 in enumerable)
						{
							IcyFileInfo childFileInfo = cyDirectoryInfo.GetChildFileInfo(item3.RelativePath);
							num += childFileInfo.Length;
						}
						if (key.MirrorDestination)
						{
							foreach (IcyFileSystemInfo extra in cyStructure2.Extras)
							{
								try
								{
									extra.Delete();
								}
								catch (Exception ex3)
								{
									Client.Log.AddError($"Could not delete extra entry {extra} durring destination directory mirroring", ex3, LogCategories.FileSystem);
								}
							}
						}
						DriveInfo driveInfo = new DriveInfo(Path.GetPathRoot(destination));
						if (cyStructure2.TotalSize > 0)
						{
							SetState(ContextExecutionState.ChekingAvailableSpace);
							if (driveInfo.AvailableFreeSpace + num < cyStructure2.TotalSize)
							{
								if (Client.Settings.AllocateDiskSpace)
								{
									SetState(ContextExecutionState.MakingSpace);
									if (!Client.TryMakeFreeSpace(destination, cyStructure2.TotalSize, list, abortHandle))
									{
										string text6 = $"Disk space allocation for {key.Name} failed.";
										Client.Log.AddError(text6, null, LogCategories.FileSystem);
										ThrowExecutionFailed(text6, null, ignoreHalt: true);
									}
								}
								else
								{
									string text7 = $"Not enough disk space for {key.Name} (Disk Allocation Disabled).";
									Client.Log.AddError(text7, null, LogCategories.FileSystem);
									ThrowExecutionFailed(text7, null, ignoreHalt: true);
								}
							}
						}
						if (Client.Settings.AllocateDiskSpace && Client.Settings.DiskAllocation != 0)
						{
							long num2 = driveInfo.TotalSize / 100 * (long)Client.Settings.DiskAllocation;
							if (driveInfo.AvailableFreeSpace < num2)
							{
								SetState(ContextExecutionState.AllocatingSpace);
								if (!Client.TryMakeFreeSpace(destination, num2, list, abortHandle))
								{
									Client.Log.AddError("Extra disk space allocation failed.", null, LogCategories.FileSystem);
								}
							}
						}
						if ((cyStructure2.TotalEntries > 0) & !abortHandle.IsAborted)
						{
							cyStructureSync cyStructureSync = new cyStructureSync
							{
								ReadBufferSize = 262144,
								RetryCount = 5,
								RetryWait = 5000,
								ResumeOnFileErrors = true
							};
							cyStructureSync.Error += OnDeploymentError;
							try
							{
								SetState(ContextExecutionState.Deploying, new ExecutionContextSyncInfo(cyStructureSync, key));
								cyStructureSync.Sync(cyStructure2, cyDirectoryInfo, substractSource: true, abortHandle);
							}
							catch
							{
								throw;
							}
							finally
							{
								cyStructureSync.Error -= OnDeploymentError;
							}
						}
						string registryString = key.RegistryString;
						if (!string.IsNullOrWhiteSpace(registryString) & !abortHandle.IsAborted)
						{
							SetState(ContextExecutionState.ImportingRegistry);
							CoreRegistryFile coreRegistryFile = new CoreRegistryFile();
							coreRegistryFile.LoadFromString(registryString);
							lock (SharedFunctions.ENVIRONMENT_LOCK)
							{
								Application.SetVariables();
								coreRegistryFile.Import();
								Application.UnsetVariables();
							}
						}
						if (!Client.IsMarkedDeploymentProfile(key.ID) & !abortHandle.IsAborted)
						{
							Client.MarkDeploymentProfile(key.ID, Executable.ID);
						}
					}
					catch (Exception ex4)
					{
						string text8 = $"Deployment of {key.Name} failed.";
						Client.LogAddError(text8, ex4, LogCategories.Operation);
						ThrowExecutionFailed(text8, ex4);
					}
				}
			}
		}
		if (!abortHandle.IsAborted)
		{
			lock (Executable.PersonalUserFiles)
			{
				foreach (PersonalUserFile personalUserFile in Executable.PersonalUserFiles)
				{
					if ((Client.IsMarkedPersonalUserFile(personalUserFile.ID) && !reProcess) | abortHandle.IsAborted)
					{
						continue;
					}
					string sourcePath = personalUserFile.SourcePath;
					string text9 = personalUserFile.Name ?? "Invalid name";
					int iD = personalUserFile.ID;
					try
					{
						if (string.IsNullOrWhiteSpace(sourcePath))
						{
							throw new ArgumentException("Invalid personal user file (" + text9 + ") source path specified");
						}
						sourcePath = sourcePath.Replace("%ENTRYPUBLISHER%", Application?.PublisherName ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
						sourcePath = sourcePath.Replace("%ENTRYDEVELOPER%", Application?.DeveloperName ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
						sourcePath = sourcePath.Replace("%ENTRYTITLE%", Application?.Title ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
						string dESTINATION_PATH = Environment.ExpandEnvironmentVariables(sourcePath);
						string fILE_FILTER = Client.BuildFilter(personalUserFile.IncludeFiles, personalUserFile.ExcludeFiles);
						string dIRECTORY_FILTER = Client.BuildFilter(personalUserFile.IncludeDirectories, personalUserFile.ExcludeDirectories);
						bool includeSubDirectories2 = personalUserFile.IncludeSubDirectories;
						bool iS_REGISTRY = personalUserFile.Type == PersonalUserFileType.Registry;
						try
						{
							if (personalUserFile.CleanUp)
							{
								Client.CleanPersonalFile(dESTINATION_PATH, fILE_FILTER, dIRECTORY_FILTER, includeSubDirectories2, text9, iS_REGISTRY);
							}
						}
						catch (Exception ex5)
						{
							ThrowExecutionFailed(ex5.Message, ex5);
						}
						SetState(ContextExecutionState.GettingUserProfile);
						Client.DeployPersonalFile(iD, dESTINATION_PATH, text9, iS_REGISTRY);
					}
					catch (Exception ex6)
					{
						string text10 = "Failed processing personal user file " + text9 + ".";
						Client.LogAddError(text10, ex6, LogCategories.Operation);
						ThrowExecutionFailed(text10, ex6);
					}
					finally
					{
						if (!Client.IsMarkedPersonalUserFile(personalUserFile.ID) & !abortHandle.IsAborted)
						{
							Client.MarkPersonalUserFile(personalUserFile.ID, Executable.ID);
						}
					}
				}
			}
		}
		if (!abortHandle.IsAborted && Executable.VirtualImages.Count > 0)
		{
			string text11 = Client.Settings.MounterPath;
			if (!string.IsNullOrWhiteSpace(text11))
			{
				text11 = Path.GetFullPath(Environment.ExpandEnvironmentVariables(text11));
			}
			if (string.IsNullOrWhiteSpace(text11) | string.IsNullOrWhiteSpace(Client.Settings.MounterOptions))
			{
				string message = "Virtual Image Mounter configuration is invalid.";
				Client.LogAdd(message, LogCategories.Operation);
				ThrowExecutionFailed(message);
			}
			else if (!File.Exists(text11))
			{
				string text12 = "Virtual Image Mounter configuration is invalid.";
				Exception ex7 = new FileNotFoundException("File not found.", text11);
				Client.LogAddError(text12, ex7, LogCategories.Operation);
				ThrowExecutionFailed(text12, ex7);
			}
			else
			{
				foreach (CDImage virtualImage in Executable.VirtualImages)
				{
					string cDImagePath = virtualImage.CDImagePath;
					if (string.IsNullOrWhiteSpace(cDImagePath))
					{
						string text13 = "Virtual Image configuration is invalid.";
						Client.LogAddError(text13, LogCategories.Operation);
						ThrowExecutionFailed(text13);
						continue;
					}
					cDImagePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(cDImagePath));
					Environment.SetEnvironmentVariable("cdimage", cDImagePath);
					Environment.SetEnvironmentVariable("deviceid", virtualImage.MountOptions.DeviceID.ToString());
					Environment.SetEnvironmentVariable("mountoptions", string.IsNullOrWhiteSpace(virtualImage.MountOptions.MountOptions) ? virtualImage.MountOptions.MountOptions : "");
					string arguments = Environment.ExpandEnvironmentVariables(Client.Settings.MounterOptions);
					Process process = Process.Start(text11, arguments);
					while (!(process.HasExited | abortHandle.IsAborted))
					{
						Thread.Sleep(500);
					}
					if (abortHandle.IsAborted)
					{
						process.Kill();
					}
					if (virtualImage.MountOptions.CheckExitCode && process.HasExited && process.ExitCode < 0)
					{
						Client.Log.AddInformation($"Virtual Image Mounter reported an error {process.ExitCode}.", LogCategories.Operation);
					}
				}
			}
		}
		if (!abortHandle.IsAborted && ((Executable.LicenseProfiles.Count > 0) & !IsAlive))
		{
			lock (LICENSE_OP_LOCK)
			{
				OnReleaseReservation();
				if (LicenseReservation == null)
				{
					SetState(ContextExecutionState.ReservingLicense);
					if (Client.CreateReservation(Executable.ID, out var reservation))
					{
						LicenseReservation = reservation;
						SetReleaseTimer();
					}
					else
					{
						string message2 = "License reservation failed for " + Executable.VisualOptions.Caption + ".";
						ThrowExecutionFailed(message2, null, ignoreHalt: true);
					}
				}
			}
		}
		if (!abortHandle.IsAborted & !HasCompleted)
		{
			OnProcessTasks(ActivationType.PreLicenseManagement, abortHandle);
		}
		if (!abortHandle.IsAborted && ((HasCompleted | AutoLaunch) & !IsAlive) && LicenseReservation != null)
		{
			foreach (int profileId in LicenseReservation.Licenses.Keys)
			{
				IApplicationLicense applicationLicense = LicenseReservation.Licenses[profileId];
				LicenseProfile licenseProfile = Executable.LicenseProfiles.Where((LicenseProfile lp) => lp.ID == profileId).Single();
				try
				{
					lock (SharedFunctions.ENVIRONMENT_LOCK)
					{
						Application.SetVariables();
						ILicenseManagerPlugin licenseManagerPlugin = (ILicenseManagerPlugin)Activator.CreateInstance(licenseProfile.PluginAssembly, licenseProfile.ManagerPlugin).Unwrap();
						try
						{
							if (licenseManagerPlugin is IConfigurableLicenseManager configurableLicenseManager)
							{
								configurableLicenseManager.Initialize(licenseProfile.PluginSettings);
							}
						}
						catch (Exception innerException)
						{
							throw new Exception("Plugin initialization failed.", innerException);
						}
						bool forceCreation = false;
						SetState(ContextExecutionState.InstallingLicense);
						licenseManagerPlugin.Install(applicationLicense, this, ref forceCreation);
						if (!flag && forceCreation)
						{
							flag = true;
						}
						Plugins.Add(licenseManagerPlugin, applicationLicense);
						Application.UnsetVariables();
					}
				}
				catch (Exception ex8)
				{
					string text14 = "License installation of " + licenseProfile.Name + " failed.";
					Client.Log.AddError(text14, ex8, LogCategories.Generic);
					ThrowExecutionFailed(text14, ex8, ignoreHalt: true);
				}
			}
		}
		if (abortHandle.IsAborted || !(HasCompleted | AutoLaunch))
		{
			return;
		}
		try
		{
			if ((!IsAlive | Executable.MultiRun) || !isAlive)
			{
				OnProcessTasks(ActivationType.PreLaunch, abortHandle);
			}
			if (flag || (!IsAlive | Executable.MultiRun))
			{
				if (abortHandle.IsAborted)
				{
					return;
				}
				foreach (IExecutionDivertPlugin item4 in Plugins.Keys.Where((ILicenseManagerPlugin x) => x is IExecutionDivertPlugin).ToList().Cast<IExecutionDivertPlugin>())
				{
					if (item4.DivertExecution(this))
					{
						return;
					}
				}
				SetState(ContextExecutionState.Starting);
				CoreProcess.WaitForWindowCreated(CreateProcess(), 5000);
			}
			else
			{
				SetState(ContextExecutionState.Activating);
				if (TryActivate())
				{
					SetState(ContextExecutionState.Activated);
				}
			}
		}
		catch (Exception ex9)
		{
			string text15 = "Could not start executable " + (Executable?.VisualOptions?.Caption ?? "Unknown Executable") + " (" + (Application?.Title ?? "Unknown App") + ").";
			Client.Log.AddError(text15, ex9, LogCategories.Generic);
			ThrowExecutionFailed(text15, ex9, ignoreHalt: true);
		}
	}

	private void OnAbort(bool async)
	{
		IAbortHandle abortHandle = AbortHandle;
		Trace($"Entering, abort handle is null {abortHandle == null}, isAsync={async}.", "OnAbort", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 1743);
		if (abortHandle != null)
		{
			lock (abortHandle)
			{
				if (IsAborting)
				{
					throw new ArgumentException("Already aborting.");
				}
				IsAborting = true;
				SetState(ContextExecutionState.Aborting);
				if (async)
				{
					Trace("Entering Abort.", "OnAbort", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 1757);
					abortHandle.Abort();
					Trace("Entered Abort.", "OnAbort", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 1759);
				}
				else
				{
					Trace("Entering AbortWait.", "OnAbort", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 1763);
					abortHandle.AbortWait();
					Trace("Entered AbortWait.", "OnAbort", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 1765);
				}
				return;
			}
		}
		throw new ArgumentNullException("AbortHandle", "Abort handle may not be null.");
	}

	private void OnReleaseReservation()
	{
		lock (LICENSE_OP_LOCK)
		{
			UnsetReleaseTimer();
			if (LicenseReservation != null)
			{
				if (Client.ReleaseReservation(LicenseReservation.Id))
				{
					SetState(ContextExecutionState.ReleasedLicense, LicenseReservation);
				}
				LicenseReservation = null;
			}
			foreach (KeyValuePair<ILicenseManagerPlugin, IApplicationLicense> plugin in Plugins)
			{
				try
				{
					plugin.Key.Uninstall(plugin.Value);
				}
				catch (Exception ex)
				{
					Client.Log.AddError("Could not uninstall application license.", ex, LogCategories.Configuration);
				}
			}
			Plugins.Clear();
		}
	}

	private void OnProcessTasks(ActivationType type, IAbortHandle abortHandle)
	{
		lock (CONTEXT_OP_LOCK)
		{
			foreach (Task item in Executable.Tasks.Where((Task task) => task.Activation.HasFlag(type)).ToList())
			{
				try
				{
					SetState(ContextExecutionState.ExecutingTask, item);
					if (item.IsActive)
					{
						item.InternalProcess.Kill();
					}
					lock (SharedFunctions.ENVIRONMENT_LOCK)
					{
						using (new EnvironmentVariableContext(this))
						{
							item.Execute(abortHandle);
						}
					}
				}
				catch (Exception ex)
				{
					string text = "Task (" + item.TaskName + ") execution failed.";
					Client.Log.AddError(text, ex, LogCategories.Generic);
					ThrowExecutionFailed(text, ex);
				}
			}
		}
	}

	private void SetReleaseTimer()
	{
		lock (LICENSE_OP_LOCK)
		{
			if (LicenseReleaseTimer != null)
			{
				LicenseReleaseTimer.Stop();
			}
			else
			{
				LicenseReleaseTimer = new System.Timers.Timer(60000.0)
				{
					AutoReset = false
				};
				LicenseReleaseTimer.Elapsed += OnReleaseLicenseTimerCallBack;
			}
			LicenseReleaseTimer.Start();
		}
	}

	private void UnsetReleaseTimer()
	{
		LicenseReleaseTimer?.Stop();
	}

	private void OnContextFailed(Exception ex)
	{
		SetState(ContextExecutionState.Failed, ex);
		OnFinalized();
	}

	private void OnWaitFinalize()
	{
		lock (FINALIZE_OP_LOCK)
		{
			try
			{
				SafeReleaseFinalizeHandle();
				IsWaitingFinalization = true;
				FinalizeWaitHandle = new ManualResetEvent(initialState: false);
				if (!IsDestructionInitiated)
				{
					Thread.Sleep(WaitFinalizeTime);
				}
				lock (CONTEXT_OP_LOCK)
				{
					if (!IsAlive && !IsExecuting)
					{
						OnFinalized();
					}
				}
			}
			catch
			{
				throw;
			}
			finally
			{
				SafeReleaseFinalizeHandle();
				IsWaitingFinalization = false;
			}
		}
	}

	private void OnFinalized()
	{
		try
		{
			lock (CONTEXT_OP_LOCK)
			{
				try
				{
					double totalSeconds = TotalExecutionSpan.TotalSeconds;
					DateTime? dateTime = startTime?.ToLocalTime();
					if (dateTime.HasValue && totalSeconds > 0.0)
					{
						Client.AppStatSet(Executable.ID, dateTime.Value, totalSeconds);
					}
				}
				catch (Exception ex)
				{
					Trace(ex, "OnFinalized", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 1952);
				}
				try
				{
					OnReleaseReservation();
				}
				catch (Exception ex2)
				{
					Trace(ex2, "OnFinalized", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 1963);
				}
				OnProcessTasks(ActivationType.PostTermination, abortHandle);
				Reset();
				SetState(ContextExecutionState.Finalized);
				IsFinalized = true;
				Trace("Execution context finalized.", "OnFinalized", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 1979);
			}
		}
		catch (Exception ex3)
		{
			Client.LogAddError("Execution context finalization failed.", ex3, LogCategories.Operation);
		}
	}

	private void SetState(ContextExecutionState newState, object arguments = null)
	{
		ContextExecutionState oldState = State;
		State = newState;
		RaiseContextStateChanged(new ExecutionContextStateArgs(Executable.ID, newState, oldState, arguments));
	}

	private void RaiseContextStateChanged(ExecutionContextStateArgs args)
	{
		if (args == null)
		{
			throw new ArgumentNullException("args");
		}
		EventHandler<ExecutionContextStateArgs> executionStateChaged = this.ExecutionStateChaged;
		if (executionStateChaged == null)
		{
			return;
		}
		Delegate[] invocationList = executionStateChaged.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<ExecutionContextStateArgs> eventHandler = (EventHandler<ExecutionContextStateArgs>)invocationList[i];
			try
			{
				eventHandler(this, args);
			}
			catch (Exception ex)
			{
				Trace(ex, "RaiseContextStateChanged", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 2027);
			}
		}
	}

	private void ThrowExecutionFailed(string message, Exception innerException = null, bool ignoreHalt = false)
	{
		if (innerException is ContextExecutionFailedException)
		{
			throw innerException;
		}
		if (HaltOnError || ignoreHalt)
		{
			throw new ContextExecutionFailedException(message, innerException);
		}
	}

	private void SafeReleaseFinalizeHandle()
	{
		try
		{
			lock (FINALIZE_OP_LOCK)
			{
				if (FinalizeWaitHandle != null)
				{
					FinalizeWaitHandle.Set();
					FinalizeWaitHandle.Close();
					FinalizeWaitHandle = null;
				}
			}
		}
		catch (Exception ex)
		{
			Trace("Finalize wait handle release failed.", ex, "SafeReleaseFinalizeHandle", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 2074);
		}
	}

	private void OnProcessPostCreating(object sender, ProcessCreatingEventArgs e)
	{
		if (!IsUsable || e.ResultCode != 0 || !MonitorChildren)
		{
			return;
		}
		try
		{
			bool flag = false;
			ExecutionContextProcessInfo processInfo = null;
			lock (CONTEXT_OP_LOCK)
			{
				if (!IsUsable || Processes.ContainsKey(e.ProcessId))
				{
					return;
				}
				foreach (int key in Processes.Keys)
				{
					if (!IsUsable)
					{
						break;
					}
					if (e.ParentProcessId == key)
					{
						if (AddProcess(e.ProcessId, e.ProcessName, isMain: false, raiseContextEvent: false, out processInfo))
						{
							flag = true;
						}
						break;
					}
				}
			}
			if (flag)
			{
				SetState(ContextExecutionState.ProcessCreated, processInfo);
			}
		}
		catch (Exception ex)
		{
			Client.Log.AddError("Could not handle execution context process created event.", ex, LogCategories.Generic);
		}
	}

	private void OnProcessTerminated(object sender, ProcessTerminatedEventArgs e)
	{
		try
		{
			bool flag = false;
			ExecutionContextProcessInfo value;
			lock (CONTEXT_OP_LOCK)
			{
				if (!Processes.TryRemove(e.ProcessId, out value))
				{
					return;
				}
				int count = Processes.Count;
				Trace(string.Format("Removed process from execution context, process id: {0}, process file name {1}", e.ProcessId, value?.ProcessFileName ?? "Unknown"), "OnProcessTerminated", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 2186);
				Trace($"New tracked process count (remove) {count}.", "OnProcessTerminated", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 2187);
				bool num = count > 0;
				if (!num)
				{
					endTime = DateTime.UtcNow;
				}
				flag = value.IsMain && TerminateChildren;
				if (!num && !IsWaitingFinalization && !IsFinalized)
				{
					Action action = OnWaitFinalize;
					action.BeginInvoke(OnFinalizeCallBack, action);
				}
			}
			SetState(ContextExecutionState.ProcessExited, value);
			if (flag)
			{
				try
				{
					CoreProcess.KillChildren(e.ProcessId, recurse: true);
					return;
				}
				catch (Exception ex)
				{
					Client.Log.AddError("Could not kill child processes in execution context process exited event.", ex, LogCategories.Generic);
					return;
				}
			}
		}
		catch (Exception ex2)
		{
			Client.Log.AddError("Could not handle execution context process exited event.", ex2, LogCategories.Generic);
		}
	}

	private void OnDeploymentError(object sender, ExceptionEventArgs args)
	{
		Client.Log.AddError("Error during deployment", args.Exception, LogCategories.FileSystem);
	}

	private void OnFinalizeCallBack(IAsyncResult result)
	{
		try
		{
			((Action)result.AsyncState).EndInvoke(result);
		}
		catch (Exception ex)
		{
			Trace("Finalization callback error.", ex, "OnFinalizeCallBack", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 2252);
		}
	}

	private void OnExecutionCallBack(IAsyncResult result)
	{
		try
		{
			IAbortHandle abortHandle = (IAbortHandle)result.AsyncState;
			IsExecuting = false;
			IsAborting = false;
			if ((object)abortHandle.Delegate != null)
			{
				((Action<IAbortHandle, bool>)abortHandle.Delegate).EndInvoke(result);
			}
			if (abortHandle.IsAborted)
			{
				SetState(ContextExecutionState.Aborted);
				HasCompleted = false;
			}
			else
			{
				SetState(ContextExecutionState.Completed);
				HasCompleted = true;
			}
		}
		catch (OperationCanceledException)
		{
			SetState(ContextExecutionState.Aborted);
			HasCompleted = false;
		}
		catch (ContextExecutionFailedException ex2)
		{
			OnContextFailed(ex2);
		}
		catch (Exception ex3)
		{
			OnContextFailed(ex3);
			Client.Log.AddError("Execution context error.", ex3, LogCategories.Generic);
		}
	}

	private void OnReleaseLicenseTimerCallBack(object sender, ElapsedEventArgs args)
	{
		try
		{
			if (!IsAlive)
			{
				OnReleaseReservation();
			}
		}
		catch (Exception ex)
		{
			Trace(ex, "OnReleaseLicenseTimerCallBack", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Classes\\ExecutionContext.cs", 2316);
		}
	}
}
