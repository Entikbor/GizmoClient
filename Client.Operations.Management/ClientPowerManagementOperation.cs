using System;
using CoreLib;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class ClientPowerManagementOperation : IOperationBase, IOperation
{
	public ClientPowerManagementOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			if (base.Command.ParamsArray.Length >= 2)
			{
				PowerStates parameterAt = GetParameterAt<PowerStates>(0);
				GetParameterAt<bool>(1);
				RaiseCompleted(true);
				GizmoClient.Current.SetPowerState(parameterAt);
			}
			else
			{
				RaiseInvalidParams("Count");
			}
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
