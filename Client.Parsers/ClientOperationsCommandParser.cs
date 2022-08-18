using Client.Operations.Management;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Parsers;

public class ClientOperationsCommandParser : CommandParserBase
{
	public override bool TryParse(IDispatcherCommand cmd)
	{
		if (cmd.Type != CommandType.UserOperation)
		{
			return false;
		}
		if (cmd.ParamsArray.Length < 1)
		{
			return false;
		}
		switch ((UserOperations)cmd.ParamsArray[0])
		{
		case UserOperations.Login:
			cmd.Operation = new ServerSideLoginOperation(cmd);
			return true;
		case UserOperations.Logout:
			cmd.Operation = new ServerSideLogoutOperation(cmd);
			return true;
		case UserOperations.GetUserState:
			cmd.Operation = new GetUserStateOperation(cmd);
			return true;
		case UserOperations.UINotify:
			cmd.Operation = new UINotificationOperation(cmd);
			return true;
		case UserOperations.Grace:
			cmd.Operation = new ServerSideGraceOperation(cmd);
			return true;
		default:
			return false;
		}
	}
}
