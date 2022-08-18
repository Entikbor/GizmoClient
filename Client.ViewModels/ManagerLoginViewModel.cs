using System;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Client.Controls;
using CoreLib;
using SharedLib;
using SkinInterfaces;
using TransformLib.Encryption;

namespace Client.ViewModels;

public class ManagerLoginViewModel : ClientNotifyWindowViewModelBase
{
	private string password;

	private SimpleCommand<object, object> exitCommand;

	private SimpleCommand<object, object> uninstallCommand;

	private SimpleCommand<object, object> adminModeCommand;

	private SimpleCommand<object, object> acceptCommand;

	private bool isAuthenticated;

	private uint loginAttempts;

	private uint maxAttempts = 3u;

	private System.Threading.Timer timer;

	private int maximumTime = 30000;

	private object lockObject = new object();

	public SimpleCommand<object, object> UninstallCommand
	{
		get
		{
			if (uninstallCommand == null)
			{
				uninstallCommand = new SimpleCommand<object, object>(OnCanUninstallCommand, OnUninstallCommand);
			}
			return uninstallCommand;
		}
	}

	public SimpleCommand<object, object> AdminModeCommand
	{
		get
		{
			if (adminModeCommand == null)
			{
				adminModeCommand = new SimpleCommand<object, object>(OnCanSwitchShellCommand, OnSwitchShellCommand);
			}
			return adminModeCommand;
		}
	}

	public SimpleCommand<object, object> ExitCommand
	{
		get
		{
			if (exitCommand == null)
			{
				exitCommand = new SimpleCommand<object, object>(OnCanExitCommand, OnExitCommand);
			}
			return exitCommand;
		}
	}

	public SimpleCommand<object, object> AcceptCommand
	{
		get
		{
			if (acceptCommand == null)
			{
				acceptCommand = new SimpleCommand<object, object>(OnCanAcceptCommand, OnAcceptCommand);
			}
			return acceptCommand;
		}
	}

	public string Password
	{
		get
		{
			return password;
		}
		set
		{
			password = value;
			RaisePropertyChanged("Password");
			ResetCommands();
		}
	}

	public uint LoginAttempts
	{
		get
		{
			return loginAttempts;
		}
		protected set
		{
			loginAttempts = value;
			RaisePropertyChanged("LoginAttempts");
		}
	}

	public uint MaxAttempts
	{
		get
		{
			return maxAttempts;
		}
		protected set
		{
			maxAttempts = value;
			RaisePropertyChanged("MaxAttempts");
		}
	}

	public bool IsAuthenticated
	{
		get
		{
			return isAuthenticated;
		}
		set
		{
			isAuthenticated = value;
			RaisePropertyChanged("IsAuthenticated");
		}
	}

	public TimeSpan TimeLeft => TimeSpan.FromMilliseconds(maximumTime);

	public ManagerLoginViewModel(GizmoClient client)
		: base(client, new ManagerUI())
	{
	}

	private bool OnCanExitCommand(object parameter)
	{
		return true;
	}

	private void OnExitCommand(object parameter)
	{
		Hide();
	}

	private bool OnCanSwitchShellCommand(object parameter)
	{
		if (IsAuthenticated)
		{
			return !base.Client.IsInMaintenance;
		}
		return false;
	}

	private async void OnSwitchShellCommand(object parameter)
	{
		try
		{
			await base.Client.EnableMaintenanceAsync();
		}
		catch (Exception ex)
		{
			base.Client.Log.AddError("Could not initiate maintenance mode.", ex, LogCategories.Generic);
		}
		finally
		{
			Hide();
		}
	}

	private bool OnCanUninstallCommand(object parameter)
	{
		return IsAuthenticated;
	}

	private void OnUninstallCommand(object parameter)
	{
		try
		{
			bool deactivateOnly = bool.Parse(parameter.ToString());
			base.Client.Uninstall(deactivateOnly);
		}
		catch (Exception ex)
		{
			base.Client.Log.AddError("Could not initiate client uninstall.", ex, LogCategories.Generic);
		}
		finally
		{
			Hide();
		}
	}

	private bool OnCanAcceptCommand(object parameter)
	{
		return !string.IsNullOrWhiteSpace(Password);
	}

	private void OnAcceptCommand(object parameter)
	{
		string text = Password;
		bool flag = false;
		if (!string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(base.Client.Settings.ManagerPassword))
		{
			flag = string.Compare(text, "password", StringComparison.InvariantCultureIgnoreCase) == 0;
		}
		else if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(base.Client.Settings.ManagerPassword))
		{
			flag = SHA1.ValidateString(text, base.Client.Settings.ManagerPassword);
		}
		if (flag)
		{
			IsAuthenticated = true;
			ResetCommands();
			return;
		}
		LoginAttempts++;
		Password = null;
		if (LoginAttempts >= MaxAttempts)
		{
			Hide();
			MessageBoxEx.Show(new IWindowWrapper(base.Client.ShellWindowHandle), "Failed to authenticate. System will now reboot.", "Authentication Failed.", MessageBoxButtons.OK, MessageBoxIcon.Hand, 5000u);
			base.Client.SetPowerState(PowerStates.Reboot);
		}
	}

	protected override void OnInitializeWindow()
	{
		base.Window.Topmost = true;
		base.Window.MinHeight = 0.0;
		base.Window.Width = 400.0;
		base.Window.DataContext = this;
	}

	protected override void OnResetCommands()
	{
		AcceptCommand.RaiseCanExecuteChanged();
		ExitCommand.RaiseCanExecuteChanged();
		UninstallCommand.RaiseCanExecuteChanged();
		AdminModeCommand.RaiseCanExecuteChanged();
	}

	protected override void OnMainWindowClosed(object sender, EventArgs e)
	{
		if (timer != null)
		{
			timer.Dispose();
			timer = null;
		}
	}

	protected override void OnMainWindowLoaded(object sender, RoutedEventArgs e)
	{
		lock (lockObject)
		{
			timer = new System.Threading.Timer(TimerCallBack, null, 1000, 1000);
		}
	}

	protected override void OnCloseWindow()
	{
		if (!base.WasClosed)
		{
			base.AllowClosing = true;
			base.Window.Owner = null;
			base.Window.Close();
			base.WindowHandle = IntPtr.Zero;
		}
	}

	protected override void OnShowWindow(IntPtr owner)
	{
		if (!base.IsLoaded)
		{
			new WindowInteropHelper(base.Window).Owner = owner;
			base.Window.ShowActivated = true;
			base.Window.Topmost = true;
			base.Window.Show();
			base.Window.Focus();
			base.WindowHandle = new WindowInteropHelper(base.Window).Handle;
		}
	}

	private void TimerCallBack(object state)
	{
		lock (lockObject)
		{
			maximumTime -= 1000;
			RaisePropertyChanged("TimeLeft");
			if (IsAuthenticated | (maximumTime <= 0))
			{
				if (timer != null)
				{
					timer.Dispose();
					timer = null;
				}
				if (maximumTime <= 0)
				{
					Hide();
				}
			}
		}
	}
}
