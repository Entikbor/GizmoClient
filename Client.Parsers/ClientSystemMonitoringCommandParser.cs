using SharedLib.Commands;
using SharedLib.Dispatcher;
using SharedLib.Dispatcher.Operations;

namespace Client.Parsers;

public class ClientSystemMonitoringCommandParser : CommandParserBase
{
	public override bool TryParse(IDispatcherCommand cmd)
	{
		if (cmd.Type != CommandType.SystemMonitoring)
		{
			return false;
		}
		if (cmd.ParamsArray.Length < 1)
		{
			return false;
		}
		if ((SystemMonitorTypes)cmd.ParamsArray[0] == SystemMonitorTypes.Monitor)
		{
			cmd.Operation = new GetScreenImage(cmd);
			return true;
		}
		return false;
	}
}
