using System;
using SharedLib;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class ServerSideLogoutOperation : IOperationBase
{
	public ServerSideLogoutOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			RaiseStarted();
			if (!TryGetParameterAt<UserLogoutFlags>(1, out var parameter))
			{
				parameter = UserLogoutFlags.None;
			}
			GizmoClient.Current.OnUserLogout(userInitiated: false, parameter);
			RaiseCompleted();
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
