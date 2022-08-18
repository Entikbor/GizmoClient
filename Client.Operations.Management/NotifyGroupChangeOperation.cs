using System;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class NotifyGroupChangeOperation : IOperationBase
{
	public NotifyGroupChangeOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			GizmoClient.Current.OnResetGroupConfiguration();
			RaiseCompleted();
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
