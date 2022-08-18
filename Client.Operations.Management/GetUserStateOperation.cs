using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class GetUserStateOperation : IOperationBase
{
	public GetUserStateOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		RaiseCompleted(GizmoClient.Current.CurrentUser, GizmoClient.Current.LoginState, GizmoClient.Current.IsInMaintenance);
	}
}
