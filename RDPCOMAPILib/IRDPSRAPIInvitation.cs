using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RDPCOMAPILib;

[ComImport]
[CompilerGenerated]
[Guid("4FAC1D43-FC51-45BB-B1B4-2B53AA562FA3")]
[TypeIdentifier]
public interface IRDPSRAPIInvitation
{
	[DispId(232)]
	string ConnectionString
	{
		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		[DispId(232)]
		[return: MarshalAs(UnmanagedType.BStr)]
		get;
	}
}
