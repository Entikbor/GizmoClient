using System.ComponentModel.Composition;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
public class ClientUserLockViewModel : ViewModel, IPartImportsSatisfiedNotification
{
	private bool isLocked;

	private string message;

	private IExecutionChangedAwareCommand lockCommand;

	private IExecutionChangedAwareCommand clickNumberCommand;

	private IExecutionChangedAwareCommand clearPasswordCommand;

	private IExecutionChangedAwareCommand deleteNumberCommand;

	private IExecutionChangedAwareCommand cancelLockCommand;

	private IExecutionChangedAwareCommand acceptLockCommand;

	private bool accepted;

	private string inputPassword = string.Empty;

	private string password = string.Empty;

	[Import]
	public GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public bool IsLocked
	{
		get
		{
			return isLocked;
		}
		protected set
		{
			SetProperty(ref isLocked, value, "IsLocked");
		}
	}

	public string Message
	{
		get
		{
			return message;
		}
		protected set
		{
			SetProperty(ref message, value, "Message");
		}
	}

	public string InputPassword
	{
		get
		{
			return inputPassword;
		}
		protected set
		{
			SetProperty(ref inputPassword, value, "InputPassword");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand LockCommand
	{
		get
		{
			if (lockCommand == null)
			{
				lockCommand = new SimpleCommand<object, object>(OnCanLockCommand, OnLockCommand);
			}
			return lockCommand;
		}
		internal set
		{
			SetProperty(ref lockCommand, value, "LockCommand");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand ClickNumberCommand
	{
		get
		{
			if (clickNumberCommand == null)
			{
				clickNumberCommand = new SimpleCommand<object, object>(OnCanClickNumberCommand, OnClickNumberCommand);
			}
			return clickNumberCommand;
		}
		internal set
		{
			SetProperty(ref clickNumberCommand, value, "ClickNumberCommand");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand ClearPasswordCommand
	{
		get
		{
			if (clearPasswordCommand == null)
			{
				clearPasswordCommand = new SimpleCommand<object, object>(OnCanClearPasswordCommand, OnClearPasswordCommand);
			}
			return clearPasswordCommand;
		}
		internal set
		{
			SetProperty(ref clearPasswordCommand, value, "ClearPasswordCommand");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand DeleteNumberCommand
	{
		get
		{
			if (deleteNumberCommand == null)
			{
				deleteNumberCommand = new SimpleCommand<object, object>(OnCanDeleteNumberCommand, OnDeleteNumberCommand);
			}
			return deleteNumberCommand;
		}
		internal set
		{
			SetProperty(ref deleteNumberCommand, value, "DeleteNumberCommand");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand CancelLockCommand
	{
		get
		{
			if (cancelLockCommand == null)
			{
				cancelLockCommand = new SimpleCommand<object, object>(OnCanCancelLockCommand, OnCancelLockCommand);
			}
			return cancelLockCommand;
		}
		internal set
		{
			SetProperty(ref cancelLockCommand, value, "CancelLockCommand");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand AcceptLockCommand
	{
		get
		{
			if (acceptLockCommand == null)
			{
				acceptLockCommand = new SimpleCommand<object, object>(OnCanAcceptLockCommand, OnAcceptLockCommand);
			}
			return acceptLockCommand;
		}
		internal set
		{
			SetProperty(ref acceptLockCommand, value, "AcceptLockCommand");
		}
	}

	private bool OnCanLockCommand(object param)
	{
		return true;
	}

	private void OnLockCommand(object param)
	{
		Client.OnEnterUserLock();
	}

	private bool OnCanClickNumberCommand(object param)
	{
		return InputPassword.Length < 6;
	}

	private void OnClickNumberCommand(object param)
	{
		InputPassword += param;
		ResetCommands();
	}

	private bool OnCanClearPasswordCommand(object param)
	{
		return InputPassword.Length > 0;
	}

	private void OnClearPasswordCommand(object param)
	{
		InputPassword = string.Empty;
		ResetCommands();
	}

	private bool OnCanDeleteNumberCommand(object param)
	{
		return InputPassword.Length > 0;
	}

	private void OnDeleteNumberCommand(object param)
	{
		if (InputPassword.Length > 0)
		{
			InputPassword = InputPassword.Substring(0, InputPassword.Length - 1);
		}
		ResetCommands();
	}

	private bool OnCanCancelLockCommand(object param)
	{
		return !accepted;
	}

	private void OnCancelLockCommand(object param)
	{
		Client.OnExitUserLock();
	}

	private bool OnCanAcceptLockCommand(object param)
	{
		return InputPassword.Length == 6;
	}

	private void OnAcceptLockCommand(object param)
	{
		if (accepted)
		{
			if (password == InputPassword)
			{
				Client.OnExitUserLock();
			}
			else
			{
				InputPassword = string.Empty;
				Message = Client.GetLocalizedString("WRONG_PASSWORD");
			}
		}
		else
		{
			password = InputPassword;
			InputPassword = string.Empty;
			accepted = true;
			Message = Client.GetLocalizedString("ENTER_PASSWORD_TO_UNLOCK");
		}
		ResetCommands();
	}

	protected override void OnResetCommands()
	{
		base.OnResetCommands();
		ClickNumberCommand.RaiseCanExecuteChanged();
		ClearPasswordCommand.RaiseCanExecuteChanged();
		DeleteNumberCommand.RaiseCanExecuteChanged();
		CancelLockCommand.RaiseCanExecuteChanged();
		AcceptLockCommand.RaiseCanExecuteChanged();
	}

	private void OnClientUserLockChange(object sender, UserLockChangeEventArgs e)
	{
		IsLocked = e.IsLocked;
		if (!IsLocked)
		{
			Message = Client.GetLocalizedString("ENTER_PASSWORD_TO_LOCK");
			password = string.Empty;
			InputPassword = string.Empty;
			accepted = false;
		}
		ResetCommands();
	}

	public void OnImportsSatisfied()
	{
		Message = Client.GetLocalizedString("ENTER_PASSWORD_TO_LOCK");
		Client.UserLockChange += OnClientUserLockChange;
	}
}
