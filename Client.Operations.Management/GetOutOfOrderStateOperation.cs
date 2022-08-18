using System;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class GetOutOfOrderStateOperation : IOperationBase
{
	public GetOutOfOrderStateOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			RaiseCompleted(GizmoClient.Current.IsOutOfOrder);
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
