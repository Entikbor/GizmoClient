using System;
using ServerService;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class AppEventOp : IOperationBase, IOperation
{
	public AppEventOp(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			RaiseStarted();
			if (TryGetParameterAt<AppRatedEventArgs>(1, out var parameter))
			{
				GizmoClient.Current.ProcessEventArgs(parameter);
			}
			else
			{
				if (!TryGetParameterAt<AppStatEventArgs>(1, out var parameter2))
				{
					RaiseInvalidParams("args");
					return;
				}
				GizmoClient.Current.ProcessEventArgs(parameter2);
			}
			RaiseCompleted();
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
