using Client.Operations.Management;
using SharedLib.Commands;
using SharedLib.Dispatcher;
using SharedLib.Dispatcher.Operations;

namespace Client.Parsers;

public class ClientSystemManagementCommandParser : CommandParserBase
{
	public override bool TryParse(IDispatcherCommand cmd)
	{
		if (cmd.Type != CommandType.SystemManagement)
		{
			return false;
		}
		if (cmd.ParamsArray.Length < 1)
		{
			return false;
		}
		switch ((SystemManagement)cmd.ParamsArray[0])
		{
		case SystemManagement.GetComputerName:
			cmd.Operation = new GetComputerNameOperation(cmd);
			return true;
		case SystemManagement.SetComputerName:
			cmd.Operation = new SetComputerNameOperation(cmd);
			return true;
		case SystemManagement.GetOsInfo:
			cmd.Operation = new GetOsInfoOperation(cmd);
			return true;
		case SystemManagement.GetMacAddress:
			cmd.Operation = new GetMacAddressOperation(cmd);
			return true;
		case SystemManagement.SetRegistryString:
			cmd.Operation = new InstallRegistryString(cmd);
			return true;
		case SystemManagement.ManageInput:
			cmd.Operation = new SetLockStateOperation(cmd);
			return true;
		case SystemManagement.GetInputLockState:
			cmd.Operation = new GetLockStateOperation(cmd);
			return true;
		case SystemManagement.GetOutOfOrderState:
			cmd.Operation = new GetOutOfOrderStateOperation(cmd);
			return true;
		case SystemManagement.SetOutOfOrderState:
			cmd.Operation = new SetOutOfOrderStateOperation(cmd);
			return true;
		case SystemManagement.SetMaintenanceMode:
			cmd.Operation = new MaintenanceSetOperation(cmd);
			return true;
		case SystemManagement.GetMaintenanceMode:
			cmd.Operation = new MaintenanceGetOperation(cmd);
			return true;
		case SystemManagement.RDPSessionStart:
			cmd.Operation = new RDPSessionOp(cmd);
			return true;
		default:
			return false;
		}
	}
}
