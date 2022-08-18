using System;
using System.Collections.Generic;
using SharedLib.Applications;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class SetAppProfileListOperation : IOperationBase
{
	public SetAppProfileListOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			if (!TryGetParameterAt<List<IAppProfile>>(1, out var parameter))
			{
				RaiseInvalidParams("appProfileList");
				return;
			}
			if (!TryGetParameterAt<bool>(2, out var parameter2))
			{
				parameter2 = false;
			}
			GizmoClient.Current.SetAppProfiles(parameter, parameter2);
			RaiseCompleted();
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
