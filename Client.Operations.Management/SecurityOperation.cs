using System;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class SecurityOperation : IOperationBase
{
	public SecurityOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			if (!TryGetParameterAt<SecurityOperations>(0, out var parameter))
			{
				RaiseInvalidParams("operationType");
				return;
			}
			switch (parameter)
			{
			case SecurityOperations.GetState:
				RaiseCompleted(GizmoClient.Current.IsSecurityEnabled);
				break;
			case SecurityOperations.SetState:
			{
				bool parameterAt = GetParameterAt<bool>(1);
				GizmoClient.Current.IsSecurityEnabled = parameterAt;
				RaiseCompleted();
				break;
			}
			default:
				RaiseInvalidParams("operationType");
				break;
			}
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
