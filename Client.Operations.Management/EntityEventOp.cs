using System;
using GizmoDALV2;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class EntityEventOp : IOperationBase, IOperation
{
	public EntityEventOp(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			RaiseStarted();
			if (TryGetParameterAt<IEntityEventArgs>(1, out var parameter))
			{
				GizmoClient.Current.ProcessEventArgs(parameter);
				RaiseCompleted();
			}
			else
			{
				RaiseInvalidParams("args");
			}
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
