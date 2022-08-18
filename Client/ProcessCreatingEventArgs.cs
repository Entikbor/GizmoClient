using System;
using System.Runtime.Serialization;

namespace Client;

[Serializable]
[DataContract]
public class ProcessCreatingEventArgs : ProcessCreationEventArgBase
{
	[DataMember]
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

	[DataMember]
	public virtual int ResultCode { get; set; }

	public ProcessCreatingEventArgs(int processId, int parentProcessId, int creatingProcessId, int creatingThreadId, string processName, string imageFileName, bool fileOpenNameAvailable, string commandLine)
		: base(processId, parentProcessId, creatingProcessId, creatingThreadId, processName, imageFileName, fileOpenNameAvailable, commandLine)
	{
	}
}
