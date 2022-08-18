using System;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class SetOutOfOrderStateOperation : IOperationBase
{
	public SetOutOfOrderStateOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			if (!TryGetParameterAt<bool>(1, out var parameter))
			{
				RaiseInvalidParams("state");
			}
			else
			{
				GizmoClient.Current.IsOutOfOrder = parameter;
				RaiseCompleted();
			}
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
