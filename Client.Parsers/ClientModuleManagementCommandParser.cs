using Client.Operations.Management;
using SharedLib.Commands;
using SharedLib.Dispatcher;
using SharedLib.Dispatcher.Operations;

namespace Client.Parsers;

public class ClientModuleManagementCommandParser : CommandParserBase
{
	public override bool TryParse(IDispatcherCommand cmd)
	{
		if (cmd.Type != CommandType.ModuleManagement)
		{
			return false;
		}
		if (cmd.ParamsArray.Length < 1)
		{
			return false;
		}
		switch ((ModuleManagement)cmd.ParamsArray[0])
		{
		case ModuleManagement.RestartModule:
			cmd.Operation = new RestartModuleOperation(cmd);
			return true;
		case ModuleManagement.ShutDownModule:
			cmd.Operation = new ShutDownModuleOperation(cmd);
			return true;
		case ModuleManagement.GetModuleInfo:
			cmd.Operation = new GetApplicationModule(cmd);
			return true;
		case ModuleManagement.UpdateModule:
			cmd.Operation = new ClientModuleUpdateOperation(cmd);
			return true;
		default:
			return false;
		}
	}
}
