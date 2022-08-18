using Client.Operations.Management;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Parsers;

public class PowerManagementCommandParser : CommandParserBase
{
	public override bool TryParse(IDispatcherCommand cmd)
	{
		if (cmd.Type != CommandType.PowerManagement)
		{
			return false;
		}
		if (cmd.ParamsArray.Length < 1)
		{
			return false;
		}
		cmd.Operation = new ClientPowerManagementOperation(cmd);
		return true;
	}
}
