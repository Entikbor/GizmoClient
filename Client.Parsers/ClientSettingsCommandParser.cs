using Client.Operations.Management;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Parsers;

public class ClientSettingsCommandParser : CommandParserBase
{
	public override bool TryParse(IDispatcherCommand cmd)
	{
		if (cmd.Type != CommandType.SettingsManagement)
		{
			return false;
		}
		if (cmd.ParamsArray.Length < 1)
		{
			return false;
		}
		switch ((SettingsManagement)cmd.ParamsArray[0])
		{
		case SettingsManagement.SetSettings:
			cmd.Operation = new SetSettingsOperation(cmd);
			return true;
		case SettingsManagement.SetReady:
			cmd.Operation = new ClientReadyOperation(cmd);
			return true;
		case SettingsManagement.SetHostId:
			cmd.Operation = new SetIdOperation(cmd);
			return true;
		case SettingsManagement.LoadPlugins:
			cmd.Operation = new LoadPluginsOperation(cmd);
			return true;
		case SettingsManagement.DenyConnection:
			cmd.Operation = new DenyConnectionsOperation(cmd);
			return true;
		case SettingsManagement.SetNewsFeedList:
			cmd.Operation = new SetNewsFeedListOperation(cmd);
			return true;
		case SettingsManagement.SetSecurityProfileList:
			cmd.Operation = new SetSecurityProfileListOperation(cmd);
			return true;
		case SettingsManagement.SetAppProfileList:
			cmd.Operation = new SetAppProfileListOperation(cmd);
			return true;
		case SettingsManagement.NotifyGroupChange:
			cmd.Operation = new NotifyGroupChangeOperation(cmd);
			return true;
		default:
			return false;
		}
	}
}
