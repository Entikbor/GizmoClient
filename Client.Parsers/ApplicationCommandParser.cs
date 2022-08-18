using Client.Operations.Management;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Parsers;

public class ApplicationCommandParser : CommandParserBase
{
	public override bool TryParse(IDispatcherCommand cmd)
	{
		if (cmd.Type != CommandType.ApplicationsManagement)
		{
			return false;
		}
		if (cmd.ParamsArray.Length < 2)
		{
			return false;
		}
		switch ((ApplicationManagement)cmd.ParamsArray[0])
		{
		case ApplicationManagement.SetContainer:
			cmd.Operation = new SetApplicationsContainerOperation(cmd);
			return true;
		case ApplicationManagement.UpdateApplication:
			cmd.Operation = new ApplicationUpdateOperation(cmd);
			return true;
		case ApplicationManagement.AppEvent:
			cmd.Operation = new AppEventOp(cmd);
			return true;
		default:
			return false;
		}
	}
}
