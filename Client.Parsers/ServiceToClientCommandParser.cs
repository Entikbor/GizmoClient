using Client.Operations.Management;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Parsers;

public class ServiceToClientCommandParser : CommandParserBase
{
	public override bool TryParse(IDispatcherCommand cmd)
	{
		if (cmd.Type != CommandType.ServiceToClinet)
		{
			return false;
		}
		if (cmd.ParamsArray.Length < 1)
		{
			return false;
		}
		switch ((ServiceToClientOpType)cmd.ParamsArray[0])
		{
		case ServiceToClientOpType.UserBalanceEvent:
			cmd.Operation = new UserBalanceChangeOp(cmd);
			return true;
		case ServiceToClientOpType.EntityEvent:
			cmd.Operation = new EntityEventOp(cmd);
			return true;
		case ServiceToClientOpType.EventBatch:
			cmd.Operation = new EventBatchOp(cmd);
			return true;
		case ServiceToClientOpType.ReservationChange:
			cmd.Operation = new ReservationChangeOp(cmd);
			return true;
		default:
			return false;
		}
	}
}
