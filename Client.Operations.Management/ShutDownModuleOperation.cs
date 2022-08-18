using System;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class ShutDownModuleOperation : IOperationBase, IOperation
{
	public ShutDownModuleOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			RaiseCompleted();
			GizmoClient.Current.Stop();
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
