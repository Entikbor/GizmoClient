using System;
using System.ComponentModel.Composition;
using System.Threading;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
public class ClientGracePeriodViewModel : ViewModel, IPartImportsSatisfiedNotification
{
	private bool isInGracePeriod;

	private TimeSpan time;

	private Timer timer;

	private IExecutionChangedAwareCommand logoutCommand;

	[Import]
	public GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public bool IsInGracePeriod
	{
		get
		{
			return isInGracePeriod;
		}
		protected set
		{
			SetProperty(ref isInGracePeriod, value, "IsInGracePeriod");
		}
	}

	[IgnorePropertyModification]
	public TimeSpan Time
	{
		get
		{
			return time;
		}
		protected set
		{
			SetProperty(ref time, value, "Time");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand LogoutCommand
	{
		get
		{
			if (logoutCommand == null)
			{
				logoutCommand = new SimpleCommand<object, object>(OnCanLogoutCommand, OnLogoutCommand);
			}
			return logoutCommand;
		}
		internal set
		{
			SetProperty(ref logoutCommand, value, "LogoutCommand");
		}
	}

	private void OnClientGracePeriodChange(object sender, GracePeriodChangeEventArgs e)
	{
		IsInGracePeriod = e.IsInGracePeriod;
		if (IsInGracePeriod)
		{
			Time = TimeSpan.FromMinutes(e.GracePeriodTime);
			if (timer == null)
			{
				timer = new Timer(OnTimerCallback, null, 0, 1000);
			}
		}
		else if (timer != null)
		{
			timer.Dispose();
			timer = null;
		}
	}

	private void OnTimerCallback(object state)
	{
		Time = TimeSpan.FromSeconds(Time.TotalSeconds - 1.0);
		if (Time.TotalSeconds <= 0.0)
		{
			Time = TimeSpan.FromSeconds(0.0);
			if (timer != null)
			{
				timer.Dispose();
				timer = null;
			}
		}
	}

	private bool OnCanLogoutCommand(object param)
	{
		return true;
	}

	private async void OnLogoutCommand(object param)
	{
		await Client.LogoutAsync();
	}

	public void OnImportsSatisfied()
	{
		Time = default(TimeSpan);
		Client.GracePeriodChange += OnClientGracePeriodChange;
	}
}
