using System;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class ServerSideGraceOperation : IOperationBase
{
	public ServerSideGraceOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			RaiseStarted();
			if (TryGetParameterAt<bool>(1, out var parameter))
			{
				if (parameter)
				{
					if (!TryGetParameterAt<int>(2, out var parameter2))
					{
						parameter2 = 1;
					}
					GizmoClient.Current.OnEnterGracePeriod(parameter2);
				}
				else
				{
					GizmoClient.Current.OnExitGracePeriod();
				}
			}
			RaiseCompleted();
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
