using System;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class MaintenanceGetOperation : IOperationBase
{
	public MaintenanceGetOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			RaiseStarted();
			RaiseCompleted(GizmoClient.Current.IsInMaintenance);
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
