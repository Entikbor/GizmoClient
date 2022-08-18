using System;
using System.Windows;
using SharedLib;
using SharedLib.Commands;
using SharedLib.Dispatcher;
using SharedLib.ViewModels;

namespace Client.Operations.Management;

public class UINotificationOperation : IOperationBase
{
	public UINotificationOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			RaiseStarted();
			string parameterAt = GetParameterAt<string>(1);
			WindowShowParams parameterAt2 = GetParameterAt<WindowShowParams>(2);
			INotifyWindowViewModel model;
			MessageBoxResult messageBoxResult = GizmoClient.Current.NotifyUser(parameterAt, parameterAt2, out model);
			RaiseCompleted(messageBoxResult);
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
