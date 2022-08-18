using System;
using SharedLib.Commands;
using SharedLib.Configuration;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class SetSettingsOperation : IOperationBase, IOperation
{
	public SetSettingsOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			if (!TryGetParameterAt<ClientSettings>(1, out var parameter))
			{
				RaiseInvalidParams("clientSettings");
			}
			else
			{
				GizmoClient.Current.SetSettings(parameter, saveSettings: true);
				RaiseCompleted();
			}
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
