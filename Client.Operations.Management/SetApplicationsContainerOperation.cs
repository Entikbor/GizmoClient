using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class SetApplicationsContainerOperation : IOperationBase, IOperation
{
	public SetApplicationsContainerOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		RaiseCompleted();
	}
}
