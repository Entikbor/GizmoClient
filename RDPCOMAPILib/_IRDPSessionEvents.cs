using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RDPCOMAPILib;

[ComImport]
[CompilerGenerated]
[InterfaceType(2)]
[Guid("98A97042-6698-40E9-8EFD-B3200990004B")]
[TypeIdentifier]
public interface _IRDPSessionEvents
{
	[MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	[DispId(301)]
	void OnAttendeeConnected([In][MarshalAs(UnmanagedType.IDispatch)] object pAttendee);

	void _VtblGap1_13();

	[MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	[DispId(309)]
	void OnControlLevelChangeRequest([In][MarshalAs(UnmanagedType.IDispatch)] object pAttendee, [In] CTRL_LEVEL RequestedLevel);
}
