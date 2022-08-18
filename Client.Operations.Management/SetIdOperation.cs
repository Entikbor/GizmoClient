using System;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class SetIdOperation : IOperationBase, IOperation
{
	public SetIdOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			if (!TryGetParameterAt<int>(1, out var parameter))
			{
				RaiseInvalidParams("clientId");
			}
			else
			{
				GizmoClient.Current.Id = parameter;
				RaiseCompleted();
			}
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
