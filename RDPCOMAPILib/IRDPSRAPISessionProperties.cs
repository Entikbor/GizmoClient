using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RDPCOMAPILib;

[ComImport]
[CompilerGenerated]
[Guid("339B24F2-9BC0-4F16-9AAC-F165433D13D4")]
[TypeIdentifier]
public interface IRDPSRAPISessionProperties
{
	[IndexerName("Property")]
	[DispId(0)]
	object this[[In][MarshalAs(UnmanagedType.BStr)] string PropertyName]
	{
		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		[DispId(0)]
		[return: MarshalAs(UnmanagedType.Struct)]
		get;
		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		[DispId(0)]
		[param: In]
		[param: MarshalAs(UnmanagedType.Struct)]
		set;
	}
}
