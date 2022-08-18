using System;
using System.Collections.Generic;
using System.Linq;
using ServerService;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

internal class EventBatchOp : IOperationBase
{
	public EventBatchOp(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			if (!TryGetParameterAt<IEnumerable<EventArgs>>(1, out var parameter))
			{
				RaiseInvalidParams(parameter);
				return;
			}
			foreach (IGrouping<Type, EventArgs> item in from arg in parameter
				group arg by arg.GetType())
			{
				if (!(item.Key == typeof(OrderStatusChangeEventArgs)))
				{
					continue;
				}
				foreach (OrderStatusChangeEventArgs item2 in item.OfType<OrderStatusChangeEventArgs>())
				{
					GizmoClient.Current.ProcessEventArgs(item2);
				}
			}
			RaiseCompleted();
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
	}
}
