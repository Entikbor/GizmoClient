using System;
using System.Runtime.Serialization;

namespace Client;

[Serializable]
[DataContract]
public abstract class ProcessCreationEventArgBase : ProcessOrThreadEventArgsBase
{
	[DataMember]
	public string CommandLine { get; protected set; }

	[DataMember]
	public int CreatingProcessId { get; protected set; }

	[DataMember]
	public int CreatingThreadId { get; protected set; }

	[DataMember]
	public bool FileOpenNameAvailable { get; protected set; }

	[DataMember]
	public string ImageFileName { get; protected set; }

	[DataMember]
	public int ParentProcessId { get; protected set; }

	public ProcessCreationEventArgBase(int processId, int parentProcessId, int creatingProcessId, int creatingThreadId, string processName, string imageFileName, bool fileOpenNameAvailable, string commandLine)
		: base(processId, processName)
	{
		CommandLine = commandLine;
		CreatingProcessId = creatingProcessId;
		CreatingThreadId = creatingThreadId;
		FileOpenNameAvailable = fileOpenNameAvailable;
		ImageFileName = imageFileName;
		ParentProcessId = parentProcessId;
	}
}
