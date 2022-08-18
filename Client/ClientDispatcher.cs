using Client.Parsers;
using CoreLib.Commanding;
using CyClone.Commanding;
using CyClone.Core;
using SharedLib.Commands;
using SharedLib.Dispatcher;
using SharedLib.Operations;

namespace Client;

public class ClientDispatcher : MessageDispatcherBase, IMessageDispatcher, IBufferManager
{
	public ClientDispatcher()
		: base(poolDispatch: false, poolProcess: false)
	{
		Parsers.Add(new BenchmarkParser());
		Parsers.Add(new cyCommandParser());
		Parsers.Add(new VFSCommandParser());
		Parsers.Add(new ProcessOperationParser());
		Parsers.Add(new ImagingOperationsParser());
		Parsers.Add(new ApplicationCommandParser());
		Parsers.Add(new ClientModuleManagementCommandParser());
		Parsers.Add(new ClientSettingsCommandParser());
		Parsers.Add(new ClientSystemManagementCommandParser());
		Parsers.Add(new ClientSystemMonitoringCommandParser());
		Parsers.Add(new PowerManagementCommandParser());
		Parsers.Add(new SecurityCommandParser());
		Parsers.Add(new ClientOperationsCommandParser());
		Parsers.Add(new TaskOperationParser());
		Parsers.Add(new ServiceToClientCommandParser());
	}

	protected override void OnReset(OperationState state)
	{
		base.OnReset(state);
		ProtocolVersion = 0;
		CompressionLevel = 0;
	}
}
