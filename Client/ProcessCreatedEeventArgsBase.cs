using System;
using System.Runtime.Serialization;

namespace Client;

[Serializable]
[DataContract]
public abstract class ProcessCreatedEeventArgsBase : ProcessCreationEventArgBase
{
	public override int ProcessId
	{
		get
		{
			return base.ProcessId;
		}
		protected set
		{
			base.ProcessId = value;
		}
	}

	public ProcessCreatedEeventArgsBase(int processId, int parentProcessId, int creatingProcessId, int creatingThreadId, string processName, string imageFileName, bool fileOpenNameAvailable, string commandLine)
		: base(processId, parentProcessId, creatingProcessId, creatingThreadId, processName, imageFileName, fileOpenNameAvailable, commandLine)
	{
	}
}
