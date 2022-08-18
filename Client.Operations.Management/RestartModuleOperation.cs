using System;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class RestartModuleOperation : IOperationBase, IOperation
{
	public RestartModuleOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			RaiseCompleted();
			GizmoClient.Current.Restart();
		}
		catch (Exception ex)
		{
			RaiseFailed(ex.Message);
		}
	}
}
