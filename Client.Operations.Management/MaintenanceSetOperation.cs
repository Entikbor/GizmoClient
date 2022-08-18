using System;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class MaintenanceSetOperation : IOperationBase
{
	public MaintenanceSetOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			RaiseStarted();
			(GetParameterAt<bool>(1) ? GizmoClient.Current.EnableMaintenanceAsync() : GizmoClient.Current.DisableMaintenanceAsync()).Wait();
			RaiseCompleted();
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
