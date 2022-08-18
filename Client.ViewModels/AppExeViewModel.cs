using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreLib;
using CyClone.Core;
using GizmoDALV2.Entities;
using SharedLib;
using SharedLib.Configuration;
using SkinInterfaces;
using Windows.UI.Notifications;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class AppExeViewModel : ExecuteViewModelBase, IAppExeViewModel, IPartImportsSatisfiedNotification
{
	private NotifyTaskCompletion<byte[]> imageTask;

	private NotifyTaskCompletion<bool> exeutableFileExiststTask;

	private NotifyTaskCompletion<IEnumerable<PersonalFileViewModel>> personalFilesTask;

	private string caption;

	private string executablePath;

	private int exeId;

	private int appId;

	private int displayOrder;

	private IExecutionChangedAwareCommand resetCommand;

	private IExecutionChangedAwareCommand terminateCommand;

	private double progress;

	private bool isActive;

	private bool isReady;

	private bool isRunning;

	private bool isIndeterminate;

	private bool autoStart;

	private bool isAccessible;

	private bool isQuickLaunch;

	private bool hasDeploymentProfiles;

	private bool isOverlayShowing;

	private bool isIgnoringConcurrentExecutionLimit;

	private int totalExecutions;

	private int totalUserExecutions;

	private double totalTime;

	private double totalUserTime;

	private AppViewModel app;

	private readonly object PROGRESS_CB_LOCK = new object();

	private Timer PROGRESS_TIMER;

	private readonly SemaphoreSlim EXECUTE_LOCK = new SemaphoreSlim(1, 1);

	[Import]
	private IAppImageHandler ImageHandler { get; set; }

	[Import]
	private IDialogService DialogService { get; set; }

	[Import]
	private IShellWindow Shell { get; set; }

	[Import]
	private GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand ResetCommand
	{
		get
		{
			if (resetCommand == null)
			{
				resetCommand = new SimpleCommand<object, object>(OnCanResetCommand, OnResetCommand);
			}
			return resetCommand;
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand TerminateCommand
	{
		get
		{
			if (terminateCommand == null)
			{
				terminateCommand = new SimpleCommand<object, object>(OnCanTerminateCommand, OnTerminateCommand);
			}
			return terminateCommand;
		}
	}

	[IgnorePropertyModification]
	public AppViewModel App
	{
		get
		{
			return app;
		}
		set
		{
			SetProperty(ref app, value, "App");
		}
	}

	[IgnorePropertyModification]
	public NotifyTaskCompletion<byte[]> ImageData
	{
		get
		{
			if (imageTask == null)
			{
				imageTask = new NotifyTaskCompletion<byte[]>(GetImageHandler());
			}
			return imageTask;
		}
		internal set
		{
			SetProperty(ref imageTask, value, "ImageData");
		}
	}

	[IgnorePropertyModification]
	public NotifyTaskCompletion<bool> ExecutableFileExists
	{
		get
		{
			if (exeutableFileExiststTask == null)
			{
				exeutableFileExiststTask = new NotifyTaskCompletion<bool>(GetExecutableFileExistsHandler());
			}
			return exeutableFileExiststTask;
		}
		internal set
		{
			SetProperty(ref exeutableFileExiststTask, value, "ExecutableFileExists");
		}
	}

	[IgnorePropertyModification]
	public int ExeId
	{
		get
		{
			return exeId;
		}
		set
		{
			SetProperty(ref exeId, value, "ExeId");
		}
	}

	[IgnorePropertyModification]
	public int DisplayOrder
	{
		get
		{
			return displayOrder;
		}
		set
		{
			SetProperty(ref displayOrder, value, "DisplayOrder");
		}
	}

	[IgnorePropertyModification]
	public string Caption
	{
		get
		{
			return caption;
		}
		set
		{
			SetProperty(ref caption, value, "Caption");
		}
	}

	[IgnorePropertyModification]
	public string ExecutablePath
	{
		get
		{
			return executablePath;
		}
		set
		{
			SetProperty(ref executablePath, value, "ExecutablePath");
		}
	}

	[IgnorePropertyModification]
	public bool IsIndeterminate
	{
		get
		{
			return isIndeterminate;
		}
		set
		{
			SetProperty(ref isIndeterminate, value, "IsIndeterminate");
		}
	}

	[IgnorePropertyModification]
	public double Progress
	{
		get
		{
			return progress;
		}
		set
		{
			SetProperty(ref progress, value, "Progress");
		}
	}

	[IgnorePropertyModification]
	public int AppId
	{
		get
		{
			return appId;
		}
		set
		{
			SetProperty(ref appId, value, "AppId");
		}
	}

	[IgnorePropertyModification]
	public bool AutoStart
	{
		get
		{
			return autoStart;
		}
		set
		{
			SetProperty(ref autoStart, value, "AutoStart");
		}
	}

	[IgnorePropertyModification]
	public bool IsAccessible
	{
		get
		{
			return isAccessible;
		}
		set
		{
			SetProperty(ref isAccessible, value, "IsAccessible");
		}
	}

	[IgnorePropertyModification]
	public ApplicationModes Modes { get; set; }

	[IgnorePropertyModification]
	public double TotalTime
	{
		get
		{
			return totalTime;
		}
		set
		{
			SetProperty(ref totalTime, value, "TotalTime");
		}
	}

	[IgnorePropertyModification]
	public double TotalUserTime
	{
		get
		{
			return totalUserTime;
		}
		set
		{
			SetProperty(ref totalUserTime, value, "TotalUserTime");
		}
	}

	[IgnorePropertyModification]
	public int TotalExecutions
	{
		get
		{
			return totalExecutions;
		}
		set
		{
			SetProperty(ref totalExecutions, value, "TotalExecutions");
		}
	}

	[IgnorePropertyModification]
	public int TotalUserExecutions
	{
		get
		{
			return totalUserExecutions;
		}
		set
		{
			SetProperty(ref totalUserExecutions, value, "TotalUserExecutions");
		}
	}

	[IgnorePropertyModification]
	public bool IsQuickLaunch
	{
		get
		{
			return isQuickLaunch;
		}
		set
		{
			SetProperty(ref isQuickLaunch, value, "IsQuickLaunch");
		}
	}

	[IgnorePropertyModification]
	public bool HasDeploymentProfiles
	{
		get
		{
			return hasDeploymentProfiles;
		}
		set
		{
			SetProperty(ref hasDeploymentProfiles, value, "HasDeploymentProfiles");
		}
	}

	[IgnorePropertyModification]
	public int AgeRating => App?.AgeRating ?? 0;

	[IgnorePropertyModification]
	public AgeRatingType AgeRatingType => App?.AgeRatingType ?? AgeRatingType.None;

	[IgnorePropertyModification]
	public bool IsActive
	{
		get
		{
			return isActive;
		}
		protected set
		{
			SetProperty(ref isActive, value, "IsActive");
		}
	}

	[IgnorePropertyModification]
	public bool IsReady
	{
		get
		{
			return isReady;
		}
		set
		{
			SetProperty(ref isReady, value, "IsReady");
		}
	}

	[IgnorePropertyModification]
	public bool IsRunning
	{
		get
		{
			return isRunning;
		}
		set
		{
			SetProperty(ref isRunning, value, "IsRunning");
		}
	}

	[IgnorePropertyModification]
	public bool IsOverlayShowing
	{
		get
		{
			return isOverlayShowing;
		}
		set
		{
			SetProperty(ref isOverlayShowing, value, "IsOverlayShowing");
		}
	}

	[IgnorePropertyModification]
	public NotifyTaskCompletion<IEnumerable<PersonalFileViewModel>> PersonalFiles
	{
		get
		{
			if (personalFilesTask == null)
			{
				personalFilesTask = new NotifyTaskCompletion<IEnumerable<PersonalFileViewModel>>(GetPersonalFilesHandler());
			}
			return personalFilesTask;
		}
		internal set
		{
			SetProperty(ref personalFilesTask, value, "PersonalFiles");
		}
	}

	[IgnorePropertyModification]
	public bool IsIgnoringConcurrentExecutionLimit
	{
		get
		{
			return isIgnoringConcurrentExecutionLimit;
		}
		set
		{
			SetProperty(ref isIgnoringConcurrentExecutionLimit, value, "IsIgnoringConcurrentExecutionLimit");
		}
	}

	private async Task<byte[]> GetImageHandler()
	{
		return await ImageHandler.TryGetImageDataAsync(ExeId, ImageType.Executable).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<bool> GetExecutableFileExistsHandler()
	{
		if (HasDeploymentProfiles)
		{
			return true;
		}
		string EXECUTABLE_PATH = ExecutablePath;
		if (string.IsNullOrWhiteSpace(EXECUTABLE_PATH))
		{
			return false;
		}
		return await Task.Run(delegate
		{
			EXECUTABLE_PATH = EXECUTABLE_PATH.Replace("%ENTRYPUBLISHER%", App?.Publisher?.Name ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
			EXECUTABLE_PATH = EXECUTABLE_PATH.Replace("%ENTRYDEVELOPER%", App?.Developer?.Name ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
			EXECUTABLE_PATH = EXECUTABLE_PATH.Replace("%ENTRYTITLE%", App?.Title ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
			return File.Exists(Environment.ExpandEnvironmentVariables(EXECUTABLE_PATH));
		});
	}

	private async Task<IEnumerable<PersonalFileViewModel>> GetPersonalFilesHandler()
	{
		return (from puf in await Client.AppExePersonalFileGetAsync(ExeId)
			where puf.PersonalFile != null
			where puf.PersonalFile.Accessible
			orderby puf.UseOrder
			select puf).Select(delegate(AppExePersonalFile puf)
		{
			PersonalFileViewModel exportedValue = Client.GetExportedValue<PersonalFileViewModel>();
			exportedValue.Caption = puf.PersonalFile.Caption;
			string source = puf.PersonalFile.Source;
			if (!string.IsNullOrWhiteSpace(source))
			{
				source = source.Replace("%ENTRYPUBLISHER%", App?.Publisher?.Name ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
				source = source.Replace("%ENTRYDEVELOPER%", App?.Developer?.Name ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
				source = source.Replace("%ENTRYTITLE%", App?.Title ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
				string expandedPath = Environment.ExpandEnvironmentVariables(source);
				exportedValue.ExpandedPath = expandedPath;
			}
			return exportedValue;
		});
	}

	private async Task<bool> ValidateAgeAndNotifyAsync()
	{
		if (!PassAgeRating())
		{
			string localizedString = Client.GetLocalizedString("MESSAGE_APPLICATION_AGE_RESTRICTED");
			await DialogService.ShowAcceptDialogAsync(localizedString);
			return false;
		}
		return true;
	}

	private async Task<bool> ValidateExecutionCountAndNotifyAsync()
	{
		if ((Client.Settings?.IsConcurrentExecutionLimitEnabled ?? false) && !IsIgnoringConcurrentExecutionLimit && await Client.ExecutionContextActiveCountGetAsync(ExeId) > 0)
		{
			string localizedString = Client.GetLocalizedString("WARNING_CONCURRENT_EXECUTION_LIMIT_REACHED");
			if (!(await DialogService.ShowAcceptDialogAsync(localizedString, MessageDialogButtons.AcceptCancel)))
			{
				return false;
			}
			await Client.ExecutionContextKillAsync();
		}
		return true;
	}

	private bool PassAgeRating()
	{
		if (AgeRatingType == AgeRatingType.None)
		{
			return true;
		}
		ClientSettings settings = Client.Settings;
		if (settings == null || !settings.IsAgeRatingsEnabled)
		{
			return true;
		}
		if (!Client.IsUserLoggedIn)
		{
			return true;
		}
		if (Client.IsCurrentUserIsGuest)
		{
			return true;
		}
		uint num = 0u;
		switch (AgeRatingType)
		{
		case AgeRatingType.Manual:
			num = ((AgeRating > 0) ? ((uint)AgeRating) : 0u);
			break;
		case AgeRatingType.PEGI:
			num = ((PEGI)AgeRating).GetAttributeOfType<AgeRatingAttribute>().Age;
			break;
		case AgeRatingType.ESRB:
			num = ((ESRB)AgeRating).GetAttributeOfType<AgeRatingAttribute>().Age;
			break;
		}
		return Client.CurrentUser?.BirthDate.Age() >= num;
	}

	public Task<bool> ValidateExecutableFileExist()
	{
		return GetExecutableFileExistsHandler();
	}

	protected override bool OnCanExecuteCommand(object param)
	{
		return true;
	}

	protected override async void OnExecuteCommand(object param)
	{
		_ = 5;
		try
		{
			if (!(await EXECUTE_LOCK.WaitAsync(TimeSpan.Zero)))
			{
				return;
			}
			try
			{
				try
				{
					if (!(await ValidateAgeAndNotifyAsync()) || !(await ValidateExecutionCountAndNotifyAsync()))
					{
						return;
					}
				}
				catch (OperationCanceledException)
				{
					return;
				}
				IsActive = true;
				try
				{
					AsyncContextResult asyncContextResult = await Client.GetExecutionContextAsync(ExeId, CancellationToken.None);
					if (!asyncContextResult.Success)
					{
						IsActive = false;
						return;
					}
					ExecutionContext context = asyncContextResult.Context;
					if (context.IsExecuting)
					{
						string localizedString = Client.GetLocalizedString("MESSAGE_CONFIRM_CANCEL");
						if (await DialogService.ShowAcceptDialogAsync(localizedString, MessageDialogButtons.AcceptCancel))
						{
							context.Abort(async: true);
						}
						return;
					}
					context.AutoLaunch = AutoStart;
					try
					{
						await Task.Run(delegate
						{
							context.Execute();
						});
					}
					catch (ArgumentException)
					{
					}
				}
				catch
				{
					throw;
				}
			}
			catch (Exception ex3)
			{
				Client.TraceWrite(ex3, "OnExecuteCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\AppExeViewModel.cs", 542);
			}
			finally
			{
				EXECUTE_LOCK.Release();
			}
		}
		catch (Exception ex4)
		{
			Client.LogAddError("OnExecuteCommand", ex4, LogCategories.Operation);
		}
	}

	private bool OnCanResetCommand(object param)
	{
		return !IsActive;
	}

	private async void OnResetCommand(object param)
	{
		_ = 4;
		try
		{
			if (!(await EXECUTE_LOCK.WaitAsync(TimeSpan.Zero)))
			{
				return;
			}
			try
			{
				try
				{
					if (!(await ValidateAgeAndNotifyAsync()))
					{
						return;
					}
				}
				catch (OperationCanceledException)
				{
					return;
				}
				try
				{
					string localizedObject = Client.GetLocalizedObject<string>("MESSAGE_CONFIRM_REPAIR");
					if (!(await DialogService.ShowAcceptDialogAsync(localizedObject, MessageDialogButtons.AcceptCancel)))
					{
						return;
					}
					if (IsActive)
					{
						return;
					}
					AsyncContextResult asyncContextResult = await Client.GetExecutionContextAsync(ExeId, CancellationToken.None);
					if (!asyncContextResult.Success)
					{
						return;
					}
					ExecutionContext context = asyncContextResult.Context;
					context.AutoLaunch = AutoStart;
					try
					{
						await Task.Run(delegate
						{
							context.Execute(reprocess: true);
						});
					}
					catch (ArgumentException)
					{
					}
				}
				catch (Exception ex3)
				{
					Client.TraceWrite(ex3, "OnResetCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\AppExeViewModel.cs", 605);
				}
			}
			catch (Exception ex4)
			{
				Client.TraceWrite(ex4, "OnResetCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\AppExeViewModel.cs", 610);
			}
			finally
			{
				EXECUTE_LOCK.Release();
			}
		}
		catch (Exception ex5)
		{
			Client.LogAddError("OnResetCommand", ex5, LogCategories.Operation);
		}
	}

	private bool OnCanTerminateCommand(object param)
	{
		return IsRunning;
	}

	private async void OnTerminateCommand(object param)
	{
		_ = 2;
		try
		{
			if (!(await EXECUTE_LOCK.WaitAsync(TimeSpan.Zero)))
			{
				return;
			}
			try
			{
				AsyncContextResult contextResult = await Client.GetExecutionContextAsync(ExeId, CancellationToken.None);
				if (contextResult.Success)
				{
					await Task.Run(delegate
					{
						contextResult.Context.Kill();
					});
				}
			}
			catch
			{
				throw;
			}
			finally
			{
				EXECUTE_LOCK.Release();
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnTerminateCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\AppExeViewModel.cs", 655);
		}
	}

	protected override void OnResetCommands()
	{
		base.OnResetCommands();
		base.ExecuteCommand?.RaiseCanExecuteChanged();
		ResetCommand?.RaiseCanExecuteChanged();
		TerminateCommand?.RaiseCanExecuteChanged();
	}

	private async void OnClientExecutionContextStateChage(object sender, ExecutionContextStateArgs e)
	{
		_ = 3;
		try
		{
			if (e.ExecutableId != ExeId || !(sender is IExecutionContext executionContext))
			{
				return;
			}
			IsRunning = executionContext.IsAlive;
			IsActive = executionContext.IsExecuting;
			IsReady = executionContext.HasCompleted || (e.NewState == ContextExecutionState.Completed && !executionContext.IsExecuting);
			if (executionContext.IsExecuting)
			{
				IsIndeterminate = true;
				Progress = 0.0;
			}
			else
			{
				IsIndeterminate = false;
			}
			PROGRESS_TIMER?.Dispose();
			PROGRESS_TIMER = null;
			switch (e.NewState)
			{
			case ContextExecutionState.Destroyed:
			case ContextExecutionState.Released:
				IsReady = false;
				IsActive = false;
				break;
			case ContextExecutionState.Deploying:
				if (e.StateObject is IExecutionContextSyncInfo state)
				{
					PROGRESS_TIMER = new Timer(OnProgressTimerCallback, state, 0, 200);
				}
				IsIndeterminate = false;
				break;
			case ContextExecutionState.Completed:
			{
				if (AutoStart || executionContext.HasCompleted)
				{
					break;
				}
				string HEADER_MESSAGE = Client.GetLocalizedString("MESSAGE_APP_READY_TO_START");
				string localizedString = Client.GetLocalizedString("MESSAGE_PRESS_PLAY_TO_LAUNCH");
				string MESSAGE = string.Format(localizedString, Caption ?? "?");
				string localizedString2 = Client.GetLocalizedString("APP_PLAY");
				string ARGUMENT = "START_APP";
				ToastTemplateTypeProxy TEMPLATE = ToastTemplateTypeProxy.ToastImageAndText02;
				ToastNotificationPriorityProxy PRIORITY = ToastNotificationPriorityProxy.Default;
				CancellationToken CT = CancellationToken.None;
				ToastAction[] ACTIONS = new ToastAction[1]
				{
					new ToastAction
					{
						Arguments = ARGUMENT,
						Content = localizedString2
					}
				};
				byte[] array = await ImageData.Task;
				ToastResult toastResult;
				try
				{
					if (array != null)
					{
						string tempFileName = Path.GetTempFileName();
						File.WriteAllBytes(tempFileName, array);
						toastResult = await Client.ShowToastAsync(TEMPLATE, new string[2] { HEADER_MESSAGE, MESSAGE }, tempFileName, ACTIONS, PRIORITY, CT);
					}
					else
					{
						toastResult = await Client.ShowToastWithResourcetAsync(TEMPLATE, new string[2] { HEADER_MESSAGE, MESSAGE }, "Client.Resources.Icons.done.png", ACTIONS, PRIORITY, CT);
					}
				}
				catch (OperationCanceledException)
				{
					return;
				}
				if (toastResult != null && !toastResult.DismissReason.HasValue && toastResult.Arguments == ARGUMENT && OnCanExecuteCommand(null))
				{
					OnExecuteCommand(null);
				}
				break;
			}
			}
			if (e.NewState == ContextExecutionState.Failed)
			{
				string localizedString3 = Client.GetLocalizedString("MESSAGE_ERROR_STARTING");
				switch (e.OldState)
				{
				case ContextExecutionState.ReservingLicense:
					localizedString3 = Client.GetLocalizedString("MESSAGE_NO_MORE_LICENSES");
					break;
				case ContextExecutionState.ChekingAvailableSpace:
				case ContextExecutionState.MakingSpace:
					localizedString3 = Client.GetLocalizedString("MESSAGE_NO_DISK_SPACE");
					break;
				}
				await DialogService.ShowAcceptDialogAsync(localizedString3);
			}
			ResetCommands();
		}
		catch (Exception ex2)
		{
			Client.TraceWrite(ex2, "OnClientExecutionContextStateChage", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\AppExeViewModel.cs", 818);
		}
	}

	private void OnProgressTimerCallback(object state)
	{
		if (!Monitor.TryEnter(PROGRESS_CB_LOCK))
		{
			return;
		}
		try
		{
			if (!(state is IExecutionContextSyncInfo executionContextSyncInfo) || !(executionContextSyncInfo.Syncer is cyStructureSync cyStructureSync))
			{
				return;
			}
			long total = cyStructureSync.Total;
			long totalWritten = cyStructureSync.TotalWritten;
			if (total > 0)
			{
				Progress = totalWritten * 100 / total;
				if (IsIndeterminate)
				{
					IsIndeterminate = false;
				}
			}
			else
			{
				if (!IsIndeterminate)
				{
					IsIndeterminate = true;
				}
				Progress = 0.0;
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnProgressTimerCallback", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\AppExeViewModel.cs", 851);
		}
		finally
		{
			Monitor.Exit(PROGRESS_CB_LOCK);
		}
	}

	private void OnShellOverlayEvent(object sender, OverlayEventArgs e)
	{
		IsOverlayShowing = e.IsOpen;
	}

	public void OnImportsSatisfied()
	{
		Client.ExecutionContextStateChage += OnClientExecutionContextStateChage;
		Shell.OverlayEvent += OnShellOverlayEvent;
	}
}
