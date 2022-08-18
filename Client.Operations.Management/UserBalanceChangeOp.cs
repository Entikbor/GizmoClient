using System;
using ServerService;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class UserBalanceChangeOp : IOperationBase
{
	public UserBalanceChangeOp(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			UsageSessionChangedEventArgs parameter2;
			if (TryGetParameterAt<UserBalanceEventArgs>(1, out var parameter))
			{
				GizmoClient.Current.ProcessEventArgs(parameter);
				RaiseCompleted();
			}
			else if (TryGetParameterAt<UsageSessionChangedEventArgs>(1, out parameter2))
			{
				GizmoClient.Current.ProcessEventArgs(parameter2);
				RaiseCompleted();
			}
			else
			{
				RaiseInvalidParams();
			}
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
