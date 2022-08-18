using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RDPCOMAPILib;

[ComImport]
[CompilerGenerated]
[ComEventInterface(typeof(_IRDPSessionEvents), typeof(_IRDPSessionEvents))]
[TypeIdentifier("cc802d05-ae07-4c15-b496-db9d22aa0a84", "RDPCOMAPILib._IRDPSessionEvents_Event")]
public interface _IRDPSessionEvents_Event
{
	event _IRDPSessionEvents_OnAttendeeConnectedEventHandler OnAttendeeConnected;

	void _VtblGap1_26();

	event _IRDPSessionEvents_OnControlLevelChangeRequestEventHandler OnControlLevelChangeRequest;
}
