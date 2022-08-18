using System;
using System.Runtime.Serialization;

namespace Client;

[Serializable]
[DataContract]
public abstract class ProcessOrThreadEventArgsBase : EventArgs
{
	[DataMember]
	public virtual int ProcessId { get; protected set; }

	[DataMember]
	public virtual string ProcessName { get; protected set; }

	public ProcessOrThreadEventArgsBase(int processId, string processName)
	{
		ProcessId = processId;
		ProcessName = processName;
	}
}
