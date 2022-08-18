#define TRACE
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class ClientReadyOperation : IOperationBase
{
	public ClientReadyOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			Task.Run(delegate
			{
				GizmoClient.Current.Ready();
			}).ContinueWith(delegate(Task task)
			{
				if (task.IsFaulted)
				{
					Trace.WriteLine($"ClientReadyOperation:Execute() {task.Exception}");
				}
			}, TaskContinuationOptions.OnlyOnFaulted).ConfigureAwait(continueOnCapturedContext: false);
			RaiseCompleted();
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
