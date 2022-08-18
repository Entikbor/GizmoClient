using System;
using System.ComponentModel.Composition;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using GizmoShell;
using IntegrationLib;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
public class UserLoginViewModel : ValidateViewModelBase<NullView>, IPartImportsSatisfiedNotification
{
	private string username;

	private string password;

	private string currentUserName;

	private string failMessage;

	private IExecutionChangedAwareCommand loginCommand;

	private IExecutionChangedAwareCommand logoutCommand;

	private IExecutionChangedAwareCommand registerCommand;

	private int computerNumber;

	private LoginProgressState loginProgressState;

	private bool isUserLoggedIn;

	private Timer STATE_CHANGE_TIMER;

	private int? currentUserId;

	private bool registrationEnabled;

	private readonly object LOGIN_STATE_LOCK = new object();

	[Import]
	private IClinetCompositionService CompositionService { get; set; }

	[Import(AllowDefault = true, AllowRecomposition = true)]
	private IDialogService DialogService { get; set; }

	[Import(AllowDefault = true, AllowRecomposition = true)]
	private IShellWindow Shell { get; set; }

	[Import]
	private GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand LoginCommand
	{
		get
		{
			if (loginCommand == null)
			{
				loginCommand = new SimpleCommand<object, object>(OnCanLoginCommand, OnLoginCommand);
			}
			return loginCommand;
		}
		internal set
		{
			SetProperty(ref loginCommand, value, "LoginCommand");
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

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand RegisterCommand
	{
		get
		{
			if (registerCommand == null)
			{
				registerCommand = new SimpleCommand<object, object>(OnCanRegisterCommand, OnRegisterCommand);
			}
			return registerCommand;
		}
	}

	[IgnorePropertyModification]
	public LoginProgressState LoginProgressState
	{
		get
		{
			return loginProgressState;
		}
		private set
		{
			SetProperty(ref loginProgressState, value, "LoginProgressState");
			RaisePropertyChanged("LoginProgressStateIndex");
		}
	}

	[IgnorePropertyModification]
	public int LoginProgressStateIndex => (int)LoginProgressState;

	[IgnorePropertyModification]
	public int ComputerNumber
	{
		get
		{
			return computerNumber;
		}
		private set
		{
			SetProperty(ref computerNumber, value, "ComputerNumber");
		}
	}

	[MaxLength(45)]
	[Required]
	[IgnorePropertyModification]
	public string Username
	{
		get
		{
			return username;
		}
		set
		{
			SetPropertyAndValidate(ref username, value, "Username");
		}
	}

	[MaxLength(45)]
	[IgnorePropertyModification]
	public string Password
	{
		get
		{
			return password;
		}
		set
		{
			SetPropertyAndValidate(ref password, value, "Password");
		}
	}

	[IgnorePropertyModification]
	public bool IsUserLoggedIn
	{
		get
		{
			return isUserLoggedIn;
		}
		private set
		{
			SetProperty(ref isUserLoggedIn, value, "IsUserLoggedIn");
		}
	}

	[IgnorePropertyModification]
	public string FailMessage
	{
		get
		{
			return failMessage;
		}
		set
		{
			SetProperty(ref failMessage, value, "FailMessage");
		}
	}

	[IgnorePropertyModification]
	public int? CurrentUserId
	{
		get
		{
			return currentUserId;
		}
		private set
		{
			SetProperty(ref currentUserId, value, "CurrentUserId");
		}
	}

	[IgnorePropertyModification]
	public string CurrentUserName
	{
		get
		{
			return currentUserName;
		}
		private set
		{
			SetProperty(ref currentUserName, value, "CurrentUserName");
		}
	}

	[IgnorePropertyModification]
	public bool CanLogout
	{
		get
		{
			if (!Client.Settings.DisplayLogoutButton)
			{
				return false;
			}
			if (!Client.IsUserLoggedIn)
			{
				return false;
			}
			return true;
		}
	}

	[IgnorePropertyModification]
	public bool IsRegistrationEnabled
	{
		get
		{
			return registrationEnabled;
		}
		set
		{
			SetProperty(ref registrationEnabled, value, "IsRegistrationEnabled");
		}
	}

	private bool OnCanLoginCommand(object param)
	{
		return true;
	}

	private async void OnLoginCommand(object param)
	{
		if (Validate())
		{
			await Client.LoginAsync(Username, Password, allowEmptyPasswords: true);
		}
	}

	private bool OnCanLogoutCommand(object param)
	{
		return CanLogout;
	}

	private async void OnLogoutCommand(object param)
	{
		try
		{
			string localizedString = Client.GetLocalizedString("MESSAGE_CONFIRM_LOGOUT");
			if (!(await DialogService.ShowAcceptDialogAsync(localizedString, MessageDialogButtons.AcceptCancel)))
			{
				return;
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex2)
		{
			Client.TraceWrite(ex2, "OnLogoutCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\UserLoginViewModel.cs", 232);
		}
		await Client.LogoutAsync();
	}

	private bool OnCanRegisterCommand(object param)
	{
		return IsRegistrationEnabled;
	}

	private void OnRegisterCommand(object param)
	{
		try
		{
			ClearUserCredentialsInput();
			RegistrationViewModel exportedValue = CompositionService.GetExportedValue<RegistrationViewModel>();
			Shell.ShowOverlayAsync(exportedValue.View, allowClosing: true);
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnRegisterCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\UserLoginViewModel.cs", 254);
		}
	}

	private void ClearUserCredentialsInput()
	{
		Username = null;
		RemoveError("Username");
		RaiseErrorsChanged("Username");
		Password = null;
		RemoveError("Password");
		RaiseErrorsChanged("Password");
	}

	private void OnUserIdleChange(object sender, UserIdleEventArgs e)
	{
		if (!Client.IsUserLoggedIn && e.IsIdle)
		{
			ClearUserCredentialsInput();
		}
	}

	private void OnStateChnageTimerCallBack(object state)
	{
		if (!Monitor.TryEnter(LOGIN_STATE_LOCK))
		{
			return;
		}
		try
		{
			LoginProgressState = LoginProgressState.Initial;
			FailMessage = null;
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnStateChnageTimerCallBack", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\UserLoginViewModel.cs", 297);
		}
		finally
		{
			Monitor.Exit(LOGIN_STATE_LOCK);
		}
	}

	private void OnLoginStateChange(object sender, UserEventArgs e)
	{
		Monitor.Enter(LOGIN_STATE_LOCK);
		try
		{
			STATE_CHANGE_TIMER?.Dispose();
			STATE_CHANGE_TIMER = null;
			switch (e.State)
			{
			case LoginState.LoggingIn:
				LoginProgressState = LoginProgressState.Login;
				Shell.HideCurrentOverlay(cancel: true);
				break;
			case LoginState.LoginCompleted:
				LoginProgressState = LoginProgressState.Initial;
				IsUserLoggedIn = true;
				CurrentUserId = e.UserProfile?.Id;
				CurrentUserName = e.UserProfile?.UserName;
				Username = null;
				Password = null;
				base.ValidationErrors.Clear();
				RaiseErrorsChanged("Username");
				RaiseErrorsChanged("Password");
				break;
			case LoginState.LoggingOut:
				IsUserLoggedIn = false;
				LoginProgressState = LoginProgressState.Logout;
				break;
			case LoginState.LoggedOut:
				LoginProgressState = LoginProgressState.Initial;
				IsUserLoggedIn = false;
				CurrentUserId = null;
				CurrentUserName = null;
				break;
			case LoginState.LoginFailed:
			{
				LoginProgressState = LoginProgressState.Failed;
				string localizedObject = Client.GetLocalizedObject<string>(e.FailReason);
				FailMessage = localizedObject;
				switch (e.FailReason)
				{
				case LoginResult.InvalidUserName:
					Username = null;
					Password = null;
					break;
				case LoginResult.InvalidPassword:
					Password = null;
					break;
				default:
					Username = null;
					Password = null;
					break;
				}
				base.ValidationErrors.Clear();
				STATE_CHANGE_TIMER = STATE_CHANGE_TIMER ?? new Timer(OnStateChnageTimerCallBack);
				STATE_CHANGE_TIMER.Change(5000, -1);
				break;
			}
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnLoginStateChange", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\UserLoginViewModel.cs", 375);
		}
		finally
		{
			Monitor.Exit(LOGIN_STATE_LOCK);
		}
	}

	private void OnUserGroupConfigurationChange(object sender, EventArgs e)
	{
		ResetCommands();
		RaisePropertyChanged("CanLogout");
	}

	protected override void OnResetCommands()
	{
		base.OnResetCommands();
		LoginCommand?.RaiseCanExecuteChanged();
		LogoutCommand?.RaiseCanExecuteChanged();
	}

	public void OnImportsSatisfied()
	{
		Client.LoginStateChange -= OnLoginStateChange;
		Client.LoginStateChange += OnLoginStateChange;
		Client.GroupConfigurationChange -= OnUserGroupConfigurationChange;
		Client.GroupConfigurationChange += OnUserGroupConfigurationChange;
		Client.UserIdleChange -= OnUserIdleChange;
		Client.UserIdleChange += OnUserIdleChange;
		CurrentUserId = Client.CurrentUser?.Id;
		CurrentUserName = Client.CurrentUser?.UserName;
		IsRegistrationEnabled = Client.Settings?.IsClientRegistrationEnabled ?? false;
	}
}
