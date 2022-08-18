using System;

namespace Client;

public interface IProcessMonitor
{
	event EventHandler<ProcessCreatingEventArgs> ProcessCreating;

	event EventHandler<ProcessCreatingEventArgs> ProcessPostCreating;

	event EventHandler<ProcessTerminatedEventArgs> ProcessTerminated;
}
