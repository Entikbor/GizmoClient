using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RDPCOMAPILib;

[ComImport]
[CompilerGenerated]
[Guid("EC0671B3-1B78-4B80-A464-9132247543E3")]
[TypeIdentifier]
public interface IRDPSRAPIAttendee
{
	void _VtblGap1_2();

	[DispId(242)]
	CTRL_LEVEL ControlLevel
	{
		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		[DispId(242)]
		get;
		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		[DispId(242)]
		[param: In]
		set;
	}
}
