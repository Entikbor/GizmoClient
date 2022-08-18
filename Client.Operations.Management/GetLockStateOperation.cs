using System;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class GetLockStateOperation : IOperationBase
{
	public GetLockStateOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			RaiseCompleted(GizmoClient.Current.IsInputLocked);
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
