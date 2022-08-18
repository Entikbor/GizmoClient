using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RDPCOMAPILib;

[ComImport]
[CompilerGenerated]
[Guid("4722B049-92C3-4C2D-8A65-F7348F644DCF")]
[DefaultMember("Item")]
[TypeIdentifier]
public interface IRDPSRAPIInvitationManager : IEnumerable
{
	void _VtblGap1_3();

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	[DispId(107)]
	[return: MarshalAs(UnmanagedType.Interface)]
	RDPSRAPIInvitation CreateInvitation([In][MarshalAs(UnmanagedType.BStr)] string bstrAuthString, [In][MarshalAs(UnmanagedType.BStr)] string bstrGroupName, [In][MarshalAs(UnmanagedType.BStr)] string bstrPassword, [In] int AttendeeLimit);
}
