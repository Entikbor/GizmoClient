using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RDPCOMAPILib;

[ComImport]
[CompilerGenerated]
[Guid("EEB20886-E470-4CF6-842B-2739C0EC5CFB")]
[TypeIdentifier]
public interface IRDPSRAPISharingSession
{
	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	[DispId(100)]
	void Open();

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	[DispId(101)]
	void Close();

	void _VtblGap1_2();

	[DispId(202)]
	RDPSRAPISessionProperties Properties
	{
		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		[DispId(202)]
		[return: MarshalAs(UnmanagedType.Interface)]
		get;
	}

	void _VtblGap2_1();

	[DispId(204)]
	RDPSRAPIInvitationManager Invitations
	{
		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		[DispId(204)]
		[return: MarshalAs(UnmanagedType.Interface)]
		get;
	}
}
