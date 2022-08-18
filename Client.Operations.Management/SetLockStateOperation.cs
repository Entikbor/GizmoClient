using System;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class SetLockStateOperation : IOperationBase
{
	public SetLockStateOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			if (!TryGetParameterAt<bool>(1, out var parameter))
			{
				RaiseInvalidParams("lockState");
			}
			else
			{
				GizmoClient.Current.IsInputLocked = parameter;
				RaiseCompleted();
			}
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
