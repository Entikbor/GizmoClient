using Client.Operations.Management;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Parsers;

public class SecurityCommandParser : CommandParserBase
{
	public override bool TryParse(IDispatcherCommand cmd)
	{
		if (cmd.Type != CommandType.SecurityManagement)
		{
			return false;
		}
		if (cmd.ParamsArray.Length < 1)
		{
			return false;
		}
		cmd.Operation = new SecurityOperation(cmd);
		return true;
	}
}
