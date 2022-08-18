using System;
using System.Runtime.Serialization;

namespace Client;

[Serializable]
[DataContract]
public class ProcessTerminatedEventArgs : ProcessOrThreadEventArgsBase
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
	public override string ProcessName
	{
		get
		{
			return base.ProcessName;
		}
		protected set
		{
			base.ProcessName = value;
		}
	}

	public ProcessTerminatedEventArgs(int processId, string processName)
		: base(processId, processName)
	{
		ProcessId = processId;
		ProcessName = processName;
	}

	public override string ToString()
	{
		return string.Format("({0}) Process Id: {1} Process name: {2}", "ProcessTerminatedEventArgs", ProcessId, ProcessName);
	}
}
