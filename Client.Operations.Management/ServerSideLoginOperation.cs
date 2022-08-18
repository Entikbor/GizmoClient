using System;
using IntegrationLib;
using SharedLib;
using SharedLib.Commands;
using SharedLib.Dispatcher;
using SharedLib.User;

namespace Client.Operations.Management;

public class ServerSideLoginOperation : IOperationBase
{
	public ServerSideLoginOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			if (!TryGetParameterAt<IUserIdentity>(1, out var parameter))
			{
				RaiseInvalidParams("identity");
				return;
			}
			if (!TryGetParameterAt<IUserProfile>(2, out var parameter2))
			{
				RaiseInvalidParams("userProfile");
				return;
			}
			if (!TryGetParameterAt<UserInfoTypes>(3, out var parameter3))
			{
				parameter3 = UserInfoTypes.None;
			}
			RaiseStarted();
			GizmoClient.Current.OnUserLogin(parameter, parameter2, parameter3);
			RaiseCompleted();
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
