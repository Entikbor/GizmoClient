using System;
using System.Collections.Generic;
using SharedLib.Commands;
using SharedLib.Dispatcher;
using SharedLib.Management;

namespace Client.Operations.Management;

public class SetSecurityProfileListOperation : IOperationBase
{
	public SetSecurityProfileListOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			if (!TryGetParameterAt<List<ISecurityProfile>>(1, out var parameter))
			{
				RaiseInvalidParams("securityProfileList");
				return;
			}
			if (!TryGetParameterAt<bool>(2, out var parameter2))
			{
				parameter2 = false;
			}
			GizmoClient.Current.SetSecurityProfiles(parameter, parameter2);
			RaiseCompleted();
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
