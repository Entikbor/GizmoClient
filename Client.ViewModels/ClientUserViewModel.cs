using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Client.Views;
using GizmoShell;
using ServerService;
using SharedLib;
using SharedLib.Dispatcher.Exceptions;
using SharedLib.User;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
public class ClientUserViewModel : ViewModel, IPartImportsSatisfiedNotification
{
	private string username;

	private bool canChangePassword;

	private bool isIdle;

	private bool isLoggedIn;

	private bool hasPendingUserAgreements;

	private decimal deposits;

	private decimal balance;

	private decimal outstanding;

	private double? time;

	private string activeTimeSourceName;

	private UsageType currentUsage;

	private int points;

	private IExecutionChangedAwareCommand userSettingsCommand;

	private IExecutionChangedAwareCommand changePasswordCommand;

	[Import]
	private IClinetCompositionService CompositionService { get; set; }

	[Import(AllowDefault = true)]
	private IDialogService DialogService { get; set; }

	[Import(AllowDefault = true)]
	private IShellWindow Shell { get; set; }

	[Import]
	private GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand UserSettingsCommand
	{
		get
		{
			if (userSettingsCommand == null)
			{
				userSettingsCommand = new SimpleCommand<object, object>(OnCanUserSettingsCommand, OnUserSettingsCommand);
			}
			return userSettingsCommand;
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand ChangePasswordCommand
	{
		get
		{
			if (changePasswordCommand == null)
			{
				changePasswordCommand = new SimpleCommand<object, object>(OnCanChangePasswordCommand, OnChangePasswordCommand);
			}
			return changePasswordCommand;
		}
	}

	[IgnorePropertyModification]
	public string Username
	{
		get
		{
			return username;
		}
		set
		{
			SetProperty(ref username, value, "Username");
		}
	}

	[IgnorePropertyModification]
	public bool CanChangePassword
	{
		get
		{
			return canChangePassword;
		}
		set
		{
			SetProperty(ref canChangePassword, value, "CanChangePassword");
			RaisePropertyChanged("ShowOptionsButton");
		}
	}

	[IgnorePropertyModification]
	public bool ShowOptionsButton
	{
		get
		{
			if (!CanChangePassword)
			{
				return Client.AllowUserLock;
			}
			return true;
		}
	}

	[IgnorePropertyModification]
	public bool IsIdle
	{
		get
		{
			return isIdle;
		}
		set
		{
			SetProperty(ref isIdle, value, "IsIdle");
		}
	}

	[IgnorePropertyModification]
	public decimal Deposits
	{
		get
		{
			return deposits;
		}
		set
		{
			SetProperty(ref deposits, value, "Deposits");
		}
	}

	[IgnorePropertyModification]
	public decimal Balance
	{
		get
		{
			return balance;
		}
		set
		{
			SetProperty(ref balance, value, "Balance");
		}
	}

	[IgnorePropertyModification]
	public double? Time
	{
		get
		{
			return time;
		}
		set
		{
			SetProperty(ref time, value, "Time");
			RaisePropertyChanged("TimeFormatted");
		}
	}

	[IgnorePropertyModification]
	public int Points
	{
		get
		{
			return points;
		}
		set
		{
			SetProperty(ref points, value, "Points");
		}
	}

	[IgnorePropertyModification]
	public string TimeFormatted
	{
		get
		{
			if (!Time.HasValue)
			{
				return null;
			}
			TimeSpan timeSpan = TimeSpan.FromSeconds(Time.GetValueOrDefault());
			return $"{(int)timeSpan.TotalHours:00}:{timeSpan.Minutes:00}";
		}
	}

	[IgnorePropertyModification]
	public decimal Outstanding
	{
		get
		{
			return outstanding;
		}
		set
		{
			SetProperty(ref outstanding, value, "Outstanding");
		}
	}

	[IgnorePropertyModification]
	public string ActiveTimeSourceName
	{
		get
		{
			return activeTimeSourceName;
		}
		private set
		{
			SetProperty(ref activeTimeSourceName, value, "ActiveTimeSourceName");
		}
	}

	[IgnorePropertyModification]
	public UsageType ActiveUsageType
	{
		get
		{
			return currentUsage;
		}
		private set
		{
			SetProperty(ref currentUsage, value, "ActiveUsageType");
		}
	}

	[IgnorePropertyModification]
	public bool IsLoggedIn
	{
		get
		{
			return isLoggedIn;
		}
		set
		{
			SetProperty(ref isLoggedIn, value, "IsLoggedIn");
		}
	}

	[IgnorePropertyModification]
	public bool HasPendingUserAgreements
	{
		get
		{
			return hasPendingUserAgreements;
		}
		set
		{
			SetProperty(ref hasPendingUserAgreements, value, "HasPendingUserAgreements");
		}
	}

	private bool OnCanUserSettingsCommand(object param)
	{
		return false;
	}

	private async void OnUserSettingsCommand(object param)
	{
		try
		{
			IUserSettingsView exportedValue = CompositionService.GetExportedValue<IUserSettingsView>();
			await Shell.ShowOverlayAsync(exportedValue);
		}
		catch
		{
		}
	}

	private bool OnCanChangePasswordCommand(object param)
	{
		return true;
	}

	private async void OnChangePasswordCommand(object param)
	{
		await UserPasswordChangeAsync();
	}

	private async Task UserPasswordChangeAsync()
	{
		try
		{
			CancellationTokenSource cts = new CancellationTokenSource();
			SimpleCommand<object, object> cancelCommand = new SimpleCommand<object, object>((object o) => true, delegate
			{
				cts.Cancel();
			});
			try
			{
				IUserPasswordEditViewModel passwordEditModel = CompositionService.GetExportedValue<IUserPasswordEditViewModel>();
				passwordEditModel.ShowOldPassword = true;
				passwordEditModel.CancelCommand = cancelCommand;
				passwordEditModel.AcceptCommand = new SimpleCommand<object, object>((object o) => passwordEditModel.IsValid, async delegate
				{
					passwordEditModel.Validate();
					if (!passwordEditModel.Validate() || !passwordEditModel.IsValid)
					{
						return;
					}
					bool hidden = false;
					try
					{
						string oldPassword = passwordEditModel.OldPassword;
						if (string.IsNullOrWhiteSpace(oldPassword))
						{
							return;
						}
						string password = passwordEditModel.Password;
						if (string.IsNullOrWhiteSpace(password))
						{
							return;
						}
						await Client.ChangeUserPasswordAsync(oldPassword, password);
					}
					catch (AccessDeniedException ex3)
					{
						_ = ex3;
						Shell.HideCurrentOverlay();
						hidden = true;
						string localizedString = Client.GetLocalizedString("WRONG_PASSWORD");
						await (DialogService?.ShowAcceptDialogAsync(localizedString));
					}
					catch (Exception)
					{
						throw;
					}
					finally
					{
						if (!hidden)
						{
							Shell.HideCurrentOverlay();
						}
					}
				});
				IUserPasswordEditView content = await Application.Current.Dispatcher.InvokeAsync(delegate
				{
					IUserPasswordEditView exportedValue = CompositionService.GetExportedValue<IUserPasswordEditView>();
					exportedValue.DataContext = passwordEditModel;
					return exportedValue;
				});
				await Shell.ShowOverlayAsync(content, allowClosing: false, cts.Token);
			}
			catch (OperationCanceledException)
			{
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task UserEditAsync(UserInfoTypes requiredInfo)
	{
		try
		{
			CancellationTokenSource cts = new CancellationTokenSource();
			SimpleCommand<object, object> cancelCommand = new SimpleCommand<object, object>((object o) => true, delegate
			{
				cts.Cancel();
			});
			if (requiredInfo.HasFlag(UserInfoTypes.Password))
			{
				try
				{
					IUserPasswordEditViewModel passwordEditModel = CompositionService.GetExportedValue<IUserPasswordEditViewModel>();
					passwordEditModel.CancelCommand = cancelCommand;
					passwordEditModel.AcceptCommand = new SimpleCommand<object, object>((object o) => passwordEditModel.IsValid, async delegate
					{
						passwordEditModel.Validate();
						if (!passwordEditModel.Validate() || !passwordEditModel.IsValid)
						{
							return;
						}
						try
						{
							string password = passwordEditModel.Password;
							if (!string.IsNullOrWhiteSpace(password))
							{
								await Client.SetUserPasswordAsync(password);
							}
						}
						catch
						{
							throw;
						}
						finally
						{
							Shell.HideCurrentOverlay();
						}
					});
					IUserPasswordEditView content = await Application.Current.Dispatcher.InvokeAsync(delegate
					{
						IUserPasswordEditView exportedValue2 = CompositionService.GetExportedValue<IUserPasswordEditView>();
						exportedValue2.DataContext = passwordEditModel;
						return exportedValue2;
					});
					await Shell.ShowOverlayAsync(content, allowClosing: false, cts.Token);
				}
				catch (OperationCanceledException)
				{
				}
			}
			if (requiredInfo == UserInfoTypes.None || requiredInfo == UserInfoTypes.Password)
			{
				return;
			}
			try
			{
				IUserProfileEditViewModel userProfileEditViewModel = CompositionService.GetExportedValue<IUserProfileEditViewModel>();
				userProfileEditViewModel.RequiredUserInfo = requiredInfo;
				userProfileEditViewModel.CancelCommand = cancelCommand;
				userProfileEditViewModel.AcceptCommand = new SimpleCommand<object, object>((object o) => userProfileEditViewModel.IsValid, async delegate
				{
					if (!userProfileEditViewModel.Validate() || !userProfileEditViewModel.IsValid)
					{
						return;
					}
					try
					{
						IUserProfile userInfoAsync = userProfileEditViewModel.ToSource();
						await Client.SetUserInfoAsync(userInfoAsync);
					}
					catch
					{
						throw;
					}
					finally
					{
						Shell.HideCurrentOverlay();
					}
				});
				IUserProfile userProfile = await Client.UserProfileGetAsync();
				userProfile.BirthDate = ((userProfile.BirthDate == DateTime.MinValue) ? DateTime.Now : userProfile.BirthDate);
				userProfileEditViewModel.FromSource(userProfile);
				userProfileEditViewModel.ResetValidation();
				IUserProfileEditView content2 = await Application.Current.Dispatcher.InvokeAsync(delegate
				{
					IUserProfileEditView exportedValue = CompositionService.GetExportedValue<IUserProfileEditView>();
					exportedValue.DataContext = userProfileEditViewModel;
					return exportedValue;
				});
				await Shell.ShowOverlayAsync(content2, allowClosing: false, cts.Token);
			}
			catch (OperationCanceledException)
			{
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private void SetUsernameByProfile(IUserProfile profile)
	{
		bool flag = profile != null && profile.Role == UserRoles.Guest;
		Username = (flag ? Client.GetLocalizedString("USER_GUEST") : profile?.UserName);
		CanChangePassword = profile?.CanChangePassword ?? false;
	}

	private void Client_UserAgreementsLoaded(object sender, UserAgreementsLoadedEventArgs e)
	{
		HasPendingUserAgreements = e.HasPendingUserAgreements;
	}

	private void OnUserIdleChange(object sender, UserIdleEventArgs e)
	{
		IsIdle = e.IsIdle;
	}

	private void OnUserProfileChange(object sender, UserProfileChangeArgs e)
	{
		SetUsernameByProfile(e.NewProfile);
	}

	private async void OnLoginStateChange(object sender, UserEventArgs e)
	{
		IsLoggedIn = e.State == LoginState.LoginCompleted;
		if (!IsLoggedIn)
		{
			Client.OnExitGracePeriod();
			Client.OnExitUserLock();
		}
		try
		{
			if (e.State == LoginState.LoginCompleted)
			{
				SetUsernameByProfile(e.UserProfile);
				if (e.IsUserInfoRequired || e.IsUserPasswordRequired)
				{
					await UserEditAsync(e.RequiredUserInformation);
				}
			}
			else
			{
				Username = null;
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex2)
		{
			Client.TraceWrite(ex2, "OnLoginStateChange", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\UserViewModel.cs", 515);
		}
	}

	private void OnUsageSessionChanged(object sender, UsageSessionChangedEventArgs e)
	{
		ActiveTimeSourceName = e.CurrentTimeProduct;
		ActiveUsageType = e.CurrentUsageType;
	}

	private void OnUserBalanceChange(object sender, UserBalanceEventArgs e)
	{
		Points = e.Balance.Points;
		Time = e.Balance.AvailableCreditedTime;
		Deposits = e.Balance.Deposits;
		Balance = e.Balance.Balance;
		Outstanding = e.Balance.TotalOutstanding;
	}

	public void OnImportsSatisfied()
	{
		Client.UserBalanceChange -= OnUserBalanceChange;
		Client.UsageSessionChanged -= OnUsageSessionChanged;
		Client.LoginStateChange -= OnLoginStateChange;
		Client.UserProfileChange -= OnUserProfileChange;
		Client.UserIdleChange -= OnUserIdleChange;
		Client.UserAgreementsLoaded -= Client_UserAgreementsLoaded;
		Client.UserBalanceChange += OnUserBalanceChange;
		Client.UsageSessionChanged += OnUsageSessionChanged;
		Client.LoginStateChange += OnLoginStateChange;
		Client.UserProfileChange += OnUserProfileChange;
		Client.UserIdleChange += OnUserIdleChange;
		Client.UserAgreementsLoaded += Client_UserAgreementsLoaded;
		IsIdle = Client.IsUserIdle;
		IsLoggedIn = Client.IsUserLoggedIn;
		SetUsernameByProfile(Client.CurrentUser);
	}
}
