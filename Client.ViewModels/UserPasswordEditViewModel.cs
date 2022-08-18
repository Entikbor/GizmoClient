using System.ComponentModel.Composition;
using System.ComponentModel.DataAnnotations;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
[Export(typeof(IUserPasswordEditViewModel))]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class UserPasswordEditViewModel : ValidateViewModelBase<NullView>, IUserPasswordEditViewModel
{
	private bool showOldPassword;

	private string oldPassword;

	private string password;

	private string repeatePassword;

	private IExecutionChangedAwareCommand acceptCommand;

	private IExecutionChangedAwareCommand cancelCommand;

	[Import]
	private GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand AcceptCommand
	{
		get
		{
			return acceptCommand;
		}
		set
		{
			SetProperty(ref acceptCommand, value, "AcceptCommand");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand CancelCommand
	{
		get
		{
			return cancelCommand;
		}
		set
		{
			SetProperty(ref cancelCommand, value, "CancelCommand");
		}
	}

	[IgnorePropertyModification]
	public bool ShowOldPassword
	{
		get
		{
			return showOldPassword;
		}
		set
		{
			SetProperty(ref showOldPassword, value, "ShowOldPassword");
		}
	}

	[MaxLength(20)]
	public string OldPassword
	{
		get
		{
			return oldPassword;
		}
		set
		{
			SetPropertyAndValidate(ref oldPassword, value, "OldPassword");
		}
	}

	[MaxLength(20)]
	[Required]
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

	[MaxLength(20)]
	[Required]
	public string RepeatPassword
	{
		get
		{
			return repeatePassword;
		}
		set
		{
			SetPropertyAndValidate(ref repeatePassword, value, "RepeatPassword");
		}
	}

	protected override void AfterValidatePropery(string propertyName, object value)
	{
		base.AfterValidatePropery(propertyName, value);
		if (propertyName == "OldPassword")
		{
			if (string.IsNullOrWhiteSpace(OldPassword) && ShowOldPassword)
			{
				string localizedString = Client.GetLocalizedString("VE_FIELD_REQUIRED");
				AddError("OldPassword", localizedString);
				RaiseErrorsChanged("OldPassword");
			}
			else
			{
				RemoveError("OldPassword");
				RaiseErrorsChanged("OldPassword");
			}
		}
		if (propertyName == "Password")
		{
			if (string.IsNullOrWhiteSpace(Password))
			{
				AddError("Password", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
				RaiseErrorsChanged("Password");
			}
			else
			{
				RemoveError("Password");
				RaiseErrorsChanged("Password");
			}
		}
		if (propertyName == "RepeatPassword")
		{
			if (string.IsNullOrWhiteSpace(RepeatPassword))
			{
				AddError("RepeatPassword", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
				RaiseErrorsChanged("RepeatPassword");
			}
			else
			{
				RemoveError("RepeatPassword");
				RaiseErrorsChanged("RepeatPassword");
			}
		}
		if ((propertyName == "RepeatPassword" || propertyName == "Password") && !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(RepeatPassword))
		{
			RemoveError("RepeatPassword");
			RaiseErrorsChanged("RepeatPassword");
			if (Password != RepeatPassword)
			{
				string localizedString2 = Client.GetLocalizedString("VE_REPEAT_PASSWORD_MISSMATCH");
				AddError("RepeatPassword", localizedString2);
				RaiseErrorsChanged("RepeatPassword");
			}
		}
	}

	protected override void OnResetCommands()
	{
		base.OnResetCommands();
		AcceptCommand?.RaiseCanExecuteChanged();
		CancelCommand?.RaiseCanExecuteChanged();
	}
}
