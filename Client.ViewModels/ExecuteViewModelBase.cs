using System.ComponentModel.Composition;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public abstract class ExecuteViewModelBase : ViewModel
{
	private IExecutionChangedAwareCommand executeCommand;

	public IExecutionChangedAwareCommand ExecuteCommand
	{
		get
		{
			if (executeCommand == null)
			{
				executeCommand = new SimpleCommand<object, object>(OnCanExecuteCommand, OnExecuteCommand);
			}
			return executeCommand;
		}
		internal set
		{
			SetProperty(ref executeCommand, value, "ExecuteCommand");
		}
	}

	protected virtual bool OnCanExecuteCommand(object param)
	{
		return true;
	}

	protected virtual void OnExecuteCommand(object param)
	{
	}

	protected override void OnResetCommands()
	{
		base.OnResetCommands();
		ExecuteCommand?.RaiseCanExecuteChanged();
	}
}
