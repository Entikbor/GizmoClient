using System;
using System.ComponentModel.Composition;
using System.Threading;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

public abstract class MediaViewModelBase : ViewModel, IMediaViewModel
{
	private IExecutionChangedAwareCommand exitFullScreenCommand;

	private IExecutionChangedAwareCommand fullScreenCommand;

	private MediaSourceType sourceType;

	private bool isInputIdle;

	private bool isFailed;

	private string mediaSource;

	private IExecutionChangedAwareCommand mouseMoveCommand;

	private Timer INPUT_IDLE_TIMER;

	private readonly int IDLE_SPAN = 3000;

	private readonly object CALLBACK_LOCK = new object();

	[Import]
	protected GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand ExitFullScreenCommand
	{
		get
		{
			return exitFullScreenCommand;
		}
		set
		{
			SetProperty(ref exitFullScreenCommand, value, "ExitFullScreenCommand");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand FullScreenCommand
	{
		get
		{
			return fullScreenCommand;
		}
		set
		{
			SetProperty(ref fullScreenCommand, value, "FullScreenCommand");
		}
	}

	[IgnorePropertyModification]
	public MediaSourceType SourceType
	{
		get
		{
			return sourceType;
		}
		internal set
		{
			SetProperty(ref sourceType, value, "SourceType");
		}
	}

	[IgnorePropertyModification]
	public string MediaSource
	{
		get
		{
			return mediaSource;
		}
		internal set
		{
			SetProperty(ref mediaSource, value, "MediaSource");
		}
	}

	[IgnorePropertyModification]
	public bool IsInputIdle
	{
		get
		{
			return isInputIdle;
		}
		private set
		{
			SetProperty(ref isInputIdle, value, "IsInputIdle");
		}
	}

	[IgnorePropertyModification]
	public bool IsFailed
	{
		get
		{
			return isFailed;
		}
		protected set
		{
			SetProperty(ref isFailed, value, "IsFailed");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand MouseMoveCommand
	{
		get
		{
			if (mouseMoveCommand == null)
			{
				mouseMoveCommand = new SimpleCommand<object, object>((object e) => true, OnMouseMoveCommand);
			}
			return mouseMoveCommand;
		}
	}

	private void OnMouseMoveCommand(object param)
	{
		lock (CALLBACK_LOCK)
		{
			IsInputIdle = false;
			if (INPUT_IDLE_TIMER == null)
			{
				INPUT_IDLE_TIMER = new Timer(OnIdleTimerCallback, null, IDLE_SPAN, IDLE_SPAN);
			}
			INPUT_IDLE_TIMER.Change(IDLE_SPAN, IDLE_SPAN);
		}
	}

	public virtual void Attach()
	{
		lock (CALLBACK_LOCK)
		{
			if (INPUT_IDLE_TIMER == null)
			{
				INPUT_IDLE_TIMER = new Timer(OnIdleTimerCallback, null, IDLE_SPAN, IDLE_SPAN);
			}
			INPUT_IDLE_TIMER.Change(IDLE_SPAN, IDLE_SPAN);
		}
	}

	public virtual void Detach()
	{
		lock (CALLBACK_LOCK)
		{
			INPUT_IDLE_TIMER?.Dispose();
			INPUT_IDLE_TIMER = null;
		}
	}

	private void OnIdleTimerCallback(object param)
	{
		if (!Monitor.TryEnter(CALLBACK_LOCK))
		{
			return;
		}
		try
		{
			INPUT_IDLE_TIMER?.Dispose();
			INPUT_IDLE_TIMER = null;
			IsInputIdle = true;
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnIdleTimerCallback", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\MediaViewModel.cs", 162);
		}
		finally
		{
			Monitor.Exit(CALLBACK_LOCK);
		}
	}

	protected override void OnResetCommands()
	{
		base.OnResetCommands();
		ExitFullScreenCommand?.RaiseCanExecuteChanged();
		FullScreenCommand?.RaiseCanExecuteChanged();
	}
}
