#define TRACE
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Media;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using callback.CBFSConnect;
using callback.CBFSFilter;
using Client.Drivers;
using Client.ViewModels;
using CoreAudioApi;
using CoreAudioApi.Interfaces;
using CoreLib;
using CoreLib.Diagnostics;
using CoreLib.Hooking;
using CoreLib.Imaging;
using CoreLib.Registry;
using CoreLib.Threading;
using CoreLib.Tools;
using CyClone.Core;
using CyClone.Security;
using CyClone.Streams;
using Gizmo;
using Gizmo.Shared;
using Gizmo.Web.Api.Models;
using GizmoDALV2;
using GizmoDALV2.DTO;
using GizmoDALV2.Entities;
using GizmoShell;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using IntegrationLib;
using Localization.Engine;
using Microsoft.Win32;
using NetLib;
using Newtonsoft.Json;
using ServerService;
using SharedLib;
using SharedLib.Applications;
using SharedLib.Commands;
using SharedLib.Configuration;
using SharedLib.Deployment;
using SharedLib.Dispatcher;
using SharedLib.Dispatcher.Exceptions;
using SharedLib.Logging;
using SharedLib.Management;
using SharedLib.Plugins;
using SharedLib.Tasks;
using SharedLib.User;
using SharedLib.ViewModels;
using TransformLib;
using Win32API.Com;
using Win32API.Headers;
using Win32API.Headers.Shell32.Enumerations;
using Win32API.Headers.WinUser.Enumerations;
using Win32API.Headers.WinUser.Structures;
using Win32API.Modules;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;
using Windows.UI.Notifications;

namespace Client;

[Export(typeof(IClient))]
[Export(typeof(IProcessMonitor))]
[Export(typeof(GizmoClient))]
[Export(typeof(IClinetCompositionService))]
[Export(typeof(SharedLib.Logging.ILogService))]
[Export(typeof(IAppImageHandler))]
[Export(typeof(IViewModelLocatorList<IAppExeViewModel>))]
[Export(typeof(IViewModelLocatorList<ICategoryDisplayViewModel>))]
[Export(typeof(IViewModelLocatorList<IAppViewModel>))]
[Export(typeof(IViewModelLocatorList<IAppEnterpriseDisplayViewModel>))]
[Export(typeof(IViewModelLocatorList<INewsViewModel>))]
[Export(typeof(IViewModelLocatorList<IProductViewModel>))]
[Export(typeof(IViewModelLocatorList<IProductGroupViewModel>))]
[Export(typeof(IViewModelLocatorList<IPaymentMethodViewModel>))]
public class GizmoClient : NetworkBase, IClient, IProcessMonitor, IClinetCompositionService, SharedLib.Logging.ILogService, IAppImageHandler, IViewModelLocatorList<IAppExeViewModel>, IViewModelLocator<IAppExeViewModel>, IViewModelLocatorList<ICategoryDisplayViewModel>, IViewModelLocator<ICategoryDisplayViewModel>, IViewModelLocatorList<IAppViewModel>, IViewModelLocator<IAppViewModel>, IViewModelLocatorList<IAppEnterpriseDisplayViewModel>, IViewModelLocator<IAppEnterpriseDisplayViewModel>, IViewModelLocatorList<INewsViewModel>, IViewModelLocator<INewsViewModel>, IViewModelLocatorList<IProductViewModel>, IViewModelLocator<IProductViewModel>, IViewModelLocatorList<IProductGroupViewModel>, IViewModelLocator<IProductGroupViewModel>, IViewModelLocatorList<IPaymentMethodViewModel>, IViewModelLocator<IPaymentMethodViewModel>
{
	private class AudioSessionInfo
	{
		public uint ProcessId { get; set; }

		public AudioSessionState State { get; set; }

		public float? Volume { get; set; }

		public bool? IsMuted { get; set; }
	}

	public class ClientLog : LogBase
	{
		public ClientLog(IMessageDispatcher dispatcher)
		{
			base.BaseLogProvider = new NTierLogProvider(dispatcher);
			base.CacheLogProvider = new TextLogProvider("cacheLog.txt");
		}

		protected override void AddLogMessage(ILogMessage message)
		{
			bool flag = false;
			if (base.BaseLogProvider.IsAvailable)
			{
				try
				{
					base.BaseLogProvider.AddMessage(message);
					flag = true;
				}
				catch
				{
					flag = false;
				}
			}
			if (flag)
			{
				return;
			}
			try
			{
				base.CacheLogProvider.Open();
				base.CacheLogProvider.AddMessage(message);
			}
			catch
			{
			}
			finally
			{
				try
				{
					if (base.CacheLogProvider.IsOpened)
					{
						base.CacheLogProvider.Close();
					}
				}
				catch
				{
				}
			}
		}
	}

	[ComImport]
	[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IPropertyStore
	{
		uint GetCount(out uint propertyCount);

		uint GetAt([In] uint propertyIndex, out PropertyKey key);

		uint GetValue([In] ref PropertyKey key, [Out] PropVariant pv);

		uint SetValue([In] ref PropertyKey key, [In] PropVariant pv);

		uint Commit();
	}

	public static class ErrorHelper
	{
		public static void VerifySucceeded(uint hresult)
		{
			if (hresult > 1)
			{
				throw new Exception("Failed with HRESULT: " + hresult.ToString("X"));
			}
		}
	}

	[StructLayout(LayoutKind.Explicit)]
	public sealed class PropVariant : IDisposable
	{
		private static Dictionary<Type, Action<PropVariant, Array, uint>> _vectorActions;

		private static Dictionary<Type, Func<object, PropVariant>> _cache;

		private static object _padlock;

		[FieldOffset(0)]
		private decimal _decimal;

		[FieldOffset(0)]
		private ushort _valueType;

		[FieldOffset(12)]
		private IntPtr _ptr2;

		[FieldOffset(8)]
		private IntPtr _ptr;

		[FieldOffset(8)]
		private int _int32;

		[FieldOffset(8)]
		private uint _uint32;

		[FieldOffset(8)]
		private byte _byte;

		[FieldOffset(8)]
		private sbyte _sbyte;

		[FieldOffset(8)]
		private short _short;

		[FieldOffset(8)]
		private ushort _ushort;

		[FieldOffset(8)]
		private long _long;

		[FieldOffset(8)]
		private ulong _ulong;

		[FieldOffset(8)]
		private double _double;

		[FieldOffset(8)]
		private float _float;

		public bool IsNullOrEmpty
		{
			get
			{
				if (_valueType != 0)
				{
					return _valueType == 1;
				}
				return true;
			}
		}

		public object Value
		{
			get
			{
				VarEnum valueType = (VarEnum)_valueType;
				if (valueType <= (VarEnum)4101)
				{
					switch (valueType)
					{
					case VarEnum.VT_I2:
						return _short;
					case VarEnum.VT_I4:
					case VarEnum.VT_INT:
						return _int32;
					case VarEnum.VT_R4:
						return _float;
					case VarEnum.VT_R8:
						return _double;
					case VarEnum.VT_CY:
						return _decimal;
					case VarEnum.VT_DATE:
						return DateTime.FromOADate(_double);
					case VarEnum.VT_BSTR:
						return Marshal.PtrToStringBSTR(_ptr);
					case VarEnum.VT_DISPATCH:
						return Marshal.GetObjectForIUnknown(_ptr);
					case VarEnum.VT_ERROR:
						return _long;
					case VarEnum.VT_BOOL:
						return _int32 == -1;
					case VarEnum.VT_VARIANT:
					case (VarEnum)15:
					case VarEnum.VT_VOID:
					case VarEnum.VT_HRESULT:
					case VarEnum.VT_PTR:
					case VarEnum.VT_SAFEARRAY:
					case VarEnum.VT_CARRAY:
					case VarEnum.VT_USERDEFINED:
						return null;
					case VarEnum.VT_UNKNOWN:
						return Marshal.GetObjectForIUnknown(_ptr);
					case VarEnum.VT_DECIMAL:
						return _decimal;
					case VarEnum.VT_I1:
						return _sbyte;
					case VarEnum.VT_UI1:
						return _byte;
					case VarEnum.VT_UI2:
						return _ushort;
					case VarEnum.VT_UI4:
					case VarEnum.VT_UINT:
						return _uint32;
					case VarEnum.VT_I8:
						return _long;
					case VarEnum.VT_UI8:
						return _ulong;
					case VarEnum.VT_LPSTR:
						return Marshal.PtrToStringAnsi(_ptr);
					case VarEnum.VT_LPWSTR:
						return Marshal.PtrToStringUni(_ptr);
					case VarEnum.VT_FILETIME:
						return DateTime.FromFileTime(_long);
					case VarEnum.VT_BLOB:
						return GetBlobData();
					case (VarEnum)4098:
						return GetVector<short>();
					case (VarEnum)4099:
						return GetVector<int>();
					case (VarEnum)4100:
						return GetVector<float>();
					case (VarEnum)4101:
						return GetVector<double>();
					default:
						return null;
					}
				}
				if (valueType <= (VarEnum)4127)
				{
					switch (valueType)
					{
					case (VarEnum)4107:
						return GetVector<bool>();
					case (VarEnum)4108:
					case (VarEnum)4109:
					case (VarEnum)4111:
					case (VarEnum)4112:
					case (VarEnum)4113:
						return null;
					case (VarEnum)4110:
						return GetVector<decimal>();
					case (VarEnum)4114:
						return GetVector<ushort>();
					case (VarEnum)4115:
						return GetVector<uint>();
					case (VarEnum)4116:
						return GetVector<long>();
					case (VarEnum)4117:
						return GetVector<ulong>();
					case (VarEnum)4127:
						return GetVector<string>();
					default:
						return null;
					}
				}
				return valueType switch
				{
					(VarEnum)4160 => GetVector<DateTime>(), 
					(VarEnum)8205 => CrackSingleDimSafeArray(_ptr), 
					_ => null, 
				};
			}
		}

		public ushort VarType
		{
			get
			{
				return _valueType;
			}
			set
			{
				_valueType = value;
			}
		}

		static PropVariant()
		{
			_vectorActions = null;
			_cache = new Dictionary<Type, Func<object, PropVariant>>();
			_padlock = new object();
		}

		public PropVariant()
		{
		}

		public PropVariant(string value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			_valueType = 31;
			_ptr = Marshal.StringToCoTaskMemUni(value);
		}

		public PropVariant(string[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			PropVariantNativeMethods.InitPropVariantFromStringVector(value, (uint)value.Length, this);
		}

		public PropVariant(bool[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			PropVariantNativeMethods.InitPropVariantFromBooleanVector(value, (uint)value.Length, this);
		}

		public PropVariant(short[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			PropVariantNativeMethods.InitPropVariantFromInt16Vector(value, (uint)value.Length, this);
		}

		public PropVariant(ushort[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			PropVariantNativeMethods.InitPropVariantFromUInt16Vector(value, (uint)value.Length, this);
		}

		public PropVariant(int[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			PropVariantNativeMethods.InitPropVariantFromInt32Vector(value, (uint)value.Length, this);
		}

		public PropVariant(uint[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			PropVariantNativeMethods.InitPropVariantFromUInt32Vector(value, (uint)value.Length, this);
		}

		public PropVariant(long[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			PropVariantNativeMethods.InitPropVariantFromInt64Vector(value, (uint)value.Length, this);
		}

		public PropVariant(ulong[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			PropVariantNativeMethods.InitPropVariantFromUInt64Vector(value, (uint)value.Length, this);
		}

		public PropVariant(double[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			PropVariantNativeMethods.InitPropVariantFromDoubleVector(value, (uint)value.Length, this);
		}

		public PropVariant(DateTime[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			System.Runtime.InteropServices.ComTypes.FILETIME[] array = new System.Runtime.InteropServices.ComTypes.FILETIME[value.Length];
			for (int i = 0; i < value.Length; i++)
			{
				array[i] = DateTimeToFileTime(value[i]);
			}
			PropVariantNativeMethods.InitPropVariantFromFileTimeVector(array, (uint)array.Length, this);
		}

		public PropVariant(bool value)
		{
			_valueType = 11;
			_int32 = (value ? (-1) : 0);
		}

		public PropVariant(DateTime value)
		{
			_valueType = 64;
			System.Runtime.InteropServices.ComTypes.FILETIME pftIn = DateTimeToFileTime(value);
			PropVariantNativeMethods.InitPropVariantFromFileTime(ref pftIn, this);
		}

		public PropVariant(byte value)
		{
			_valueType = 17;
			_byte = value;
		}

		public PropVariant(sbyte value)
		{
			_valueType = 16;
			_sbyte = value;
		}

		public PropVariant(short value)
		{
			_valueType = 2;
			_short = value;
		}

		public PropVariant(ushort value)
		{
			_valueType = 18;
			_ushort = value;
		}

		public PropVariant(int value)
		{
			_valueType = 3;
			_int32 = value;
		}

		public PropVariant(uint value)
		{
			_valueType = 19;
			_uint32 = value;
		}

		public PropVariant(decimal value)
		{
			_decimal = value;
			_valueType = 14;
		}

		public PropVariant(decimal[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			_valueType = 4110;
			_int32 = value.Length;
			_ptr2 = Marshal.AllocCoTaskMem(value.Length * 16);
			for (int i = 0; i < value.Length; i++)
			{
				int[] bits = decimal.GetBits(value[i]);
				Marshal.Copy(bits, 0, _ptr2, bits.Length);
			}
		}

		public PropVariant(float value)
		{
			_valueType = 4;
			_float = value;
		}

		public PropVariant(float[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			_valueType = 4100;
			_int32 = value.Length;
			_ptr2 = Marshal.AllocCoTaskMem(value.Length * 4);
			Marshal.Copy(value, 0, _ptr2, value.Length);
		}

		public PropVariant(long value)
		{
			_long = value;
			_valueType = 20;
		}

		public PropVariant(ulong value)
		{
			_valueType = 21;
			_ulong = value;
		}

		public PropVariant(double value)
		{
			_valueType = 5;
			_double = value;
		}

		private static Array CrackSingleDimSafeArray(IntPtr psa)
		{
			if (PropVariantNativeMethods.SafeArrayGetDim(psa) != 1)
			{
				throw new NotSupportedException();
			}
			int num = PropVariantNativeMethods.SafeArrayGetLBound(psa, 1u);
			int num2 = PropVariantNativeMethods.SafeArrayGetUBound(psa, 1u);
			object[] array = new object[num2 - num + 1];
			for (int i = num; i <= num2; i++)
			{
				array[i] = PropVariantNativeMethods.SafeArrayGetElement(psa, ref i);
			}
			return array;
		}

		private static System.Runtime.InteropServices.ComTypes.FILETIME DateTimeToFileTime(DateTime value)
		{
			long num = value.ToFileTime();
			System.Runtime.InteropServices.ComTypes.FILETIME result = default(System.Runtime.InteropServices.ComTypes.FILETIME);
			result.dwLowDateTime = (int)(num & -1);
			result.dwHighDateTime = (int)(num >> 32);
			return result;
		}

		public void Dispose()
		{
			PropVariantNativeMethods.PropVariantClear(this);
			GC.SuppressFinalize(this);
		}

		~PropVariant()
		{
			Dispose();
		}

		public static PropVariant FromObject(object value)
		{
			if (value != null)
			{
				return GetDynamicConstructor(value.GetType())(value);
			}
			return new PropVariant();
		}

		private static Dictionary<Type, Action<PropVariant, Array, uint>> GenerateVectorActions()
		{
			return new Dictionary<Type, Action<PropVariant, Array, uint>>
			{
				{
					typeof(short),
					delegate(PropVariant pv, Array array, uint i)
					{
						PropVariantNativeMethods.PropVariantGetInt16Elem(pv, i, out var pnVal7);
						array.SetValue(pnVal7, i);
					}
				},
				{
					typeof(ushort),
					delegate(PropVariant pv, Array array, uint i)
					{
						PropVariantNativeMethods.PropVariantGetUInt16Elem(pv, i, out var pnVal6);
						array.SetValue(pnVal6, i);
					}
				},
				{
					typeof(int),
					delegate(PropVariant pv, Array array, uint i)
					{
						PropVariantNativeMethods.PropVariantGetInt32Elem(pv, i, out var pnVal5);
						array.SetValue(pnVal5, i);
					}
				},
				{
					typeof(uint),
					delegate(PropVariant pv, Array array, uint i)
					{
						PropVariantNativeMethods.PropVariantGetUInt32Elem(pv, i, out var pnVal4);
						array.SetValue(pnVal4, i);
					}
				},
				{
					typeof(long),
					delegate(PropVariant pv, Array array, uint i)
					{
						PropVariantNativeMethods.PropVariantGetInt64Elem(pv, i, out var pnVal3);
						array.SetValue(pnVal3, i);
					}
				},
				{
					typeof(ulong),
					delegate(PropVariant pv, Array array, uint i)
					{
						PropVariantNativeMethods.PropVariantGetUInt64Elem(pv, i, out var pnVal2);
						array.SetValue(pnVal2, i);
					}
				},
				{
					typeof(DateTime),
					delegate(PropVariant pv, Array array, uint i)
					{
						PropVariantNativeMethods.PropVariantGetFileTimeElem(pv, i, out var pftVal);
						array.SetValue(DateTime.FromFileTime(GetFileTimeAsLong(ref pftVal)), i);
					}
				},
				{
					typeof(bool),
					delegate(PropVariant pv, Array array, uint i)
					{
						PropVariantNativeMethods.PropVariantGetBooleanElem(pv, i, out var pfVal);
						array.SetValue(pfVal, i);
					}
				},
				{
					typeof(double),
					delegate(PropVariant pv, Array array, uint i)
					{
						PropVariantNativeMethods.PropVariantGetDoubleElem(pv, i, out var pnVal);
						array.SetValue(pnVal, i);
					}
				},
				{
					typeof(float),
					delegate(PropVariant pv, Array array, uint i)
					{
						float[] array3 = new float[1];
						Marshal.Copy(pv._ptr2, array3, (int)i, 1);
						array.SetValue(array3[0], (int)i);
					}
				},
				{
					typeof(decimal),
					delegate(PropVariant pv, Array array, uint i)
					{
						int[] array2 = new int[4];
						for (int j = 0; j < array2.Length; j++)
						{
							array2[j] = Marshal.ReadInt32(pv._ptr2, (int)(i * 16 + j * 4));
						}
						array.SetValue(new decimal(array2), i);
					}
				},
				{
					typeof(string),
					delegate(PropVariant pv, Array array, uint i)
					{
						string ppszVal = string.Empty;
						PropVariantNativeMethods.PropVariantGetStringElem(pv, i, ref ppszVal);
						array.SetValue(ppszVal, i);
					}
				}
			};
		}

		private object GetBlobData()
		{
			byte[] array = new byte[_int32];
			Marshal.Copy(_ptr2, array, 0, _int32);
			return array;
		}

		private static Func<object, PropVariant> GetDynamicConstructor(Type type)
		{
			lock (_padlock)
			{
				if (!_cache.TryGetValue(type, out var value))
				{
					ConstructorInfo constructor = typeof(PropVariant).GetConstructor(new Type[1] { type });
					if (constructor == null)
					{
						throw new NotSupportedException();
					}
					ParameterExpression parameterExpression = System.Linq.Expressions.Expression.Parameter(typeof(object), "arg");
					System.Linq.Expressions.Expression[] arguments = new System.Linq.Expressions.Expression[1] { System.Linq.Expressions.Expression.Convert(parameterExpression, type) };
					NewExpression body = System.Linq.Expressions.Expression.New(constructor, arguments);
					ParameterExpression[] parameters = new ParameterExpression[1] { parameterExpression };
					value = System.Linq.Expressions.Expression.Lambda<Func<object, PropVariant>>(body, parameters).Compile();
					_cache.Add(type, value);
				}
				return value;
			}
		}

		private static long GetFileTimeAsLong(ref System.Runtime.InteropServices.ComTypes.FILETIME val)
		{
			return ((long)val.dwHighDateTime << 32) + val.dwLowDateTime;
		}

		private Array GetVector<T>()
		{
			int num = PropVariantNativeMethods.PropVariantGetElementCount(this);
			if (num > 0)
			{
				lock (_padlock)
				{
					if (_vectorActions == null)
					{
						_vectorActions = GenerateVectorActions();
					}
				}
				if (!_vectorActions.TryGetValue(typeof(T), out var value))
				{
					throw new NotSupportedException();
				}
				Array array = new T[num];
				for (uint num2 = 0u; num2 < num; num2++)
				{
					value(this, array, num2);
				}
				return array;
			}
			return null;
		}

		internal void SetIUnknown(object value)
		{
			_valueType = 13;
			_ptr = Marshal.GetIUnknownForObject(value);
		}

		internal void SetSafeArray(Array array)
		{
			if (array == null)
			{
				throw new ArgumentNullException("array");
			}
			IntPtr intPtr = PropVariantNativeMethods.SafeArrayCreateVector(13, 0, (uint)array.Length);
			IntPtr ptr = PropVariantNativeMethods.SafeArrayAccessData(intPtr);
			try
			{
				for (int i = 0; i < array.Length; i++)
				{
					object value = array.GetValue(i);
					IntPtr val = ((value != null) ? Marshal.GetIUnknownForObject(value) : IntPtr.Zero);
					Marshal.WriteIntPtr(ptr, i * IntPtr.Size, val);
				}
			}
			finally
			{
				PropVariantNativeMethods.SafeArrayUnaccessData(intPtr);
			}
			_valueType = 8205;
			_ptr = intPtr;
		}

		public override string ToString()
		{
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			object[] args = new object[2]
			{
				Value,
				VarType.ToString()
			};
			return string.Format(invariantCulture, "{0}: {1}", args);
		}
	}

	internal static class PropVariantNativeMethods
	{
		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void InitPropVariantFromBooleanVector([In] bool[] prgf, uint cElems, [Out] PropVariant ppropvar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void InitPropVariantFromDoubleVector([In][Out] double[] prgn, uint cElems, [Out] PropVariant propvar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void InitPropVariantFromFileTime([In] ref System.Runtime.InteropServices.ComTypes.FILETIME pftIn, [Out] PropVariant ppropvar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void InitPropVariantFromFileTimeVector([In][Out] System.Runtime.InteropServices.ComTypes.FILETIME[] prgft, uint cElems, [Out] PropVariant ppropvar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void InitPropVariantFromInt16Vector([In][Out] short[] prgn, uint cElems, [Out] PropVariant ppropvar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void InitPropVariantFromInt32Vector([In][Out] int[] prgn, uint cElems, [Out] PropVariant propVar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void InitPropVariantFromInt64Vector([In][Out] long[] prgn, uint cElems, [Out] PropVariant ppropvar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void InitPropVariantFromPropVariantVectorElem([In] PropVariant propvarIn, uint iElem, [Out] PropVariant ppropvar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void InitPropVariantFromStringVector([In][Out] string[] prgsz, uint cElems, [Out] PropVariant ppropvar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void InitPropVariantFromUInt16Vector([In][Out] ushort[] prgn, uint cElems, [Out] PropVariant ppropvar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void InitPropVariantFromUInt32Vector([In][Out] uint[] prgn, uint cElems, [Out] PropVariant ppropvar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void InitPropVariantFromUInt64Vector([In][Out] ulong[] prgn, uint cElems, [Out] PropVariant ppropvar);

		[DllImport("Ole32.dll", PreserveSig = false)]
		internal static extern void PropVariantClear([In][Out] PropVariant pvar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void PropVariantGetBooleanElem([In] PropVariant propVar, [In] uint iElem, out bool pfVal);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void PropVariantGetDoubleElem([In] PropVariant propVar, [In] uint iElem, out double pnVal);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern int PropVariantGetElementCount([In] PropVariant propVar);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void PropVariantGetFileTimeElem([In] PropVariant propVar, [In] uint iElem, out System.Runtime.InteropServices.ComTypes.FILETIME pftVal);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void PropVariantGetInt16Elem([In] PropVariant propVar, [In] uint iElem, out short pnVal);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void PropVariantGetInt32Elem([In] PropVariant propVar, [In] uint iElem, out int pnVal);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void PropVariantGetInt64Elem([In] PropVariant propVar, [In] uint iElem, out long pnVal);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void PropVariantGetStringElem([In] PropVariant propVar, [In] uint iElem, ref string ppszVal);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void PropVariantGetUInt16Elem([In] PropVariant propVar, [In] uint iElem, out ushort pnVal);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void PropVariantGetUInt32Elem([In] PropVariant propVar, [In] uint iElem, out uint pnVal);

		[DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false, SetLastError = true)]
		internal static extern void PropVariantGetUInt64Elem([In] PropVariant propVar, [In] uint iElem, out ulong pnVal);

		[DllImport("OleAut32.dll", PreserveSig = false)]
		internal static extern IntPtr SafeArrayAccessData(IntPtr psa);

		[DllImport("OleAut32.dll")]
		internal static extern IntPtr SafeArrayCreateVector(ushort vt, int lowerBound, uint cElems);

		[DllImport("OleAut32.dll")]
		internal static extern uint SafeArrayGetDim(IntPtr psa);

		[DllImport("OleAut32.dll", PreserveSig = false)]
		internal static extern object SafeArrayGetElement(IntPtr psa, ref int rgIndices);

		[DllImport("OleAut32.dll", PreserveSig = false)]
		internal static extern int SafeArrayGetLBound(IntPtr psa, uint nDim);

		[DllImport("OleAut32.dll", PreserveSig = false)]
		internal static extern int SafeArrayGetUBound(IntPtr psa, uint nDim);

		[DllImport("OleAut32.dll", PreserveSig = false)]
		internal static extern void SafeArrayUnaccessData(IntPtr psa);
	}

	private static readonly OperatingSystem OPERATING_SYSTEM = Environment.OSVersion;

	private Cbprocess CB_PROCESS;

	private Cbregistry CB_REGISTRY;

	private Cbfilter CB_FILTER;

	private System.Threading.Timer CONNECTION_TIME_OUT_TIMER;

	private System.Threading.Timer USER_IDLE_TIMER;

	private int id;

	private string INITIAL_COMMAND_LINE = string.Empty;

	private string FILE_VERSION_INFO = string.Empty;

	private bool initialized;

	private bool isOutOfOrder;

	private bool isInMaintenance;

	private bool isShuttingDown;

	private PluginManager pluginManager;

	private ClientSettings settings;

	private ClientLog log;

	private NotifyIcon icon;

	private ClientSplashWindowModel splashModel;

	private ManagerLoginViewModel managerModel;

	private ReadOnlyCollection<IAppProfile> applicationProfiles;

	private ReadOnlyCollection<ISecurityProfile> securityProfiles;

	private GroupConfiguration groupConfiguration;

	private IAppProfile currentAppProfile;

	private ISecurityProfile currentSecurityProfile;

	private readonly object INIT_OP_LOCK = new object();

	private string preferedUILanguage;

	private const int PROCESS_FILTER_CALLBACK_TIMEOUT = 5000;

	private const int REGISTRY_FILTER_CALLBACK_TIMEOUT = 5000;

	private const int FILE_FILTER_CALLBACK_TIMEOUT = 5000;

	private bool isComposed;

	private static readonly uint DIAGNOSTIC_MESSAGE_TIMEOUT = 10000u;

	private static readonly bool ENABLE_DIAGNOSTIC_SHORTCUTS = true;

	private static readonly string COMPILE_MODE = "RELEASE";

	private bool traceEnabled;

	private int unhandledUiExceptionsCount;

	private static readonly char[] INVALID_CHAR_LIST = Path.GetInvalidPathChars();

	private Dictionary<int, ExecutionContext> executionContexts;

	private Dictionary<int, int> markedDeployments;

	private Dictionary<int, int> markedPersonalFiles;

	private readonly SemaphoreSlim EXE_CONTEXT_ASYNC_LOCK = new SemaphoreSlim(1, 1);

	private System.Threading.Timer SHUT_DOWN_TIMER;

	private bool isUserIdle;

	private readonly object SHUT_DOWN_TIMER_LOCK = new object();

	private readonly object USER_IDLE_TIMER_LOCK = new object();

	private bool USER_TIME_NOTIFIED;

	private Dictionary<int, bool> USER_TIME_NOTIFICATIONS = new Dictionary<int, bool>();

	private TimeSpan USER_IDLE_TIME = TimeSpan.FromSeconds(15.0);

	private CancellationTokenSource TOAST_CTS;

	private const int AUTO_DISCOVERY_PORT = 44967;

	private ClientDispatcher dispatcher;

	private bool isConnecting;

	private System.Threading.Timer USER_DISCONNECT_TIMER;

	private readonly object USER_DISCONNECT_TIMER_LOCK = new object();

	private static readonly string TASK_MANAGER_FILE_NAME = Path.Combine(Environment.SystemDirectory, "Taskmgr.exe");

	private ProcessTrace processTrace;

	private MouseLowLevelHook mouseHook;

	private KeyboardHook keyboardHook;

	private WindowEventHook windowEventHook;

	private ShellHook shellHook;

	private bool isSecurityEnabled;

	private bool traceUserProcesses;

	private bool isInputLocked;

	private bool taskManagerDisabled;

	private Dictionary<int, ICoreProcess> userProcesses;

	private List<IRestriction> restrictions;

	private readonly ModifierKeys defaultModifiers = ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift;

	private readonly object SEC_OP_LOCK = new object();

	private const int MAX_CONCURENT_IMAGE_REQUESTS = 6;

	private readonly SemaphoreSlim APP_IMAGE_GET_LOCK = new SemaphoreSlim(6, 6);

	private CancellationTokenSource USER_TASK_CTS;

	private readonly object USER_TOKEN_SOURCE_LOCK = new object();

	private readonly ObservableCollection<IAppExeViewModel> APP_EXE_STORE;

	private readonly ObservableCollection<ICategoryDisplayViewModel> APP_CATEGORY_STORE;

	private readonly ObservableCollection<IAppViewModel> APP_STORE;

	private readonly ObservableCollection<IAppEnterpriseDisplayViewModel> APP_ENTERPRISE_STORE;

	private readonly ObservableCollection<INewsViewModel> NEWS_STORE;

	private readonly ObservableCollection<IProductViewModel> PRODUCT_STORE;

	private readonly ObservableCollection<IProductGroupViewModel> PRODUCT_GROUP_STORE;

	private readonly ObservableCollection<IPaymentMethodViewModel> PAYMENT_METHOD_STORE;

	private readonly ConcurrentDictionary<int, AppViewModel> APP_LOOK_UP = new ConcurrentDictionary<int, AppViewModel>();

	private readonly ConcurrentDictionary<int, CategoryDisplayViewModel> CATEGORY_LOOK_UP = new ConcurrentDictionary<int, CategoryDisplayViewModel>();

	private readonly ConcurrentDictionary<int, AppExeViewModel> APP_EXE_LOOK_UP = new ConcurrentDictionary<int, AppExeViewModel>();

	private readonly ConcurrentDictionary<int, AppEnterpriseDisplayViewModel> APP_ENTERPRISE_LOOK_UP = new ConcurrentDictionary<int, AppEnterpriseDisplayViewModel>();

	private readonly ConcurrentDictionary<int, INewsViewModel> NEWS_LOOKUP = new ConcurrentDictionary<int, INewsViewModel>();

	private readonly ConcurrentDictionary<int, IFeedSourceViewModel> FEEDS_LOOKUP = new ConcurrentDictionary<int, IFeedSourceViewModel>();

	private readonly ConcurrentDictionary<int, ProductViewModel> PRODUCT_LOOKUP = new ConcurrentDictionary<int, ProductViewModel>();

	private readonly ConcurrentDictionary<int, ProductGroupViewModel> PRODUCT_GROUP_LOOKUP = new ConcurrentDictionary<int, ProductGroupViewModel>();

	private readonly ConcurrentDictionary<int, PaymentMethodViewModel> PAYMENT_METHOD_LOOKUP = new ConcurrentDictionary<int, PaymentMethodViewModel>();

	private object STORE_ACCESS_LOCK = new object();

	private IntPtr mainWindowHandle;

	private Window shellWindow;

	private bool isShellWindowInBackground;

	private readonly object WINDOW_POSITION_LOCK = new object();

	private readonly int WINDOW_POSTION_LOCK_WAIT_SPAN = 1000;

	private LoginState LOGIN_STATE;

	private IUserProfile CURRENT_USER;

	private readonly object LOGIN_OP_LOCK = new object();

	private readonly object MAINTENANCE_OP_LOCK = new object();

	private readonly object USER_UI_LOCK_OP_LOCK = new object();

	public static GizmoClient Current { get; internal set; }

	IKeyboardHook IClient.KeyBoardHook => KeyBoardHook;

	IMouseLowLevelHook IClient.MouseHook => MouseHook;

	IShellHook IClient.ShellHook => ShellHook;

	[ImportMany(typeof(IClientHookPlugin))]
	private IEnumerable<IClientHookPlugin> Hooks { get; set; }

	[Import]
	public ClientShellViewModel ShellViewModel { get; protected set; }

	private ClientSplashWindowModel SplashViewModel
	{
		get
		{
			if (splashModel == null)
			{
				splashModel = new ClientSplashWindowModel(this);
			}
			return splashModel;
		}
	}

	private ManagerLoginViewModel ManagerViewModel
	{
		get
		{
			return managerModel;
		}
		set
		{
			managerModel = value;
		}
	}

	public int Id
	{
		get
		{
			return id;
		}
		internal set
		{
			int num = id;
			if (num != value)
			{
				SetProperty(ref id, value, "Id");
				RaiseIdChange(num, value);
			}
		}
	}

	public bool IsOutOfOrder
	{
		get
		{
			return isOutOfOrder;
		}
		set
		{
			if (isOutOfOrder != value)
			{
				SetProperty(ref isOutOfOrder, value, "IsOutOfOrder");
				RaiseOutOfOrderChange(value);
			}
		}
	}

	public bool IsInMaintenance
	{
		get
		{
			return isInMaintenance;
		}
		private set
		{
			if (isInMaintenance != value)
			{
				SetProperty(ref isInMaintenance, value, "IsInMaintenance");
				RaiseMaintenanceModeChange(value);
			}
		}
	}

	public PluginManager PluginManager
	{
		get
		{
			if (pluginManager == null)
			{
				pluginManager = new PluginManager();
			}
			return pluginManager;
		}
	}

	public ClientLog Log
	{
		get
		{
			if (log == null)
			{
				log = new ClientLog(Dispatcher);
			}
			return log;
		}
	}

	public System.Windows.Application Application => System.Windows.Application.Current;

	public ClientSettings Settings
	{
		get
		{
			if (settings == null)
			{
				settings = new ClientSettings();
				settings.PropertyChanged += OnSettingsChanged;
			}
			return settings;
		}
	}

	public IAppProfile CurrentAppProfile
	{
		get
		{
			return currentAppProfile;
		}
		protected set
		{
			SetProperty(ref currentAppProfile, value, "CurrentAppProfile");
		}
	}

	public ISecurityProfile CurrentSecurityProfile
	{
		get
		{
			return currentSecurityProfile;
		}
		protected set
		{
			SetProperty(ref currentSecurityProfile, value, "CurrentSecurityProfile");
		}
	}

	public bool IsInitialized
	{
		get
		{
			return initialized;
		}
		private set
		{
			SetProperty(ref initialized, value, "IsInitialized");
		}
	}

	public bool IsShuttingDown
	{
		get
		{
			return isShuttingDown;
		}
		private set
		{
			SetProperty(ref isShuttingDown, value, "IsShuttingDown");
		}
	}

	public string InitialCommandLine
	{
		get
		{
			return INITIAL_COMMAND_LINE;
		}
		private set
		{
			SetProperty(ref INITIAL_COMMAND_LINE, value, "InitialCommandLine");
		}
	}

	public NotifyIcon Icon
	{
		get
		{
			if (icon == null)
			{
				icon = new NotifyIcon();
				icon.MouseDoubleClick += OnTrayIconEvent;
				try
				{
					Icon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
				}
				catch (ArgumentException)
				{
				}
			}
			return icon;
		}
	}

	public string VersionInfo
	{
		get
		{
			if (string.IsNullOrWhiteSpace(FILE_VERSION_INFO))
			{
				FileVersionInfo fileVersionInfo = Process.GetCurrentProcess().MainModule.FileVersionInfo;
				FILE_VERSION_INFO = $"{fileVersionInfo.ProductMajorPart}.{fileVersionInfo.ProductBuildPart}.{fileVersionInfo.ProductMinorPart}";
			}
			return FILE_VERSION_INFO;
		}
	}

	public string Language
	{
		get
		{
			return Settings.Language;
		}
		set
		{
			Settings.Language = value;
			RaisePropertyChanged("Language");
		}
	}

	public int ClientProcessId => (int)EntryPoint.CURRENT_PROCESS_ID;

	public ReadOnlyCollection<IAppProfile> AppProfiles
	{
		get
		{
			if (applicationProfiles == null)
			{
				applicationProfiles = new ReadOnlyCollection<IAppProfile>(new List<IAppProfile>());
			}
			return applicationProfiles;
		}
		private set
		{
			SetProperty(ref applicationProfiles, value, "AppProfiles");
		}
	}

	public ReadOnlyCollection<ISecurityProfile> SecurityProfiles
	{
		get
		{
			if (securityProfiles == null)
			{
				securityProfiles = new ReadOnlyCollection<ISecurityProfile>(new List<ISecurityProfile>());
			}
			return securityProfiles;
		}
		private set
		{
			SetProperty(ref securityProfiles, value, "SecurityProfiles");
		}
	}

	public GroupConfiguration GroupConfiguration
	{
		get
		{
			if (groupConfiguration == null)
			{
				groupConfiguration = new GroupConfiguration();
			}
			return groupConfiguration;
		}
		private set
		{
			SetProperty(ref groupConfiguration, value, "GroupConfiguration");
			RaiseGroupConfigurationChanged();
		}
	}

	public OperatingSystem OS => OPERATING_SYSTEM;

	public Version OSVersion => OPERATING_SYSTEM.Version;

	public string PreferedUILanguage
	{
		get
		{
			return preferedUILanguage;
		}
		set
		{
			SetProperty(ref preferedUILanguage, value, "PreferedUILanguage");
			string currentLanguage = Settings?.Language;
			if (!string.IsNullOrWhiteSpace(preferedUILanguage))
			{
				Application.Dispatcher.Invoke(() => LocalizeDictionary.Instance.Language = value);
			}
			else if (!string.IsNullOrWhiteSpace(currentLanguage))
			{
				Application.Dispatcher.Invoke(() => LocalizeDictionary.Instance.Language = currentLanguage);
			}
			RaiseLanguageChange(currentLanguage, preferedUILanguage);
		}
	}

	public bool HideAppInfo { get; set; }

	public bool AllowUserLock { get; set; }

	public bool IsComposed
	{
		get
		{
			return isComposed;
		}
		protected set
		{
			SetProperty(ref isComposed, value, "IsComposed");
		}
	}

	public bool TraceEnabled
	{
		get
		{
			return traceEnabled;
		}
		set
		{
			SetProperty(ref traceEnabled, value, "TraceEnabled");
		}
	}

	private Dictionary<int, ExecutionContext> ExecutionContexts
	{
		get
		{
			if (executionContexts == null)
			{
				executionContexts = new Dictionary<int, ExecutionContext>();
			}
			return executionContexts;
		}
	}

	private Dictionary<int, int> MarkedPersonalUserFiles
	{
		get
		{
			if (markedPersonalFiles == null)
			{
				markedPersonalFiles = new Dictionary<int, int>();
			}
			return markedPersonalFiles;
		}
	}

	private Dictionary<int, int> MarkedDeployementProfiles
	{
		get
		{
			if (markedDeployments == null)
			{
				markedDeployments = new Dictionary<int, int>();
			}
			return markedDeployments;
		}
	}

	public bool IsUserIdle
	{
		get
		{
			return isUserIdle;
		}
		private set
		{
			SetProperty(ref isUserIdle, value, "IsUserIdle");
		}
	}

	private IMappingsConfiguration PersonalDriveConfiguration { get; set; }

	public ClientDispatcher Dispatcher
	{
		get
		{
			if (dispatcher == null)
			{
				lock (NET_OP_LOCK)
				{
					if (dispatcher == null)
					{
						dispatcher = new ClientDispatcher();
						dispatcher.DispatcherException += OnDispatcherException;
					}
				}
			}
			return dispatcher;
		}
	}

	public bool IsConnecting
	{
		get
		{
			return isConnecting;
		}
		private set
		{
			SetProperty(ref isConnecting, value, "IsConnecting");
		}
	}

	internal bool AllowConnecting { get; set; } = true;


	private bool IgnoreUdpRequests { get; set; }

	private Dictionary<int, ICoreProcess> UserProcesses
	{
		get
		{
			if (userProcesses == null)
			{
				userProcesses = new Dictionary<int, ICoreProcess>();
			}
			return userProcesses;
		}
	}

	private List<IRestriction> Restrictions
	{
		get
		{
			if (restrictions == null)
			{
				restrictions = new List<IRestriction>();
			}
			return restrictions;
		}
	}

	private bool TraceUserProcesses
	{
		get
		{
			return traceUserProcesses;
		}
		set
		{
			traceUserProcesses = value;
		}
	}

	public KeyboardHook KeyBoardHook
	{
		get
		{
			if (keyboardHook == null)
			{
				keyboardHook = new KeyboardHook();
			}
			return keyboardHook;
		}
	}

	public MouseLowLevelHook MouseHook
	{
		get
		{
			if (mouseHook == null)
			{
				mouseHook = new MouseLowLevelHook();
			}
			return mouseHook;
		}
	}

	public ShellHook ShellHook
	{
		get
		{
			if (shellHook == null)
			{
				shellHook = new ShellHook();
			}
			return shellHook;
		}
	}

	public WindowEventHook WindowEventHook
	{
		get
		{
			if (windowEventHook == null)
			{
				windowEventHook = new WindowEventHook(OnWindowEventProc);
			}
			return windowEventHook;
		}
	}

	public ProcessTrace ProcessTrace
	{
		get
		{
			if (processTrace == null)
			{
				processTrace = new ProcessTrace(autoEnable: true);
			}
			return processTrace;
		}
	}

	public bool IsSecurityEnabled
	{
		get
		{
			return isSecurityEnabled;
		}
		set
		{
			bool flag = IsSecurityEnabled;
			if (flag == value)
			{
				return;
			}
			SetProperty(ref isSecurityEnabled, value, "IsSecurityEnabled");
			if (value)
			{
				if (Shell.IsExplorerShellRunning)
				{
					if (!IsUserLoggedIn || IsInGracePeriod || IsUserLocked)
					{
						Shell.TryHideExplorerWindows();
					}
					else
					{
						if (StickyShell)
						{
							Shell.TryHideDesktop();
							Shell.TryHideShowDesktopButton();
						}
						if (StartMenuDisabled)
						{
							Shell.TryHideStartButton();
						}
					}
				}
				ActivateSecurityProfile();
				RevalidateRestrictions();
			}
			else
			{
				DeactivateSecurityProfile();
				Shell.TryShowExplorerWindows();
			}
			RaiseSecurityChange(value, flag);
		}
	}

	public bool IsInputLocked
	{
		get
		{
			return isInputLocked;
		}
		set
		{
			if (IsInputLocked == value)
			{
				return;
			}
			SetProperty(ref isInputLocked, value, "IsInputLocked");
			if (value && !MouseHook.IsHooked)
			{
				Application?.Dispatcher?.Invoke(delegate
				{
					MouseHook.Hook();
				});
			}
			else if (!value && MouseHook.IsHooked)
			{
				Application.Dispatcher.Invoke(delegate
				{
					MouseHook.Unhook();
				});
			}
			RaiseLockStateChage(value);
		}
	}

	public bool TaskManagerDisabled
	{
		get
		{
			return taskManagerDisabled;
		}
		private set
		{
			SetProperty(ref taskManagerDisabled, value, "TaskManagerDisabled");
		}
	}

	private bool StartMenuDisabled => Settings?.DisableStartMenu ?? false;

	private bool StickyShell => Settings?.StickyShell ?? false;

	private bool DisableDesktopSwitching => Settings?.DisableDesktopSwitching ?? false;

	public IList<IAppExeViewModel> ListSource => APP_EXE_STORE ?? new ObservableCollection<IAppExeViewModel>();

	public IEnumerable<IAppExeViewModel> EnumerableSource => APP_EXE_STORE ?? new ObservableCollection<IAppExeViewModel>();

	IList<ICategoryDisplayViewModel> IViewModelLocatorList<ICategoryDisplayViewModel>.ListSource => APP_CATEGORY_STORE ?? new ObservableCollection<ICategoryDisplayViewModel>();

	IEnumerable<ICategoryDisplayViewModel> IViewModelLocator<ICategoryDisplayViewModel>.EnumerableSource => APP_CATEGORY_STORE ?? new ObservableCollection<ICategoryDisplayViewModel>();

	IList<IAppViewModel> IViewModelLocatorList<IAppViewModel>.ListSource => APP_STORE ?? new ObservableCollection<IAppViewModel>();

	IEnumerable<IAppViewModel> IViewModelLocator<IAppViewModel>.EnumerableSource => APP_STORE ?? new ObservableCollection<IAppViewModel>();

	IList<IAppEnterpriseDisplayViewModel> IViewModelLocatorList<IAppEnterpriseDisplayViewModel>.ListSource => APP_ENTERPRISE_STORE ?? new ObservableCollection<IAppEnterpriseDisplayViewModel>();

	IEnumerable<IAppEnterpriseDisplayViewModel> IViewModelLocator<IAppEnterpriseDisplayViewModel>.EnumerableSource => APP_ENTERPRISE_STORE ?? new ObservableCollection<IAppEnterpriseDisplayViewModel>();

	IList<INewsViewModel> IViewModelLocatorList<INewsViewModel>.ListSource => NEWS_STORE;

	IEnumerable<INewsViewModel> IViewModelLocator<INewsViewModel>.EnumerableSource => NEWS_STORE;

	IList<IProductViewModel> IViewModelLocatorList<IProductViewModel>.ListSource => PRODUCT_STORE;

	IEnumerable<IProductViewModel> IViewModelLocator<IProductViewModel>.EnumerableSource => PRODUCT_STORE;

	IList<IProductGroupViewModel> IViewModelLocatorList<IProductGroupViewModel>.ListSource => PRODUCT_GROUP_STORE;

	IEnumerable<IProductGroupViewModel> IViewModelLocator<IProductGroupViewModel>.EnumerableSource => PRODUCT_GROUP_STORE;

	IList<IPaymentMethodViewModel> IViewModelLocatorList<IPaymentMethodViewModel>.ListSource => PAYMENT_METHOD_STORE;

	IEnumerable<IPaymentMethodViewModel> IViewModelLocator<IPaymentMethodViewModel>.EnumerableSource => PAYMENT_METHOD_STORE;

	public bool DataInitialized { get; protected set; }

	public bool IsShellWindowInBackground
	{
		get
		{
			return isShellWindowInBackground;
		}
		private set
		{
			SetProperty(ref isShellWindowInBackground, value, "IsShellWindowInBackground");
		}
	}

	public IntPtr ShellWindowHandle
	{
		get
		{
			return mainWindowHandle;
		}
		private set
		{
			SetProperty(ref mainWindowHandle, value, "ShellWindowHandle");
		}
	}

	public Window ShellWindow
	{
		get
		{
			return shellWindow;
		}
		private set
		{
			SetProperty(ref shellWindow, value, "ShellWindow");
		}
	}

	private IUserIdentity StoredIdentity { get; set; }

	private UserInfoTypes StoredRequestedInfo { get; set; }

	private IUserProfile StoredUserProfile { get; set; }

	public IUserProfile CurrentUser
	{
		get
		{
			return CURRENT_USER;
		}
		private set
		{
			SetProperty(ref CURRENT_USER, value, "CurrentUser");
		}
	}

	public IUserIdentity CurrentUserIdentity { get; private set; }

	public LoginState LoginState
	{
		get
		{
			return LOGIN_STATE;
		}
		private set
		{
			SetProperty(ref LOGIN_STATE, value, "LoginState");
		}
	}

	public bool IsUserLoggedIn
	{
		get
		{
			if (LoginState.HasFlag(LoginState.LoggedIn))
			{
				return CurrentUser != null;
			}
			return false;
		}
	}

	public bool IsUserLoggingIn => LoginState == LoginState.LoggingIn;

	public bool IsUserLoggedOut => LoginState == LoginState.LoggedOut;

	public bool CanLogin
	{
		get
		{
			if (Dispatcher.IsValid && !IsUserLoggedIn)
			{
				return !IsUserLoggingIn;
			}
			return false;
		}
	}

	public bool CanLogout
	{
		get
		{
			if (Dispatcher.IsValid && IsUserLoggedIn)
			{
				return !IsUserLoggingIn;
			}
			return false;
		}
	}

	public bool IsCurrentUserIsGuest => CurrentUser?.IsGuest ?? false;

	public bool IsInGracePeriod { get; set; }

	public bool IsUserLocked { get; set; }

	public event EventHandler<ShutDownEventArgs> ShutDown;

	public event EventHandler<StartUpEventArgs> StartUp;

	public event EventHandler<UserProfileChangeArgs> UserProfileChange;

	public event EventHandler<UserPasswordChangeEventArgs> UserPasswordChange;

	public event EventHandler<UserEventArgs> LoginStateChange;

	public event EventHandler<LockStateEventArgs> LockStateChange;

	public event EventHandler<IdChangeEventArgs> IdChange;

	public event EventHandler<SecurityStateArgs> SecurityStateChange;

	public event EventHandler<CollectionChangeEventArgs> ExecutionContextCollectionChange;

	public event EventHandler<ExecutionContextStateArgs> ExecutionContextStateChage;

	public event EventHandler<ClientActivityEventArgs> ActivityChange;

	public event EventHandler<OutOfOrderStateEventArgs> OutOfOrderStateChange;

	public event EventHandler<ApplicationRateEventArgs> ApplicationRated;

	public event EventHandler<ProfilesChangeEventArgs> AppProfilesChange;

	public event EventHandler<ProfilesChangeEventArgs> SecurityProfilesChange;

	public event EventHandler<EventArgs> GroupConfigurationChange;

	public event EventHandler<MaintenanceEventArgs> MaintenanceModeChange;

	public event EventHandler<UserBalanceEventArgs> UserBalanceChange;

	public event EventHandler<UsageSessionChangedEventArgs> UsageSessionChanged;

	public event EventHandler<UserIdleEventArgs> UserIdleChange;

	public event EventHandler<OrderStatusChangeEventArgs> OrderStatusChange;

	public event EventHandler<LanguageChangeEventArgs> LanguageChange;

	public event EventHandler<ReservationChangeEventArgs> ReservationChange;

	public event EventHandler<GracePeriodChangeEventArgs> GracePeriodChange;

	public event EventHandler<UserAgreementsLoadedEventArgs> UserAgreementsLoaded;

	public event EventHandler<UserLockChangeEventArgs> UserLockChange;

	public event EventHandler<ProcessCreatingEventArgs> ProcessCreating;

	public event EventHandler<ProcessCreatingEventArgs> ProcessPostCreating;

	public event EventHandler<ProcessTerminatedEventArgs> ProcessTerminated;

	public GizmoClient()
	{
		ClientInstance.Instance = this;
		AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
		AppDomain.CurrentDomain.AssemblyLoad += OnDebugAssemblyLoad;
		AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
		AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
		AppDomain.CurrentDomain.TypeResolve += OnTypeResolve;
		Application.DispatcherUnhandledException += OnDispatcherUnhandledException;
		SharedFunctions.CaptureRequest += OnScreenCaptureRequest;
		SharedFunctions.MacRequest += OnSharedMacRequest;
		SharedFunctions.IPAddressRequest += OnSharedIPAddressRequest;
		SystemEvents.SessionSwitch += OnSessionSwitch;
		SystemEvents.SessionEnded += OnSessionEnded;
		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
		SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
		cyAccessList cyAccessList = new cyAccessList
		{
			Access = FileAccess.ReadWrite
		};
		cyAccessList.Add(Dispatcher);
		HandleManager.SharesList.Add("*", cyAccessList);
		LocalizeDictionary.Instance.DefaultResourcePath = "Client.Resources.Language.English.resources";
		APP_EXE_STORE = new ObservableCollection<IAppExeViewModel>();
		APP_CATEGORY_STORE = new ObservableCollection<ICategoryDisplayViewModel>();
		APP_STORE = new ObservableCollection<IAppViewModel>();
		APP_ENTERPRISE_STORE = new ObservableCollection<IAppEnterpriseDisplayViewModel>();
		NEWS_STORE = new ObservableCollection<INewsViewModel>();
		PRODUCT_STORE = new ObservableCollection<IProductViewModel>();
		PRODUCT_GROUP_STORE = new ObservableCollection<IProductGroupViewModel>();
		PAYMENT_METHOD_STORE = new ObservableCollection<IPaymentMethodViewModel>();
		BindingOperations.EnableCollectionSynchronization(APP_EXE_STORE, STORE_ACCESS_LOCK);
		BindingOperations.EnableCollectionSynchronization(APP_STORE, STORE_ACCESS_LOCK);
		BindingOperations.EnableCollectionSynchronization(APP_ENTERPRISE_STORE, STORE_ACCESS_LOCK);
		BindingOperations.EnableCollectionSynchronization(APP_CATEGORY_STORE, STORE_ACCESS_LOCK);
		BindingOperations.EnableCollectionSynchronization(NEWS_STORE, STORE_ACCESS_LOCK);
		BindingOperations.EnableCollectionSynchronization(PRODUCT_STORE, STORE_ACCESS_LOCK);
		BindingOperations.EnableCollectionSynchronization(PRODUCT_GROUP_STORE, STORE_ACCESS_LOCK);
		BindingOperations.EnableCollectionSynchronization(PAYMENT_METHOD_STORE, STORE_ACCESS_LOCK);
		DispatcherFactory.Dispatcher = Dispatcher;
	}

	public void Restart()
	{
		Stop(restart: true);
	}

	public void Stop()
	{
		Stop(restart: false, crashed: false, null);
	}

	public void Stop(bool restart = false, bool crashed = false, ProcessExitCode? overridExitCode = null)
	{
		try
		{
			if (IsShuttingDown)
			{
				return;
			}
			IsShuttingDown = true;
			EventHandler<ShutDownEventArgs> shutDown = this.ShutDown;
			if (shutDown != null)
			{
				ShutDownEventArgs e = new ShutDownEventArgs(restart, crashed);
				Delegate[] invocationList = shutDown.GetInvocationList();
				for (int i = 0; i < invocationList.Length; i++)
				{
					EventHandler<ShutDownEventArgs> eventHandler = (EventHandler<ShutDownEventArgs>)invocationList[i];
					try
					{
						eventHandler(this, e);
					}
					catch (Exception ex)
					{
						TraceWrite("ShutDown event handler error.", ex.Message, "Stop", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 631);
					}
				}
			}
			try
			{
				MappingManager.Clear();
			}
			catch (Exception ex2)
			{
				LogAddError("Shutdown drive unmaping failed.", ex2, LogCategories.FileSystem);
			}
			try
			{
				RestoreSpecailFoldersPaths(KnownFolderTypes.Basic);
			}
			catch (Exception ex3)
			{
				LogAddError("Shutdown shell folders restore failed.", ex3, LogCategories.FileSystem);
			}
			try
			{
				Cbprocess cB_PROCESS = CB_PROCESS;
				if (cB_PROCESS != null)
				{
					cB_PROCESS.OnProcessCreation -= ProcessCreationEvent;
					cB_PROCESS.OnProcessTermination -= OnProcessTermination;
					cB_PROCESS.StopFilter();
					cB_PROCESS = null;
				}
			}
			catch (Exception ex4)
			{
				LogAddError("Process filter termination failed.", ex4, LogCategories.Generic);
			}
			try
			{
				Cbfilter cB_FILTER = CB_FILTER;
				if (cB_FILTER != null)
				{
					cB_FILTER.StopFilter(waitForDetach: true);
					cB_FILTER = null;
				}
			}
			catch (Exception ex5)
			{
				LogAddError("File filter termination failed.", ex5, LogCategories.Generic);
			}
			try
			{
				Cbregistry cB_REGISTRY = CB_REGISTRY;
				if (cB_REGISTRY != null)
				{
					cB_REGISTRY.OnBeforeOpenKey -= RegistryOnBeforeOpenKey;
					cB_REGISTRY.StopFilter();
					cB_REGISTRY = null;
				}
			}
			catch (Exception ex6)
			{
				LogAddError("Registry filter termination failed.", ex6, LogCategories.Generic);
			}
			try
			{
				OnSecurityDeinitialize();
			}
			catch (Exception ex7)
			{
				LogAddError("OnSecurityDeinitialize error.", ex7, LogCategories.Operation);
			}
			if (Icon.Visible)
			{
				SetIconVisible(isVisible: false);
			}
			OnShellWindowClose();
			try
			{
				if (Shell.IsExplorerShellRunning)
				{
					Shell.TryShowExplorerWindows();
				}
			}
			catch (Exception ex8)
			{
				if (TraceEnabled)
				{
					TraceWrite(ex8, "Stop", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 749);
				}
			}
			AllowConnecting = false;
			Deinitialize();
			if (restart && !EntryPoint.ENABLE_CLIENT_SERVICE)
			{
				Process.Start(Assembly.GetExecutingAssembly().Location, InitialCommandLine);
			}
		}
		catch (Exception ex9)
		{
			LogAddError("Error occurred during client shutdown.", ex9, LogCategories.Operation);
		}
		finally
		{
			if (!overridExitCode.HasValue)
			{
				if (!crashed)
				{
					CoreProcess.ExitCurrent(restart ? ProcessExitCode.Restart : ProcessExitCode.ShutDown);
				}
				else
				{
					CoreProcess.ExitCurrent(ProcessExitCode.Error);
				}
			}
			else
			{
				CoreProcess.ExitCurrent(overridExitCode.Value);
			}
		}
	}

	public void SetSettingsValue(string name, string value)
	{
		Settings.Set(name, value);
	}

	public void SetSettingsValue(string name, object value)
	{
		Settings.Set(name, value);
	}

	public T GetSettingsValue<T>(string name)
	{
		return Settings.Get<T>(name);
	}

	public void SetSettings(ClientSettings settings, bool saveSettings)
	{
		if (settings == null)
		{
			throw new ArgumentNullException("settings", "Settings value may not be null");
		}
		Settings.Initialize(settings);
		Id = Settings.Number;
		Environment.SetEnvironmentVariable("HOST_NUMBER", Settings.Number.ToString());
		Environment.SetEnvironmentVariable("HOST_NAME", Settings.Name ?? string.Empty);
		Environment.SetEnvironmentVariable("CUR_HOST_GROUP_NAME", Settings.HostGroupName ?? string.Empty);
		Environment.SetEnvironmentVariable("CUR_HOST_GROUP_ID", Settings.HostGroupId.HasValue ? Settings.HostGroupId.Value.ToString() : string.Empty);
		if (saveSettings)
		{
			Settings.FilePath = Path.Combine(Settings.CachePath, "Settings.gcf");
			Settings.SaveSettings();
		}
	}

	public bool SetPowerState(PowerStates state, bool force = true)
	{
		ProcessTasks(ActivationType.Shutdown);
		bool flag = false;
		switch (state)
		{
		case PowerStates.Hibernate:
			flag = System.Windows.Forms.Application.SetSuspendState(PowerState.Hibernate, force, disableWakeEvent: false);
			break;
		case PowerStates.Suspend:
			return System.Windows.Forms.Application.SetSuspendState(PowerState.Suspend, force, disableWakeEvent: false);
		case PowerStates.Shutdown:
			flag = PowerTool.Shutdown(force);
			break;
		case PowerStates.Reboot:
			flag = PowerTool.Reboot(force);
			break;
		case PowerStates.Logoff:
			flag = PowerTool.LogOff(force);
			break;
		}
		if (flag)
		{
			Stop(restart: false, crashed: false, ProcessExitCode.SystemPowerEvent);
		}
		return flag;
	}

	internal async System.Threading.Tasks.Task StartAsync(string[] args)
	{
		Environment.SetEnvironmentVariable("CUR_WORKING_DIRECTORY", Environment.CurrentDirectory);
		RaiseActivityChange(ClientStartupActivity.CheckingSystemDriver);
		await SplashViewModel.ShowAsync();
		Settings.SetDefaults();
		if (ClientSettings.TryGetCacheSettingsFileName(out var cacheSettingsFileName) && File.Exists(cacheSettingsFileName))
		{
			try
			{
				ClientSettings clientSettings = SharedSettings.FromFile<ClientSettings>(cacheSettingsFileName);
				if (clientSettings != null)
				{
					SetSettings(clientSettings, saveSettings: false);
				}
			}
			catch (Exception ex)
			{
				TraceWrite("Cached Settings initialization failed.", ex.Message, "StartAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 948);
			}
		}
		try
		{
			if (CBHelperBase<Cbprocess>.IsInitialized)
			{
				CB_PROCESS = new Cbprocess
				{
					RuntimeLicense = "43464E4641444E585246323032323034313136474E393234353500000000000000000000000000004D555A394341575400005641314758484A34474D56310000"
				};
				CB_PROCESS.OnProcessCreation += ProcessCreationEvent;
				CB_PROCESS.OnProcessTermination += OnProcessTermination;
				CB_PROCESS.StartFilter(5000);
				CB_PROCESS.AddFilteredProcessById(-1, includeChildren: true);
			}
		}
		catch (Exception ex2)
		{
			TraceWrite(ex2, "StartAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 977);
		}
		try
		{
			ClientSettings.TryGetRegistryFilterSettings(out var disabled);
			if (!disabled && CBHelperBase<Cbregistry>.IsInitialized)
			{
				CB_REGISTRY = new Cbregistry
				{
					RuntimeLicense = "43464E4641444E585246323032323034313136474E393234353500000000000000000000000000004D555A394341575400005641314758484A34474D56310000"
				};
				CB_REGISTRY.OnBeforeOpenKey += RegistryOnBeforeOpenKey;
				CB_REGISTRY.StartFilter(5000);
				CB_REGISTRY.AddFilterRule("HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\KProcessHacker*", 0, 4L);
			}
		}
		catch (Exception ex3)
		{
			TraceWrite(ex3, "StartAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1013);
		}
		try
		{
			ClientSettings.TryGetFileFilterSettings(out var disabled2);
			if (!disabled2 && CBHelperBase<Cbfilter>.IsInitialized)
			{
				CB_FILTER = new Cbfilter
				{
					RuntimeLicense = "43464E4641444E585246323032323034313136474E393234353500000000000000000000000000004D555A394341575400005641314758484A34474D56310000"
				};
				CB_FILTER.StartFilter(5000);
			}
		}
		catch (Exception ex4)
		{
			TraceWrite(ex4, "StartAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1044);
		}
		try
		{
			OnSecurityInitialize();
			IsSecurityEnabled = true;
			if (Shell.IsExploreTrayWindowCreated())
			{
				Shell.TryHideExplorerWindows();
			}
			OnMinimizeNonClientWindows();
		}
		catch (Exception ex5)
		{
			LogAddError("OnSecurityInitialize error.", ex5, LogCategories.Operation);
		}
		RaiseActivityChange(ClientStartupActivity.ParsingArguments);
		InitialCommandLine = args.DefaultIfEmpty().Aggregate((string first, string next) => first + " " + next);
		RaiseActivityChange(ClientStartupActivity.StartingNetworkServices);
		try
		{
			Settings.Connection.IsInitialized = false;
			if (ClientSettings.TryGetRegistryConnectionSettings(out var preferedIpOrHostName, out var preferedPort, throwOnFail: true))
			{
				if (!string.IsNullOrWhiteSpace(preferedIpOrHostName))
				{
					int connectPort = 44966;
					if (!string.IsNullOrWhiteSpace(preferedPort) && int.TryParse(preferedPort, out var result) && connectPort > 0 && connectPort < 65536)
					{
						connectPort = result;
					}
					if (!IPAddress.TryParse(preferedIpOrHostName, out var address))
					{
						RaiseActivityChange(ClientStartupActivity.ResolvingServerIp);
						address = (await Dns.GetHostAddressesAsync(preferedIpOrHostName)).Where((IPAddress ADDRESS) => ADDRESS.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault();
					}
					if (address != null)
					{
						Settings.Connection.IpAddress = address.ToString();
						Settings.Connection.Hostname = preferedIpOrHostName;
						Settings.Connection.ServerPort = connectPort;
						Settings.Connection.IsInitialized = true;
						TraceWrite($"Prefered connection settings found {address}:{connectPort}.", (string)null, "StartAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1150);
					}
					else
					{
						TraceWrite("No prefered connection host found.", (string)null, "StartAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1154);
					}
				}
			}
			else
			{
				TraceWrite("No registry connection settings found.", (string)null, "StartAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1160);
			}
		}
		catch (SocketException ex6)
		{
			LogAddError("Could not parse command line connection settings.", ex6, LogCategories.Network);
		}
		catch (Exception ex7)
		{
			TraceWrite("Registry connection settings init failed.", ex7.ToString(), "StartAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1169);
			TraceWrite(ex7, "StartAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1171);
		}
		IgnoreUdpRequests = true;
		Initialize();
		if (Settings.Connection.IsInitialized)
		{
			if (base.IsAvailable)
			{
				TraceWrite($"Initiating connection to {Settings.Connection}", (string)null, "StartAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1189);
				RaiseActivityChange(ClientStartupActivity.InitiatingConnection);
				await System.Threading.Tasks.Task.Run(delegate
				{
					Connect();
				});
			}
			else
			{
				TraceWrite(string.Format("Network {0} is {1}", "IsAvailable", base.IsAvailable), (string)null, "StartAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1195);
			}
		}
		IgnoreUdpRequests = false;
		Action<object> @object = delegate
		{
			CONNECTION_TIME_OUT_TIMER?.Dispose();
			if (Monitor.TryEnter(INIT_OP_LOCK))
			{
				try
				{
					if (!IsInitialized)
					{
						if (Settings.IsInitialized)
						{
							LoadPlugins();
							Ready();
						}
						else
						{
							AllowConnecting = false;
							if (System.Windows.Forms.MessageBox.Show(new IWindowWrapper(SplashViewModel.WindowHandle), "No default Client configuration found.\nTo be able to continue a connection to the Server must be made at least once.\nPress ok to wait for server connection or cancel to terminate.", "Default Configuration", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) == DialogResult.Cancel)
							{
								Stop();
							}
							else
							{
								AllowConnecting = true;
							}
						}
					}
				}
				catch (Exception ex9)
				{
					LogAddError("Eror durring local client initialization.", ex9, LogCategories.Configuration);
				}
				finally
				{
					Monitor.Exit(INIT_OP_LOCK);
				}
			}
		};
		int dueTime = (base.IsAvailable ? 60000 : 0);
		CONNECTION_TIME_OUT_TIMER = new System.Threading.Timer(@object.Invoke, CONNECTION_TIME_OUT_TIMER, dueTime, -1);
		USER_IDLE_TIMER = new System.Threading.Timer(OnUserIdleTimerCallBack, null, 0, 100);
		try
		{
			if (OSVersion.Major >= 10)
			{
				TryCreateShortcut();
			}
		}
		catch (Exception ex8)
		{
			TraceWrite(ex8, "StartAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1286);
		}
	}

	internal void Ready()
	{
		try
		{
			lock (INIT_OP_LOCK)
			{
				if (IsInitialized)
				{
					return;
				}
				foreach (SharedLib.Management.Variable variable in Settings.Variables)
				{
					try
					{
						variable.Set(expnad: true);
					}
					catch (Exception ex)
					{
						LogAddError("Failed setting environment variable " + variable.Name, ex, LogCategories.Generic);
					}
				}
				RaiseActivityChange(ClientStartupActivity.ProcessingTasks);
				ProcessTasks(ActivationType.Startup);
				RaiseActivityChange(ClientStartupActivity.ProcessingDriveMappings);
				ProcessMappings(Settings.Mappings);
				RaiseActivityChange(ClientStartupActivity.StartingUi);
				User32.DisableProcessWindowsGhosting();
				try
				{
					string skinsPath = Settings.SkinsPath;
					string skinName = Settings.SkinName;
					if (string.IsNullOrWhiteSpace(skinName))
					{
						throw new ArgumentException("Skin name not specified.", "SkinName");
					}
					if (string.IsNullOrWhiteSpace(skinsPath))
					{
						throw new ArgumentException("Skins path not specified.", "SkinsPath");
					}
					if (!Directory.Exists(skinsPath))
					{
						throw new DirectoryNotFoundException("Skins directory " + skinsPath + " not found.");
					}
					string text = Path.Combine(skinsPath, skinName);
					if (!Directory.Exists(text))
					{
						throw new DirectoryNotFoundException("Skin directory " + text + " not found.");
					}
					string path = "config.json";
					string text2 = Path.Combine(text, path);
					SkinConfig SKIN_CONFIG = new SkinConfig
					{
						Rotator = new RotatorConfig()
					};
					SKIN_CONFIG.SetDefaults();
					if (File.Exists(text2))
					{
						try
						{
							SKIN_CONFIG = JsonDeserializeConfig(text2);
						}
						catch (FileNotFoundException)
						{
						}
						catch (Exception ex3)
						{
							LogAddError("Skin configuration file is invalid.", ex3, LogCategories.Generic);
						}
					}
					HideAppInfo = SKIN_CONFIG.HideAppInfo;
					AllowUserLock = SKIN_CONFIG.AllowUserLock;
					IEnumerable<string> noLoadDll2 = SKIN_CONFIG.NoLoadDll;
					IEnumerable<string> noLoadDlls = noLoadDll2 ?? Enumerable.Empty<string>();
					Assembly LOADED_ASSEMBLY;
					foreach (string item in (from dllFile in Directory.GetFiles(text, "*.dll")
						where !noLoadDlls.Any((string noLoadDll) => Path.GetFileName(dllFile) == noLoadDll)
						select dllFile).ToList())
					{
						try
						{
							byte[] rawAssembly = File.ReadAllBytes(item);
							LOADED_ASSEMBLY = Assembly.Load(rawAssembly);
							Application.Dispatcher.Invoke(delegate
							{
								AddAssembly(LOADED_ASSEMBLY);
							});
						}
						catch (Exception ex4)
						{
							TraceWrite("Could not load skin dll file " + item + ", exception " + ex4.ToString(), (string)null, "Ready", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1417);
						}
					}
					GetExportedValues<ISkinBootStrapper>().FirstOrDefault()?.InitializeAsync(text2).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter()
						.GetResult();
					IEnumerable<Lazy<IClientSectionModule, IClientSkinModuleMetadata>> exports = GetExports<IClientSectionModule, IClientSkinModuleMetadata>();
					exports = exports.Where((Lazy<IClientSectionModule, IClientSkinModuleMetadata> export) => !SKIN_CONFIG.SupressModule.Any((string moduleType) => moduleType == export.Metadata.Type.FullName));
					exports = exports.Where((Lazy<IClientSectionModule, IClientSkinModuleMetadata> export) => !SKIN_CONFIG.SupressModule.Any((string moduleType) => moduleType == export.Metadata.Guid));
					ClientSettings clientSettings = Settings;
					if (clientSettings != null && !clientSettings.IsOrderingEnabled)
					{
						exports = exports.Where((Lazy<IClientSectionModule, IClientSkinModuleMetadata> e) => !e.Metadata.Type.GetInterfaces().Contains(typeof(IShopModule)));
					}
					Application.Dispatcher.Invoke(() => exports.Select((Lazy<IClientSectionModule, IClientSkinModuleMetadata> export) => export.Value).ToList());
					foreach (Lazy<IClientSectionModule, IClientSkinModuleMetadata> item2 in from module in exports.OfType<Lazy<IClientSectionModule, IClientSkinModuleMetadata>>()
						orderby module.Metadata.DisplayOrder
						select module)
					{
						SectionModuleViewModel<IClientSectionModule> sectionModuleViewModel = new SectionModuleViewModel<IClientSectionModule>();
						IClientSkinModuleMetadata metadata = item2.Metadata;
						sectionModuleViewModel.IconResource = metadata.IconResource;
						sectionModuleViewModel.DisplayOrder = metadata.DisplayOrder;
						sectionModuleViewModel.Module = item2.Value;
						sectionModuleViewModel.MetaData = metadata;
						sectionModuleViewModel.Title = ((metadata.IsLocalized && !string.IsNullOrWhiteSpace(metadata.Title)) ? GetLocalizedString(metadata.Title) : metadata.Title);
						sectionModuleViewModel.Description = ((metadata.IsLocalized && !string.IsNullOrWhiteSpace(metadata.Description)) ? GetLocalizedString(metadata.Description) : metadata.Description);
						ShellViewModel.Add(sectionModuleViewModel);
					}
					try
					{
						ShellViewModel.LoginRotator.Initialize(SKIN_CONFIG, text);
					}
					catch (Exception ex5)
					{
						LogAddError("Rotator initialization failed.", ex5, LogCategories.Generic);
					}
					Application.Dispatcher.Invoke(delegate
					{
						try
						{
							ShellViewModel.Apps.AppSort = SKIN_CONFIG.DefaultAppSort;
						}
						catch (Exception ex8)
						{
							LogAddError("App sort initialization failed.", ex8, LogCategories.Generic);
						}
						try
						{
							ShellViewModel.ShopViewModel.ProductSort = SKIN_CONFIG.DefaultProductSort;
						}
						catch (Exception ex9)
						{
							LogAddError("Product sort initialization failed.", ex9, LogCategories.Generic);
						}
						Window window = GetExportedValue<IShellWindow>() as Window;
						window.ShowInTaskbar = !StickyShell;
						window.ResizeMode = ResizeMode.CanMinimize;
						window.SourceInitialized += OnShellWindowSourceInitialized;
						window.Closing += OnShellWindowClosing;
						window.DataContext = ShellViewModel;
						ShellWindow = window;
					});
				}
				catch (Exception ex6)
				{
					LogAddError("Shell initialization failed. Exception message " + ex6.Message, ex6, LogCategories.Generic);
				}
				if (SplashViewModel.IsLoaded)
				{
					SplashViewModel.Hide();
				}
				lock (MAINTENANCE_OP_LOCK)
				{
					if (!IsInMaintenance)
					{
						SetIconVisible(isVisible: false);
						OnShellWindowShow();
					}
				}
				IsInitialized = true;
				this.StartUp?.Invoke(this, new StartUpEventArgs());
				if (StoredIdentity != null)
				{
					OnUserLoggedIn(StoredIdentity, StoredUserProfile, StoredRequestedInfo);
				}
				else
				{
					InitShutdownTimer();
				}
			}
		}
		catch (Exception ex7)
		{
			LogAddError("Ready", ex7, LogCategories.Generic);
		}
	}

	private void TryClearCache()
	{
		string text = Settings?.DataPath;
		if (!string.IsNullOrWhiteSpace(text) && cyDirectory.Exists(text))
		{
			try
			{
				cyDirectory.RemoveDirectory(text, recursive: true);
			}
			catch (Exception ex)
			{
				TraceWrite(ex, "TryClearCache", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1579);
			}
		}
	}

	internal void Uninstall(bool deactivateOnly = false)
	{
		DeactivateSecurityProfile(throwOnError: false);
		CoreProcess.SetStartup("GizmoClient", currentUser: false, enable: false);
		Shell.TryShowExplorerWindows();
		if (!Shell.IsExplorerShellRunning)
		{
			Shell.StartDefault();
		}
		if (!deactivateOnly)
		{
			TryClearCache();
			try
			{
				CB_FILTER.StopFilter(waitForDetach: true);
			}
			catch
			{
			}
			try
			{
				CB_PROCESS.StopFilter();
			}
			catch
			{
			}
			try
			{
				CB_REGISTRY.StopFilter();
			}
			catch
			{
			}
			EntryPoint.Uninstall();
			ClientSettings.TryDeleteRegistrySettings();
		}
		Stop();
	}

	private bool SetIconVisible(bool isVisible)
	{
		try
		{
			Dispatcher dispatcher = Application?.Dispatcher;
			if (dispatcher != null)
			{
				Action<bool> action = delegate(bool show)
				{
					Icon.Visible = show;
				};
				if (dispatcher.CheckAccess())
				{
					action(isVisible);
				}
				else
				{
					dispatcher.Invoke(action, isVisible);
				}
			}
		}
		catch (Exception ex)
		{
			if (TraceEnabled)
			{
				TraceWrite(ex, "SetIconVisible", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client.cs", 1678);
			}
			return false;
		}
		return true;
	}

	private void ProcessTasks(ActivationType activationType)
	{
		IEnumerable<SharedLib.Tasks.Task> enumerable = Settings?.Tasks.OfType<SharedLib.Tasks.Task>();
		if (enumerable == null)
		{
			return;
		}
		if (activationType == ActivationType.Logout)
		{
			foreach (SharedLib.Tasks.Task item in enumerable.Where((SharedLib.Tasks.Task task) => task.IsActive))
			{
				try
				{
					item.Terminate();
				}
				catch (Exception ex)
				{
					LogAddError(string.Format("Task {1} termination failed. Activation event {0}", activationType, item.TaskName), ex, LogCategories.Task);
				}
			}
		}
		foreach (SharedLib.Tasks.Task item2 in enumerable.Where((SharedLib.Tasks.Task task) => task.Activation == activationType))
		{
			try
			{
				item2.Execute();
			}
			catch (Exception ex2)
			{
				LogAddError(string.Format("Task {1} execution failed. Activation event {0}", activationType, item2.TaskName), ex2, LogCategories.Task);
			}
		}
	}

	internal void OnResetGroupConfiguration()
	{
		try
		{
			GroupConfiguration = GetGroupConfiguration();
			OnAppProfilesChanged();
			OnSecurityProfilesChanged();
		}
		catch (ConnectionLostException)
		{
		}
		catch (Exception ex2)
		{
			LogAddError("OnResetGroupConfiguration", ex2, LogCategories.Generic);
		}
	}

	internal void SetAppProfiles(List<IAppProfile> profiles, bool initial)
	{
		if (profiles == null)
		{
			throw new ArgumentNullException("profiles");
		}
		AppProfiles = new ReadOnlyCollection<IAppProfile>(profiles);
		OnAppProfilesChanged(initial);
	}

	internal void SetSecurityProfiles(List<ISecurityProfile> profiles, bool initial)
	{
		if (profiles == null)
		{
			throw new ArgumentNullException("profiles");
		}
		SecurityProfiles = new ReadOnlyCollection<ISecurityProfile>(profiles);
		OnSecurityProfilesChanged(initial);
	}

	private void OnAppProfilesChanged(bool initial = false)
	{
		CurrentAppProfile = AppProfiles.Where((IAppProfile profile) => profile.Id == GroupConfiguration.AppProfile).SingleOrDefault();
		Environment.SetEnvironmentVariable("CUR_APP_PROFILE", CurrentAppProfile?.Name ?? string.Empty);
		RaiseAppPoriflesChange(initial);
	}

	private void OnSecurityProfilesChanged(bool initial = false)
	{
		CurrentSecurityProfile = SecurityProfiles.Where((ISecurityProfile profile) => profile.Id == GroupConfiguration.SecurityProfile).SingleOrDefault();
		Environment.SetEnvironmentVariable("CUR_SEC_PROFILE", CurrentSecurityProfile?.Name ?? string.Empty);
		TaskManagerDisabled = CurrentSecurityProfile?.Policies.Where((ISecurityPolicy POLICY) => POLICY.Type == SecurityPolicyType.DisableTaskMgr).Any() ?? false;
		RaiseSecuirtyPoriflesChanged(initial);
		if (!initial && CurrentSecurityProfile != null && IsSecurityEnabled)
		{
			ActivateSecurityProfile(IsSecurityEnabled);
		}
	}

	private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
	{
		try
		{
			ClientSettings clientSettings = (ClientSettings)sender;
			if (e.PropertyName == "TurnOffTimeOut" && !IsUserLoggedIn && !IsInMaintenance)
			{
				InitShutdownTimer();
			}
			if (e.PropertyName == "LanguagePath")
			{
				string resourcesPath = clientSettings.LanguagePath;
				if (!string.IsNullOrWhiteSpace(resourcesPath))
				{
					resourcesPath = Environment.ExpandEnvironmentVariables(resourcesPath);
					Application.Dispatcher.Invoke(() => LocalizeDictionary.Instance.SearchPath = resourcesPath);
				}
			}
			if (!(e.PropertyName == "Language"))
			{
				return;
			}
			string language2 = clientSettings.Language;
			string text = PreferedUILanguage;
			if (string.IsNullOrWhiteSpace(PreferedUILanguage))
			{
				string language = clientSettings.Language;
				if (!string.IsNullOrWhiteSpace(language))
				{
					Application.Dispatcher.Invoke(() => LocalizeDictionary.Instance.Language = language);
				}
			}
			RaiseLanguageChange(language2, text);
		}
		catch (Exception ex)
		{
			LogAddError("Could not process settings change.", ex, LogCategories.Configuration);
		}
	}

	private byte[] OnScreenCaptureRequest(IntPtr windowHandle, bool includeCursor)
	{
		return CoreLib.Imaging.Imaging.CaptureWindowImageBytes(windowHandle, includeCursor);
	}

	private async void OnTrayIconEvent(object sender, System.Windows.Forms.MouseEventArgs e)
	{
		try
		{
			await DisableMaintenanceAsync();
		}
		catch (Exception ex)
		{
			LogAddError("DisableMaintenanceAsync", ex, LogCategories.Generic);
		}
	}

	private void OnSessionEnded(object sender, SessionEndedEventArgs e)
	{
		SystemEvents.SessionEnded -= OnSessionEnded;
		Stop(restart: false, crashed: false, ProcessExitCode.SessionEnded);
	}

	private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
	{
		SessionSwitchReason reason = e.Reason;
		if (reason == SessionSwitchReason.ConsoleDisconnect || (uint)(reason - 6) <= 1u)
		{
			SystemEvents.SessionSwitch -= OnSessionSwitch;
			Stop(restart: false, crashed: false, ProcessExitCode.SessionEnded);
		}
	}

	public T GetExportedValue<T>()
	{
		return PluginManager.GetExportedValue<T>();
	}

	public IEnumerable<T> GetExportedValues<T>()
	{
		return PluginManager.GetExportedValues<T>();
	}

	public IEnumerable<Lazy<T, TMetadataView>> GetExports<T, TMetadataView>()
	{
		return PluginManager.GetExports<T, TMetadataView>();
	}

	public void AddAssembly(Assembly assembly)
	{
		if (assembly == null)
		{
			throw new ArgumentNullException("assembly");
		}
		PluginManager.AddAssembly(assembly);
	}

	internal void LoadPlugins()
	{
		lock (INIT_OP_LOCK)
		{
			if (IsComposed)
			{
				return;
			}
			try
			{
				RaiseActivityChange(ClientStartupActivity.InitializingPlugins);
				PluginManager.Scope = ModuleScopes.Client;
				PluginManager.Configurations = Settings.Plugins;
				PluginManager.PluginsPath = Settings.PluginsPath;
				RaiseActivityChange(ClientStartupActivity.LoadingPlugins);
				PluginManager.Compose(this, out var errors);
				foreach (Exception item in errors)
				{
					if (item is ReflectionTypeLoadException)
					{
						Exception[] loaderExceptions = (item as ReflectionTypeLoadException).LoaderExceptions;
						foreach (Exception ex in loaderExceptions)
						{
							Log.AddError("Plugin load failed.", ex, LogCategories.Generic);
						}
					}
					else
					{
						Log.AddError("Failed to load plugin library.", item, LogCategories.Configuration);
					}
				}
			}
			catch (ReflectionTypeLoadException ex2)
			{
				Log.AddError("Plugins type load exception.", ex2, LogCategories.Configuration);
				Exception[] loaderExceptions = ex2.LoaderExceptions;
				foreach (Exception ex3 in loaderExceptions)
				{
					Log.AddError("Plugin load failed.", ex3, LogCategories.Generic);
				}
			}
			catch (Exception ex4)
			{
				Log.AddError("Plugins initialization failed.", ex4, LogCategories.Configuration);
			}
			finally
			{
				IsComposed = true;
			}
		}
	}

	protected void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
	{
	}

	protected Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
	{
		return null;
	}

	protected Assembly OnTypeResolve(object sender, ResolveEventArgs args)
	{
		return null;
	}

	public void TraceWrite(Exception ex, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
	{
		if (ex != null && TraceEnabled)
		{
			TraceWrite(ex.Message, ex.ToString(), memberName, sourceFilePath, sourceLineNumber);
		}
	}

	public void TraceWrite(string message, Exception ex, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
	{
		if (ex != null && TraceEnabled)
		{
			TraceWrite(message, ex.ToString(), memberName, sourceFilePath, sourceLineNumber);
		}
	}

	public void TraceWrite(string baseMessage, string detailMessage = null, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
	{
		if (!string.IsNullOrWhiteSpace(baseMessage) && TraceEnabled)
		{
			string text = $"{DateTime.Now} [{COMPILE_MODE}] {baseMessage} [{memberName}][{sourceLineNumber}][{sourceFilePath}]";
			text = ((detailMessage != null) ? $"{text}\nDetail: {detailMessage}" : text);
			Trace.WriteLine(text);
		}
	}

	private void OnDebugAssemblyLoad(object sender, AssemblyLoadEventArgs args)
	{
		if (TraceEnabled)
		{
			TraceWrite("Loaded " + args.LoadedAssembly.FullName, (string)null, "OnDebugAssemblyLoad", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Debugging.cs", 96);
		}
	}

	private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			try
			{
				Log.AddError("Unhandled exception occured. Client will exit.", ex, LogCategories.Generic);
			}
			catch
			{
				TraceWrite(ex, "OnAppDomainUnhandledException", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Debugging.cs", 109);
			}
		}
		Stop(restart: false, crashed: true);
	}

	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		try
		{
			if (ExceptionHelper.IsHandalableException(e.Exception))
			{
				e.Handled = true;
				Interlocked.Increment(ref unhandledUiExceptionsCount);
				if (unhandledUiExceptionsCount == 1)
				{
					Log.AddError("UI Unhandled exception occured. Client will continue executing.", e.Exception, LogCategories.Generic);
				}
			}
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "OnDispatcherUnhandledException", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Debugging.cs", 134);
		}
	}

	private void RaiseApplicationRated(int appId, IRating overallRating, IRating userRating)
	{
		EventHandler<ApplicationRateEventArgs> applicationRated = this.ApplicationRated;
		if (applicationRated == null)
		{
			return;
		}
		ApplicationRateEventArgs e = new ApplicationRateEventArgs(appId, overallRating, userRating);
		Delegate[] invocationList = applicationRated.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<ApplicationRateEventArgs> eventHandler = (EventHandler<ApplicationRateEventArgs>)invocationList[i];
			try
			{
				eventHandler(this, e);
			}
			catch (Exception ex)
			{
				Log.AddError("ApplicationRated handler exception occoured.", ex, LogCategories.Generic);
			}
		}
	}

	private void RaiseActivityChange(ClientStartupActivity activity)
	{
		EventHandler<ClientActivityEventArgs> activityChange = this.ActivityChange;
		if (activityChange == null)
		{
			return;
		}
		ClientActivityEventArgs e = new ClientActivityEventArgs(activity);
		Delegate[] invocationList = activityChange.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<ClientActivityEventArgs> eventHandler = (EventHandler<ClientActivityEventArgs>)invocationList[i];
			try
			{
				eventHandler(this, e);
			}
			catch (Exception ex)
			{
				Log.AddError("ContainerChange handler exception occoured.", ex, LogCategories.Generic);
			}
		}
	}

	private void RaiseOutOfOrderChange(bool isOutOfOrder)
	{
		EventHandler<OutOfOrderStateEventArgs> outOfOrderStateChange = this.OutOfOrderStateChange;
		OutOfOrderStateEventArgs outOfOrderStateEventArgs = new OutOfOrderStateEventArgs(isOutOfOrder);
		if (outOfOrderStateChange != null)
		{
			Delegate[] invocationList = outOfOrderStateChange.GetInvocationList();
			for (int i = 0; i < invocationList.Length; i++)
			{
				EventHandler<OutOfOrderStateEventArgs> eventHandler = (EventHandler<OutOfOrderStateEventArgs>)invocationList[i];
				try
				{
					eventHandler(this, outOfOrderStateEventArgs);
				}
				catch (Exception ex)
				{
					Log.AddError("OutOfOrderStateChange handler exception occoured.", ex, LogCategories.Generic);
				}
			}
		}
		EventNotifyOfAsync(ClientEventTypes.OutOfOrderState, outOfOrderStateEventArgs);
	}

	private void RaiseIdChange(int oldId, int newId)
	{
		IdChangeEventArgs idChangeEventArgs = new IdChangeEventArgs(newId, oldId);
		EventHandler<IdChangeEventArgs> idChange = this.IdChange;
		if (idChange != null)
		{
			Delegate[] invocationList = idChange.GetInvocationList();
			for (int i = 0; i < invocationList.Length; i++)
			{
				EventHandler<IdChangeEventArgs> eventHandler = (EventHandler<IdChangeEventArgs>)invocationList[i];
				try
				{
					eventHandler(this, idChangeEventArgs);
				}
				catch (Exception ex)
				{
					Log.AddError("IdChange handler exception occoured.", ex, LogCategories.Generic);
				}
			}
		}
		EventNotifyOfAsync(ClientEventTypes.IdChange, idChangeEventArgs);
	}

	private void RaiseSecurityChange(bool isEnabled, bool wasEnabled, bool activeProfile = false)
	{
		SecurityStateArgs securityStateArgs = new SecurityStateArgs(isEnabled, wasEnabled, activeProfile);
		EventHandler<SecurityStateArgs> securityStateChange = this.SecurityStateChange;
		if (securityStateChange != null)
		{
			Delegate[] invocationList = securityStateChange.GetInvocationList();
			for (int i = 0; i < invocationList.Length; i++)
			{
				EventHandler<SecurityStateArgs> eventHandler = (EventHandler<SecurityStateArgs>)invocationList[i];
				try
				{
					eventHandler(this, securityStateArgs);
				}
				catch (Exception ex)
				{
					Log.AddError("SecurityStateChange handler exception occoured.", ex, LogCategories.Generic);
				}
			}
		}
		EventNotifyOfAsync(ClientEventTypes.SecurityState, securityStateArgs);
	}

	private void RaiseMaintenanceModeChange(bool isEnabled)
	{
		MaintenanceEventArgs maintenanceEventArgs = new MaintenanceEventArgs(isEnabled);
		EventHandler<MaintenanceEventArgs> maintenanceModeChange = this.MaintenanceModeChange;
		if (maintenanceModeChange != null)
		{
			Delegate[] invocationList = maintenanceModeChange.GetInvocationList();
			for (int i = 0; i < invocationList.Length; i++)
			{
				EventHandler<MaintenanceEventArgs> eventHandler = (EventHandler<MaintenanceEventArgs>)invocationList[i];
				try
				{
					eventHandler(this, maintenanceEventArgs);
				}
				catch (Exception ex)
				{
					Log.AddError("MaintenanceModeChanged handler exception occoured.", ex, LogCategories.Generic);
				}
			}
		}
		EventNotifyOfAsync(ClientEventTypes.Maintenance, maintenanceEventArgs);
	}

	private void RaiseLockStateChage(bool isLocked)
	{
		LockStateEventArgs lockStateEventArgs = new LockStateEventArgs(isLocked);
		EventHandler<LockStateEventArgs> lockStateChange = this.LockStateChange;
		if (lockStateChange != null)
		{
			Delegate[] invocationList = lockStateChange.GetInvocationList();
			for (int i = 0; i < invocationList.Length; i++)
			{
				EventHandler<LockStateEventArgs> eventHandler = (EventHandler<LockStateEventArgs>)invocationList[i];
				try
				{
					eventHandler(this, lockStateEventArgs);
				}
				catch (Exception ex)
				{
					Log.AddError("LockStateChange handler exception occoured.", ex, LogCategories.Generic);
				}
			}
		}
		EventNotifyOfAsync(ClientEventTypes.LockState, lockStateEventArgs);
	}

	private void RaiseAppPoriflesChange(bool isInitial = false)
	{
		EventHandler<ProfilesChangeEventArgs> appProfilesChange = this.AppProfilesChange;
		if (appProfilesChange == null)
		{
			return;
		}
		ProfilesChangeEventArgs e = new ProfilesChangeEventArgs(isInitial);
		Delegate[] invocationList = appProfilesChange.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<ProfilesChangeEventArgs> eventHandler = (EventHandler<ProfilesChangeEventArgs>)invocationList[i];
			try
			{
				eventHandler(this, e);
			}
			catch (Exception ex)
			{
				Log.AddError("AppProfilesChanged handler exception occoured.", ex, LogCategories.Generic);
			}
		}
	}

	private void RaiseSecuirtyPoriflesChanged(bool isInitial = false)
	{
		EventHandler<ProfilesChangeEventArgs> securityProfilesChange = this.SecurityProfilesChange;
		if (securityProfilesChange == null)
		{
			return;
		}
		ProfilesChangeEventArgs e = new ProfilesChangeEventArgs(isInitial);
		Delegate[] invocationList = securityProfilesChange.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<ProfilesChangeEventArgs> eventHandler = (EventHandler<ProfilesChangeEventArgs>)invocationList[i];
			try
			{
				eventHandler(this, e);
			}
			catch (Exception ex)
			{
				Log.AddError("SecurityProfilesChanged handler exception occoured.", ex, LogCategories.Generic);
			}
		}
	}

	private void RaiseGroupConfigurationChanged()
	{
		EventHandler<EventArgs> groupConfigurationChange = this.GroupConfigurationChange;
		if (groupConfigurationChange == null)
		{
			return;
		}
		Delegate[] invocationList = groupConfigurationChange.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<EventArgs> eventHandler = (EventHandler<EventArgs>)invocationList[i];
			try
			{
				eventHandler(this, new EventArgs());
			}
			catch (Exception ex)
			{
				Log.AddError("GroupConfigurationChanged handler exception occoured.", ex, LogCategories.Generic);
			}
		}
	}

	private void RaiseUserPasswordChange(string newPassword)
	{
		EventHandler<UserPasswordChangeEventArgs> userPasswordChange = this.UserPasswordChange;
		if (userPasswordChange == null)
		{
			return;
		}
		UserPasswordChangeEventArgs e = new UserPasswordChangeEventArgs(newPassword);
		Delegate[] invocationList = userPasswordChange.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<UserPasswordChangeEventArgs> eventHandler = (EventHandler<UserPasswordChangeEventArgs>)invocationList[i];
			try
			{
				eventHandler(this, e);
			}
			catch (Exception ex)
			{
				Log.AddError("UserPasswordChange handler exception occoured.", ex, LogCategories.Generic);
			}
		}
	}

	private void RaiseUserProfileChange(IUserProfile newUserProfile, IUserProfile oldUserProfile)
	{
		EventHandler<UserProfileChangeArgs> userProfileChange = this.UserProfileChange;
		if (userProfileChange == null)
		{
			return;
		}
		UserProfileChangeArgs e = new UserProfileChangeArgs(newUserProfile, oldUserProfile);
		Delegate[] invocationList = userProfileChange.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<UserProfileChangeArgs> eventHandler = (EventHandler<UserProfileChangeArgs>)invocationList[i];
			try
			{
				eventHandler(this, e);
			}
			catch (Exception ex)
			{
				Log.AddError("UserProfileChange handler exception occoured.", ex, LogCategories.Generic);
			}
		}
	}

	private void RaiseLoginStateChange(object sender, UserEventArgs args)
	{
		try
		{
			this.LoginStateChange?.Invoke(sender, args);
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "RaiseLoginStateChange", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Events.cs", 345);
		}
	}

	private void RaiseContextCollectionChange(object sender, CollectionChangeEventArgs args)
	{
		EventHandler<CollectionChangeEventArgs> executionContextCollectionChange = this.ExecutionContextCollectionChange;
		if (executionContextCollectionChange == null)
		{
			return;
		}
		Delegate[] invocationList = executionContextCollectionChange.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<CollectionChangeEventArgs> eventHandler = (EventHandler<CollectionChangeEventArgs>)invocationList[i];
			try
			{
				eventHandler(sender, args);
			}
			catch (Exception ex)
			{
				Log.AddError("ExecutionContextCollectionChange handler exception occoured.", ex, LogCategories.Generic);
			}
		}
	}

	private void RaiseContextStateChange(object sender, ExecutionContextStateArgs e)
	{
		EventHandler<ExecutionContextStateArgs> executionContextStateChage = this.ExecutionContextStateChage;
		if (executionContextStateChage == null)
		{
			return;
		}
		Delegate[] invocationList = executionContextStateChage.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<ExecutionContextStateArgs> eventHandler = (EventHandler<ExecutionContextStateArgs>)invocationList[i];
			try
			{
				eventHandler(sender, e);
			}
			catch (Exception ex)
			{
				Log.AddError("ExecutionContextStateChage proxy handler exception occoured.", ex, LogCategories.Generic);
			}
		}
	}

	private void RaiseLanguageChange(string settingLanguage, string preferedUILanguage)
	{
		try
		{
			this.LanguageChange?.Invoke(this, new LanguageChangeEventArgs(settingLanguage, preferedUILanguage));
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "RaiseLanguageChange", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Events.cs", 395);
		}
	}

	internal void RaiseReservationChange()
	{
		try
		{
			ReservationChangeEventArgs e = new ReservationChangeEventArgs();
			this.ReservationChange?.Invoke(this, e);
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "RaiseReservationChange", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Events.cs", 408);
		}
	}

	internal void RaiseGracePeriodChange(bool isInGracePeriod, int gracePeriodTime = 0)
	{
		try
		{
			GracePeriodChangeEventArgs gracePeriodChangeEventArgs = new GracePeriodChangeEventArgs();
			gracePeriodChangeEventArgs.IsInGracePeriod = isInGracePeriod;
			gracePeriodChangeEventArgs.GracePeriodTime = gracePeriodTime;
			this.GracePeriodChange?.Invoke(this, gracePeriodChangeEventArgs);
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "RaiseGracePeriodChange", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Events.cs", 423);
		}
	}

	internal void RaiseUserAgreementsLoaded(int? userId, bool hasPendingUserAgreements, List<UserAgreement> userAgreements, List<UserAgreementState> userAgreementStates)
	{
		try
		{
			UserAgreementsLoadedEventArgs userAgreementsLoadedEventArgs = new UserAgreementsLoadedEventArgs();
			userAgreementsLoadedEventArgs.UserId = userId;
			userAgreementsLoadedEventArgs.HasPendingUserAgreements = hasPendingUserAgreements;
			userAgreementsLoadedEventArgs.UserAgreements = userAgreements;
			userAgreementsLoadedEventArgs.UserAgreementStates = userAgreementStates;
			this.UserAgreementsLoaded?.Invoke(this, userAgreementsLoadedEventArgs);
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "RaiseUserAgreementsLoaded", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Events.cs", 440);
		}
	}

	internal void RaiseUserLockChange(bool isLocked)
	{
		try
		{
			UserLockChangeEventArgs userLockChangeEventArgs = new UserLockChangeEventArgs();
			userLockChangeEventArgs.IsLocked = isLocked;
			this.UserLockChange?.Invoke(this, userLockChangeEventArgs);
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "RaiseUserLockChange", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Events.cs", 454);
		}
	}

	private System.Threading.Tasks.Task EventNotifyOfAsync(ClientEventTypes type, EventArgs args)
	{
		if (args == null)
		{
			throw new ArgumentNullException("args");
		}
		return System.Threading.Tasks.Task.Factory.StartNew(delegate
		{
			if (Dispatcher.IsValid)
			{
				ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.EventNotification, type, args);
				syncOperation.StartEx();
				syncOperation.WaitCompleteEx();
			}
		}).ContinueWith(delegate(System.Threading.Tasks.Task t)
		{
			Log.AddError("EventNotifyOfAsync failed.", t.Exception, LogCategories.Dispatcher);
		}, TaskContinuationOptions.OnlyOnFaulted);
	}

	internal void ProcessEventArgs(UserBalanceEventArgs args)
	{
		if (args == null)
		{
			throw new ArgumentNullException("args");
		}
		this.UserBalanceChange?.Invoke(this, args);
		UserBalance balance = args.Balance;
		if (balance == null)
		{
			return;
		}
		double? num = balance.AvailableCreditedTime / 60.0;
		Environment.SetEnvironmentVariable("USERMINUTESLEFT", num?.ToString() ?? string.Empty);
		if (!num.HasValue)
		{
			return;
		}
		int num2 = (int)num.Value;
		if (num2 <= 0)
		{
			return;
		}
		if (Settings.TimeNotifications != null && Settings.TimeNotifications.Count > 0)
		{
			bool flag = false;
			{
				foreach (TimeNotification timeNotification in Settings.TimeNotifications)
				{
					int timeLeftWarning = timeNotification.TimeLeftWarning;
					TimeLeftWarningType timeLeftWarningType = timeNotification.TimeLeftWarningType;
					if (timeLeftWarningType == TimeLeftWarningType.None || timeLeftWarning <= 0)
					{
						continue;
					}
					if (!USER_TIME_NOTIFICATIONS.ContainsKey(timeLeftWarning))
					{
						USER_TIME_NOTIFICATIONS.Add(timeLeftWarning, value: false);
					}
					if (num2 <= timeLeftWarning)
					{
						if (USER_TIME_NOTIFICATIONS[timeLeftWarning])
						{
							continue;
						}
						if (!flag)
						{
							NotifyUserTime(num2, timeLeftWarningType).ContinueWith(delegate(System.Threading.Tasks.Task x)
							{
								if (x.Exception != null)
								{
									try
									{
										LogAddError("NotifyUserTime failed.", x.Exception, LogCategories.Generic);
									}
									catch (Exception ex2)
									{
										TraceWrite(ex2, "ProcessEventArgs", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Events.cs", 555);
									}
								}
							}, TaskContinuationOptions.OnlyOnFaulted);
							flag = true;
						}
						USER_TIME_NOTIFICATIONS[timeLeftWarning] = true;
					}
					else
					{
						USER_TIME_NOTIFICATIONS[timeLeftWarning] = false;
					}
				}
				return;
			}
		}
		int timeLeftWarning2 = Settings.TimeLeftWarning;
		TimeLeftWarningType timeLeftWarningType2 = Settings.TimeLeftWarningType;
		if (timeLeftWarningType2 == TimeLeftWarningType.None || timeLeftWarning2 <= 0)
		{
			return;
		}
		if (num2 <= timeLeftWarning2)
		{
			if (USER_TIME_NOTIFIED)
			{
				return;
			}
			NotifyUserTime(num2, timeLeftWarningType2).ContinueWith(delegate(System.Threading.Tasks.Task x)
			{
				if (x.Exception != null)
				{
					try
					{
						LogAddError("NotifyUserTime failed.", x.Exception, LogCategories.Generic);
					}
					catch (Exception ex)
					{
						TraceWrite(ex, "ProcessEventArgs", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Events.cs", 599);
					}
				}
			}, TaskContinuationOptions.OnlyOnFaulted);
			USER_TIME_NOTIFIED = true;
		}
		else
		{
			USER_TIME_NOTIFIED = false;
		}
	}

	internal void ProcessEventArgs(UsageSessionChangedEventArgs args)
	{
		if (args == null)
		{
			throw new ArgumentNullException("args");
		}
		this.UsageSessionChanged?.Invoke(this, args);
	}

	internal void ProcessEventArgs(AppRatedEventArgs args)
	{
		if (args == null)
		{
			throw new ArgumentNullException("args");
		}
		if (APP_LOOK_UP.TryGetValue(args.AppId, out var value))
		{
			value.Rating = args.AppRating;
			value.TotalRates = args.RatesCount;
		}
	}

	internal void ProcessEventArgs(AppStatEventArgs args)
	{
		if (args == null)
		{
			throw new ArgumentNullException("args");
		}
		if (APP_LOOK_UP.TryGetValue(args.AppId, out var value))
		{
			value.TotalExecutions = args.TotalAppExecutions;
			value.TotalExecutionTime = args.TotalAppTime;
		}
		if (APP_EXE_LOOK_UP.TryGetValue(args.AppExeId, out var value2))
		{
			value2.TotalTime = args.TotalAppExeTime;
			value2.TotalExecutions = args.TotalAppExeExecutions;
			if (CurrentUser?.Id == args.UserId)
			{
				value2.TotalUserTime = args.TotalAppExeUserTime;
				value2.TotalUserExecutions = args.TotalAppExeUserExecutions;
			}
		}
	}

	internal void ProcessEventArgs(IEntityEventArgs args)
	{
		if (args == null)
		{
			throw new ArgumentNullException("args");
		}
		if (args is EntityEventArgs<AppEnterprise> entityEventArgs)
		{
			switch (args.Type)
			{
			case GizmoDALV2.EntityEventType.Added:
			case GizmoDALV2.EntityEventType.Modified:
			{
				foreach (AppEnterprise addedItem in entityEventArgs.AddedItems)
				{
					CreateViewModel(addedItem);
				}
				break;
			}
			case GizmoDALV2.EntityEventType.Removed:
			{
				foreach (AppEnterprise removedItem in entityEventArgs.RemovedItems)
				{
					foreach (AppViewModel value in APP_LOOK_UP.Values)
					{
						if (value.PublisherId == removedItem.Id)
						{
							value.PublisherId = null;
						}
						if (value.DeveloperId == removedItem.Id)
						{
							value.DeveloperId = null;
						}
					}
					if (!APP_ENTERPRISE_LOOK_UP.TryRemove(removedItem.Id, out var model6))
					{
						continue;
					}
					BindingOperations.AccessCollection(APP_ENTERPRISE_STORE, delegate
					{
						if (APP_ENTERPRISE_STORE.Contains(model6))
						{
							APP_ENTERPRISE_STORE.Remove(model6);
						}
					}, writeAccess: true);
				}
				break;
			}
			}
			return;
		}
		if (args is EntityEventArgs<AppCategory> entityEventArgs2)
		{
			switch (args.Type)
			{
			case GizmoDALV2.EntityEventType.Added:
			case GizmoDALV2.EntityEventType.Modified:
			{
				foreach (AppCategory addedItem2 in entityEventArgs2.AddedItems)
				{
					CreateViewModel(addedItem2);
				}
				break;
			}
			case GizmoDALV2.EntityEventType.Removed:
			{
				foreach (AppCategory removedItem2 in entityEventArgs2.RemovedItems)
				{
					if (!CATEGORY_LOOK_UP.TryRemove(removedItem2.Id, out var model5))
					{
						continue;
					}
					BindingOperations.AccessCollection(APP_CATEGORY_STORE, delegate
					{
						if (APP_CATEGORY_STORE.Contains(model5))
						{
							APP_CATEGORY_STORE.Remove(model5);
						}
					}, writeAccess: true);
				}
				break;
			}
			}
			return;
		}
		if (args is EntityEventArgs<App> entityEventArgs3)
		{
			switch (args.Type)
			{
			case GizmoDALV2.EntityEventType.Added:
			case GizmoDALV2.EntityEventType.Modified:
			{
				foreach (App addedItem3 in entityEventArgs3.AddedItems)
				{
					CreateViewModel(addedItem3);
				}
				break;
			}
			case GizmoDALV2.EntityEventType.Removed:
			{
				foreach (App removedItem3 in entityEventArgs3.RemovedItems)
				{
					if (!APP_LOOK_UP.TryRemove(removedItem3.Id, out var model4))
					{
						continue;
					}
					BindingOperations.AccessCollection(APP_CATEGORY_STORE, delegate
					{
						if (APP_STORE.Contains(model4))
						{
							APP_STORE.Remove(model4);
						}
					}, writeAccess: true);
				}
				break;
			}
			}
			return;
		}
		if (args is EntityEventArgs<AppExe> entityEventArgs4)
		{
			switch (args.Type)
			{
			case GizmoDALV2.EntityEventType.Added:
			case GizmoDALV2.EntityEventType.Modified:
				foreach (AppExe addedItem4 in entityEventArgs4.AddedItems)
				{
					CreateViewModel(addedItem4);
				}
				break;
			case GizmoDALV2.EntityEventType.Removed:
				foreach (AppExe removedItem4 in entityEventArgs4.RemovedItems)
				{
					if (!APP_EXE_LOOK_UP.TryRemove(removedItem4.Id, out var model3))
					{
						continue;
					}
					BindingOperations.AccessCollection(APP_CATEGORY_STORE, delegate
					{
						if (APP_EXE_STORE.Contains(model3))
						{
							APP_EXE_STORE.Remove(model3);
						}
					}, writeAccess: true);
				}
				break;
			}
			IEnumerable<int> enumerable = from APP_EXE in entityEventArgs4.AddedItems.Union(entityEventArgs4.RemovedItems)
				select APP_EXE.Id;
			Dictionary<int, int> markedDeploymentProfiles = GetMarkedDeploymentProfiles();
			{
				foreach (int APP_EXE_ID in enumerable)
				{
					foreach (int item in from PROFILE in markedDeploymentProfiles
						where PROFILE.Value == APP_EXE_ID
						select PROFILE.Key)
					{
						UnmarkDeploymentProfile(item);
					}
					DestroyExecutionContext(APP_EXE_ID, release: true);
				}
				return;
			}
		}
		if (args is EntityEventArgs<Feed> entityEventArgs5)
		{
			switch (args.Type)
			{
			case GizmoDALV2.EntityEventType.Added:
			case GizmoDALV2.EntityEventType.Modified:
			{
				foreach (Feed addedItem5 in entityEventArgs5.AddedItems)
				{
					CreateViewModel(addedItem5);
				}
				break;
			}
			case GizmoDALV2.EntityEventType.Removed:
			{
				foreach (Feed removedItem5 in entityEventArgs5.RemovedItems)
				{
					if (!FEEDS_LOOKUP.TryRemove(removedItem5.Id, out var model2))
					{
						continue;
					}
					BindingOperations.AccessCollection(NEWS_STORE, delegate
					{
						if (NEWS_STORE.Contains(model2))
						{
							NEWS_STORE.Remove(model2);
						}
					}, writeAccess: true);
				}
				break;
			}
			}
		}
		else if (args is EntityEventArgs<News> entityEventArgs6)
		{
			switch (args.Type)
			{
			case GizmoDALV2.EntityEventType.Added:
			case GizmoDALV2.EntityEventType.Modified:
			{
				foreach (News addedItem6 in entityEventArgs6.AddedItems)
				{
					CreateViewModel(addedItem6);
				}
				break;
			}
			case GizmoDALV2.EntityEventType.Removed:
			{
				foreach (News removedItem6 in entityEventArgs6.RemovedItems)
				{
					if (!NEWS_LOOKUP.TryRemove(removedItem6.Id, out var model))
					{
						continue;
					}
					BindingOperations.AccessCollection(NEWS_STORE, delegate
					{
						if (NEWS_STORE.Contains(model))
						{
							NEWS_STORE.Remove(model);
						}
					}, writeAccess: true);
				}
				break;
			}
			}
		}
		else if (args is EntityEventArgs<AppGroup>)
		{
			switch (args.Type)
			{
			}
		}
	}

	internal void ProcessEventArgs(OrderStatusChangeEventArgs args)
	{
		if (args == null)
		{
			throw new ArgumentNullException("args");
		}
		this.OrderStatusChange?.Invoke(this, args);
		if (args.NewStatus == SharedLib.OrderStatus.Accepted && args.OldStatus == SharedLib.OrderStatus.OnHold)
		{
			NotifyOrderStatusAsync(SharedLib.OrderStatus.Accepted).ContinueWith(delegate
			{
			}, TaskContinuationOptions.OnlyOnFaulted).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (args.NewStatus == SharedLib.OrderStatus.Canceled)
		{
			NotifyOrderStatusAsync(SharedLib.OrderStatus.Canceled).ContinueWith(delegate
			{
			}, TaskContinuationOptions.OnlyOnFaulted).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (args.NewStatus == SharedLib.OrderStatus.OnHold)
		{
			NotifyOrderStatusAsync(SharedLib.OrderStatus.OnHold).ContinueWith(delegate
			{
			}, TaskContinuationOptions.OnlyOnFaulted).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public bool TryGetExecutionContext(int appExeId, out ExecutionContext cx)
	{
		cx = null;
		lock (ExecutionContexts)
		{
			return ExecutionContexts.TryGetValue(appExeId, out cx);
		}
	}

	public async Task<AsyncContextResult> GetExecutionContextAsync(int appExeId, CancellationToken ct)
	{
		if (TryGetExecutionContext(appExeId, out var cx))
		{
			return new AsyncContextResult(cx, result: true);
		}
		await EXE_CONTEXT_ASYNC_LOCK.WaitAsync(ct);
		try
		{
			AppExe appExe = await AppExeExecutionGraphGetAsync(appExeId);
			Executable executable = Convert(appExe);
			ApplicationProfile application = Convert(appExe.App);
			ExecutionContext executionContext = GetExecutionContext(executable, application);
			return new AsyncContextResult(executionContext, executionContext != null);
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "GetExecutionContextAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 115);
			return new AsyncContextResult(null, result: false);
		}
		finally
		{
			EXE_CONTEXT_ASYNC_LOCK.Release();
		}
	}

	public ExecutionContext GetExecutionContext(Executable executable, ApplicationProfile application)
	{
		if (executable == null)
		{
			throw new NullReferenceException("Executable profile may not be null.");
		}
		if (application == null)
		{
			throw new NullReferenceException("Application profile may not be null.");
		}
		ExecutionContext executionContext = null;
		lock (ExecutionContexts)
		{
			if (ExecutionContexts.ContainsKey(executable.ID))
			{
				return ExecutionContexts[executable.ID];
			}
			executionContext = new ExecutionContext(executable, application, this);
			executionContext.ExecutionStateChaged += RaiseContextStateChange;
			ExecutionContexts.Add(executable.ID, executionContext);
			RaiseContextCollectionChange(this, new CollectionChangeEventArgs(CollectionChangeAction.Add, executionContext));
			return executionContext;
		}
	}

	public void DestroyExecutionContext(int exeId, bool release = false, int waitFinalizeTime = 0)
	{
		TraceWrite("Entering DestroyExecutionContext", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 171);
		lock (ExecutionContexts)
		{
			TraceWrite("Entered DestroyExecutionContext", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 174);
			if (ExecutionContexts.TryGetValue(exeId, out var value))
			{
				TraceWrite("Execution context found, exe name : " + (value.Executable?.ExecutableName ?? "Unknown"), (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 177);
				try
				{
					if (release)
					{
						if (!value.IsReleased)
						{
							TraceWrite($"Releasing execution context, exe id : {exeId}", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 186);
							value.Release();
							TraceWrite($"Released execution context, exe id : {exeId}", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 188);
						}
						else
						{
							TraceWrite($"Execution context already released, exe id : {exeId}", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 192);
						}
					}
					else if (!value.IsDestroyed)
					{
						TraceWrite($"Destroying execution context, exe id : {exeId}", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 200);
						value.Destroy();
						TraceWrite($"Destroyed execution context, exe id : {exeId}", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 202);
					}
					else
					{
						TraceWrite($"Execution context already destroyed, exe id : {exeId}", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 206);
					}
					value.ExecutionStateChaged -= RaiseContextStateChange;
					TraceWrite($"Removing execution context, exe id : {exeId}", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 214);
					ExecutionContexts.Remove(exeId);
					TraceWrite($"Removed execution context, exe id : {exeId}", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 216);
					TraceWrite($"Raising execution context event, exe id : {exeId}", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 219);
					RaiseContextCollectionChange(this, new CollectionChangeEventArgs(CollectionChangeAction.Remove, value));
					TraceWrite($"Raised execution context event, exe id : {exeId}", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 221);
					return;
				}
				catch (Exception ex)
				{
					Log.AddError("Context destruction failed.", ex, LogCategories.Operation);
					return;
				}
			}
			TraceWrite($"Execution context not found, exe id : {exeId}", (string)null, "DestroyExecutionContext", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 230);
		}
	}

	public void DestroyContexts(bool release = false, int waitFinalizeTime = 0)
	{
		TraceWrite("Entering DestroyContexts", (string)null, "DestroyContexts", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 241);
		lock (ExecutionContexts)
		{
			TraceWrite("Entered DestroyContexts", (string)null, "DestroyContexts", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 244);
			int[] array = ExecutionContexts.Keys.ToArray();
			foreach (int exeId in array)
			{
				DestroyExecutionContext(exeId, release, waitFinalizeTime);
			}
		}
		TraceWrite("Exiting DestroyContexts", (string)null, "DestroyContexts", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 250);
	}

	public async System.Threading.Tasks.Task ExecutionContextKillAsync(CancellationToken ct = default(CancellationToken))
	{
		await EXE_CONTEXT_ASYNC_LOCK.WaitAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			IEnumerable<ExecutionContext> enumerable = executionContexts?.Values;
			foreach (ExecutionContext item in (enumerable ?? Enumerable.Empty<ExecutionContext>()).Where((ExecutionContext context) => !context.Executable.Options.HasFlag(ExecutableOptionType.IgnoreConcurrentExecutionLimit)).ToList())
			{
				try
				{
					ct.ThrowIfCancellationRequested();
					try
					{
						if (!item.IsAborting)
						{
							item.Abort(async: true);
						}
					}
					catch (ArgumentException)
					{
					}
					item.Kill();
				}
				catch (Exception ex2)
				{
					TraceWrite(ex2, "ExecutionContextKillAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Execution.cs", 291);
				}
			}
		}
		catch
		{
			throw;
		}
		finally
		{
			EXE_CONTEXT_ASYNC_LOCK.Release();
		}
	}

	public async Task<int> ExecutionContextActiveCountGetAsync(int callingExecutableId, CancellationToken ct = default(CancellationToken))
	{
		await EXE_CONTEXT_ASYNC_LOCK.WaitAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			return (from cx in executionContexts?.Values
				where cx.Executable.ID != callingExecutableId
				where (!cx.Executable.Options.HasFlag(ExecutableOptionType.IgnoreConcurrentExecutionLimit) && cx.IsAlive) || cx.IsExecuting
				select cx).Count() ?? 0;
		}
		catch
		{
			throw;
		}
		finally
		{
			EXE_CONTEXT_ASYNC_LOCK.Release();
		}
	}

	internal bool IsMarkedPersonalUserFile(int profileId)
	{
		lock (MarkedPersonalUserFiles)
		{
			return MarkedPersonalUserFiles.Keys.Contains(profileId);
		}
	}

	internal bool MarkPersonalUserFile(int profileId, int executableId)
	{
		lock (MarkedPersonalUserFiles)
		{
			if (!MarkedPersonalUserFiles.ContainsKey(profileId))
			{
				MarkedPersonalUserFiles.Add(profileId, executableId);
				return true;
			}
			return false;
		}
	}

	internal bool UnmarkPersonalUserFile(int profileId)
	{
		lock (MarkedPersonalUserFiles)
		{
			return MarkedPersonalUserFiles.Remove(profileId);
		}
	}

	internal Dictionary<int, int> GetMarkedPersonalUserFiles()
	{
		lock (MarkedPersonalUserFiles)
		{
			return MarkedPersonalUserFiles.ToDictionary((KeyValuePair<int, int> k) => k.Key, (KeyValuePair<int, int> v) => v.Value);
		}
	}

	internal void ClearMarkedPersonalUserFiles()
	{
		lock (MarkedPersonalUserFiles)
		{
			MarkedPersonalUserFiles.Clear();
		}
	}

	internal bool IsMarkedDeploymentProfile(int profileId)
	{
		lock (MarkedDeployementProfiles)
		{
			return MarkedDeployementProfiles.Keys.Contains(profileId);
		}
	}

	internal bool MarkDeploymentProfile(int profileId, int executableId)
	{
		lock (MarkedDeployementProfiles)
		{
			if (!MarkedDeployementProfiles.ContainsKey(profileId))
			{
				MarkedDeployementProfiles.Add(profileId, executableId);
				return true;
			}
			return false;
		}
	}

	internal bool UnmarkDeploymentProfile(int profileId)
	{
		lock (MarkedDeployementProfiles)
		{
			return MarkedDeployementProfiles.Remove(profileId);
		}
	}

	internal Dictionary<int, int> GetMarkedDeploymentProfiles()
	{
		lock (MarkedDeployementProfiles)
		{
			return MarkedDeployementProfiles.ToDictionary((KeyValuePair<int, int> k) => k.Key, (KeyValuePair<int, int> v) => v.Value);
		}
	}

	internal void ClearMarkedDeploymentProfiles()
	{
		lock (MarkedDeployementProfiles)
		{
			MarkedDeployementProfiles.Clear();
		}
	}

	public bool TryMakeFreeSpace(string destinationPath, long ammount, IEnumerable<string> ignoredPaths, IAbortHandle abortHandle)
	{
		if (string.IsNullOrWhiteSpace(destinationPath))
		{
			throw new ArgumentNullException("Destination path cannot be null or empty.", "destinationPath");
		}
		if (ammount <= 0)
		{
			throw new ArgumentNullException("Ammount cannot be less or equal to zero.", "ammount");
		}
		if (ignoredPaths == null)
		{
			throw new ArgumentNullException("Ignored paths cannot be null.", "ignoredPaths");
		}
		string pathRoot = Path.GetPathRoot(destinationPath);
		DriveInfo driveInfo = new DriveInfo(pathRoot);
		if (!driveInfo.IsReady)
		{
			throw new DriveNotFoundException(pathRoot + " not found.");
		}
		foreach (KeyValuePair<int, HashSet<string>> deploymentPath in GetDeploymentPathList(pathRoot))
		{
			foreach (string DEPLOYMENT_PATH in deploymentPath.Value)
			{
				if (ignoredPaths.Any((string PATH) => PATH.StartsWith(DEPLOYMENT_PATH, StringComparison.InvariantCultureIgnoreCase)) || HandleManager.IsSystemPath(DEPLOYMENT_PATH) || !cyDirectory.Exists(DEPLOYMENT_PATH))
				{
					continue;
				}
				try
				{
					Deployment deployment = DeploymentGet(deploymentPath.Key);
					if (deployment == null || deployment.Options.HasFlag(DeployOptionType.IgnoreCleanup))
					{
						continue;
					}
					cyStructure cyStructure = new cyStructure(DEPLOYMENT_PATH)
					{
						DirectoryFilter = BuildFilter(deployment.IncludeDirectories, deployment.ExcludeDirectories),
						FileFilter = BuildFilter(deployment.IncludeFiles, deployment.ExcludeFiles)
					};
					cyStructure.Get(deployment.Options.HasFlag(DeployOptionType.IncludeSubDirectories), abortHandle);
					if (abortHandle.IsAborted)
					{
						break;
					}
					foreach (IcyFileSystemInfo file in cyStructure.GetFiles())
					{
						if (abortHandle.IsAborted)
						{
							break;
						}
						try
						{
							if (cyFile.Exists(file.FullName))
							{
								file.Delete();
							}
						}
						catch (Exception ex)
						{
							Log.AddError("Error cleaning path " + DEPLOYMENT_PATH + ", could not delete file '" + file.FullName + "'.", ex, LogCategories.Operation);
						}
					}
					foreach (IcyFileSystemInfo directory in cyStructure.GetDirectories())
					{
						if (abortHandle.IsAborted)
						{
							break;
						}
						try
						{
							if (cyDirectory.Exists(directory.FullName))
							{
								directory.Delete();
							}
						}
						catch (Exception ex2)
						{
							Log.AddError("Error cleaning path " + DEPLOYMENT_PATH + ", could not delete directory '" + directory.FullName + "'.", ex2, LogCategories.Operation);
						}
					}
					goto IL_0302;
				}
				catch (Exception ex3)
				{
					Log.AddError($"Error cleaning path {DEPLOYMENT_PATH} during free space making operation.", ex3, LogCategories.Operation);
					goto IL_0302;
				}
				finally
				{
					UnmarkDeploymentProfile(deploymentPath.Key);
				}
				IL_0302:
				if (driveInfo.AvailableFreeSpace >= ammount)
				{
					return true;
				}
			}
		}
		return false;
	}

	public string BuildFilter(string inclusions, string exclusions)
	{
		return BuildFilter(inclusions, inclusion: true) + BuildFilter(exclusions, inclusion: false);
	}

	public string BuildFilter(string filter, bool inclusion)
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (!string.IsNullOrWhiteSpace(filter))
		{
			string text = (inclusion ? "+" : "-");
			string[] array = filter.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
			foreach (string text2 in array)
			{
				if (string.IsNullOrWhiteSpace(text2))
				{
					continue;
				}
				string text3 = text2;
				if (text3.EndsWith(cyPath.DIRECTORY_SEPERATOR_CHAR))
				{
					text3 = text2.Substring(0, text3.Length - 1);
				}
				text3 = Wildcard.WildcardToRegex(text3);
				if (!string.IsNullOrWhiteSpace(text3))
				{
					if (!text3.EndsWith(";"))
					{
						text3 += ";";
					}
					stringBuilder.Append(text + text3);
				}
			}
		}
		return stringBuilder.ToString();
	}

	internal void CleanPersonalFile(string DESTINATION_PATH, string FILE_FILTER, string DIRECTORY_FILTER, bool INCLUDE_SUB_DIRECTORIES, string PERSONAL_FILE_NAME, bool IS_REGISTRY)
	{
		if (string.IsNullOrWhiteSpace(DESTINATION_PATH))
		{
			throw new ArgumentNullException("DESTINATION_PATH");
		}
		if (string.IsNullOrWhiteSpace(PERSONAL_FILE_NAME))
		{
			throw new ArgumentNullException("PERSONAL_FILE_NAME");
		}
		try
		{
			if (IS_REGISTRY)
			{
				CoreRegistryKey coreRegistryKey = new CoreRegistryKey(DESTINATION_PATH);
				if (coreRegistryKey.Exists)
				{
					coreRegistryKey.DeleteTree();
				}
				return;
			}
			cyDirectoryInfo cyDirectoryInfo = new cyDirectoryInfo(DESTINATION_PATH);
			if (!cyDirectoryInfo.Exists)
			{
				return;
			}
			cyStructure cyStructure = new cyStructure(cyDirectoryInfo);
			cyStructure.DirectoryFilter = DIRECTORY_FILTER;
			cyStructure.FileFilter = FILE_FILTER;
			cyStructure.Get(INCLUDE_SUB_DIRECTORIES);
			foreach (IcyFileSystemInfo entry in cyStructure.Entries)
			{
				try
				{
					if (entry.GetFileInfo())
					{
						entry.Delete();
					}
				}
				catch (Exception ex)
				{
					string informationMessage = "Could not delete entry while performing personal user file cleanup " + PERSONAL_FILE_NAME + " entry " + entry.FullName + ".";
					Log.AddError(informationMessage, ex, LogCategories.Operation);
				}
			}
		}
		catch
		{
			throw;
		}
	}

	internal void DeployPersonalFile(int PERSONAL_FILE_ID, string DESTINATION_PATH, string PERSONAL_FILE_NAME, bool IS_REGISTRY)
	{
		if (!IS_REGISTRY && string.IsNullOrWhiteSpace(DESTINATION_PATH))
		{
			throw new ArgumentNullException("DESTINATION_PATH");
		}
		if (string.IsNullOrWhiteSpace(PERSONAL_FILE_NAME))
		{
			throw new ArgumentNullException("PERSONAL_FILE_NAME");
		}
		try
		{
			using Stream stream = PersonalFileStreamGet(PERSONAL_FILE_ID);
			if (stream == null)
			{
				return;
			}
			using ZipInputStream zipInputStream = new ZipInputStream(stream);
			if (IS_REGISTRY)
			{
				ZipEntry nextEntry;
				while ((nextEntry = zipInputStream.GetNextEntry()) != null)
				{
					if (nextEntry.IsFile & (string.Compare(nextEntry.Name, PERSONAL_FILE_NAME + ".reg", ignoreCase: true) == 0) & (nextEntry.Size > 0))
					{
						CoreRegistryFile coreRegistryFile = new CoreRegistryFile();
						using (MemoryStream memoryStream = new MemoryStream())
						{
							StreamUtils.Copy(zipInputStream, memoryStream, new byte[524288]);
							coreRegistryFile.LoadFromStream(memoryStream);
						}
						coreRegistryFile.Import();
						break;
					}
				}
				return;
			}
			ZipEntry nextEntry2;
			while ((nextEntry2 = zipInputStream.GetNextEntry()) != null)
			{
				string text = nextEntry2.Name.Replace('/', Path.DirectorySeparatorChar);
				if (Path.IsPathRooted(text))
				{
					text = text.Substring(Path.GetPathRoot(text).Length);
				}
				string text2 = Path.Combine(DESTINATION_PATH, text);
				string directoryName = Path.GetDirectoryName(text2);
				IcyFileSystemInfo icyFileSystemInfo2;
				if (!nextEntry2.IsFile)
				{
					IcyFileSystemInfo icyFileSystemInfo = new cyDirectoryInfo(text2);
					icyFileSystemInfo2 = icyFileSystemInfo;
				}
				else
				{
					IcyFileSystemInfo icyFileSystemInfo = new cyFileInfo(text2);
					icyFileSystemInfo2 = icyFileSystemInfo;
				}
				IcyFileSystemInfo icyFileSystemInfo3 = icyFileSystemInfo2;
				try
				{
					Directory.CreateDirectory(directoryName);
					if (icyFileSystemInfo3.Exists && icyFileSystemInfo3.IsReadOnly)
					{
						icyFileSystemInfo3.Attributes--;
						icyFileSystemInfo3.SetAtributes();
					}
					if (!nextEntry2.IsFile)
					{
						continue;
					}
					using Stream stream2 = new FileStream(text2, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
					if (nextEntry2.Size >= 0)
					{
						stream2.SetLength(nextEntry2.Size);
					}
					if (nextEntry2.Size >= 0)
					{
						StreamUtils.Copy(zipInputStream, stream2, new byte[524288]);
					}
				}
				catch (Exception ex)
				{
					string informationMessage = "Could not process personal user file " + PERSONAL_FILE_NAME + " zip entry " + text2 + ".";
					Log.AddError(informationMessage, ex, LogCategories.FileSystem);
				}
			}
		}
		catch
		{
			throw;
		}
	}

	public async Task<AppExe> AppExeExecutionGraphGetAsync(int entityId)
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.AppExeExecutionGraphGet, entityId);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<AppExe>();
	}

	public AppExe AppExeExecutionGraphGet(int entityId)
	{
		return AppExeExecutionGraphGetAsync(entityId).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
	}

	public async Task<IEnumerable<AppExePersonalFile>> AppExePersonalFileGetAsync(int entityId)
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.AppExePersonalFileGet, entityId);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<IEnumerable<AppExePersonalFile>>();
	}

	public async Task<IEnumerable<AppLink>> AppLinkGetAsync(int entityId)
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.AppLinkGet, entityId);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<IEnumerable<AppLink>>();
	}

	public async Task<Dictionary<int, HashSet<string>>> GetPersonalUserFilePathListAsync()
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.AppExePersonalFilePathsGet);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<Dictionary<int, HashSet<string>>>().ToDictionary((KeyValuePair<int, HashSet<string>> key) => key.Key, (KeyValuePair<int, HashSet<string>> value) => new HashSet<string>(from path in value.Value.Where(delegate(string path)
			{
				if (string.IsNullOrWhiteSpace(path))
				{
					return false;
				}
				string text = Environment.ExpandEnvironmentVariables(path);
				if (text.IndexOfAny(INVALID_CHAR_LIST) >= 0)
				{
					return false;
				}
				try
				{
					text = Path.GetFullPath(text);
				}
				catch
				{
					return false;
				}
				return true;
			})
			select Path.GetFullPath(Environment.ExpandEnvironmentVariables(path))));
	}

	public Dictionary<int, HashSet<string>> GetPersonalUserFilePathList()
	{
		return GetPersonalUserFilePathListAsync().ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
	}

	public async Task<Dictionary<int, HashSet<string>>> GetDeploymentPathListAsync(string rootPath)
	{
		if (string.IsNullOrWhiteSpace(rootPath))
		{
			throw new ArgumentNullException("rootPath");
		}
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.AppExeDeploymentPathsGet);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<Dictionary<int, HashSet<string>>>().ToDictionary((KeyValuePair<int, HashSet<string>> key) => key.Key, (KeyValuePair<int, HashSet<string>> value) => new HashSet<string>(from path in value.Value.Where(delegate(string path)
			{
				if (string.IsNullOrWhiteSpace(path))
				{
					return false;
				}
				string path2 = Environment.ExpandEnvironmentVariables(path);
				try
				{
					return Path.GetFullPath(path2).StartsWith(rootPath, StringComparison.InvariantCultureIgnoreCase) || path == "*.*";
				}
				catch (NotSupportedException)
				{
					return false;
				}
				catch
				{
					return false;
				}
			})
			select Path.GetFullPath(Environment.ExpandEnvironmentVariables(path))));
	}

	public Dictionary<int, HashSet<string>> GetDeploymentPathList(string rootPath)
	{
		return GetDeploymentPathListAsync(rootPath).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
	}

	public async Task<Deployment> DeploymentGetAsync(int entityId)
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.DeploymentGet, entityId);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<Deployment>();
	}

	public Deployment DeploymentGet(int entityId)
	{
		return DeploymentGetAsync(entityId).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
	}

	public async Task<PersonalFile> PersonalFileGetAsync(int entityId)
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.PersonalFileGet, entityId);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<PersonalFile>();
	}

	public PersonalFile PersonalFileGet(int entityId)
	{
		return PersonalFileGetAsync(entityId).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
	}

	public async Task<Dictionary<int, HashSet<int>>> AppExePersonalFileByActivationGetAsync(SharedLib.PersonalFileActivationType activation)
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.AppExePersonalFileByActivationGet, activation);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<Dictionary<int, HashSet<int>>>();
	}

	public Dictionary<int, HashSet<int>> AppExePersonalFileByActivationGet(SharedLib.PersonalFileActivationType activation)
	{
		return AppExePersonalFileByActivationGetAsync(activation).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
	}

	public async Task<string> ExpandRemoteVariableAsync(string name)
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.Environment, EnvironmentOprations.Expand, name);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.Data[0] as string;
	}

	public string ExpandRemoteVariable(string name)
	{
		return ExpandRemoteVariableAsync(name).GetAwaiter().GetResult();
	}

	private void InitShutdownTimer()
	{
		lock (SHUT_DOWN_TIMER_LOCK)
		{
			int turnOffTimeOut = Settings.TurnOffTimeOut;
			bool num = turnOffTimeOut > 0;
			if (!num)
			{
				DeinitShutdownTimer();
			}
			if (num && turnOffTimeOut > 0)
			{
				TimeSpan timeSpan = TimeSpan.FromMinutes(Settings.TurnOffTimeOut);
				if (SHUT_DOWN_TIMER == null)
				{
					SHUT_DOWN_TIMER = new System.Threading.Timer(OnShutdownTimerCallBack, null, timeSpan, timeSpan);
				}
				SHUT_DOWN_TIMER.Change(timeSpan, timeSpan);
			}
		}
	}

	private void DeinitShutdownTimer()
	{
		lock (SHUT_DOWN_TIMER_LOCK)
		{
			SHUT_DOWN_TIMER?.Dispose();
			SHUT_DOWN_TIMER = null;
		}
	}

	public MessageBoxResult NotifyUser(string message, string title, bool dialog)
	{
		return NotifyUser(message, new WindowShowParams
		{
			Title = title,
			Owner = ShellWindowHandle,
			ShowDialog = dialog,
			Icon = MessageBoxImage.Asterisk,
			Buttons = MessageBoxButton.OK
		});
	}

	public MessageBoxResult NotifyUser(string message, string title, MessageBoxButton buttons, MessageBoxImage icon, bool dialog)
	{
		return NotifyUser(message, new WindowShowParams
		{
			Title = title,
			ShowDialog = dialog,
			Buttons = buttons,
			Icon = icon,
			Owner = ShellWindowHandle
		});
	}

	public MessageBoxResult NotifyUser(string message, WindowShowParams parameters)
	{
		INotifyWindowViewModel model;
		return NotifyUserInternal(message, parameters, out model);
	}

	public MessageBoxResult NotifyUser(string message, WindowShowParams parameters, out INotifyWindowViewModel model)
	{
		return NotifyUserInternal(message, parameters, out model);
	}

	public MessageBoxResult NotifyUserInternal(string message, WindowShowParams parameters, out INotifyWindowViewModel model)
	{
		if (!Application.Dispatcher.CheckAccess())
		{
			model = null;
			object[] array = new object[3] { message, parameters, model };
			MessageBoxResult result = (MessageBoxResult)Application.Dispatcher.Invoke(new NotifyUserDelegate(NotifyUserInternal), array);
			model = (INotifyWindowViewModel)array[2];
			return result;
		}
		MessageBoxModel messageBoxModel = (MessageBoxModel)(model = new MessageBoxModel(this, message, parameters.Title, parameters.Icon, parameters.Buttons));
		messageBoxModel.AllowDrag = parameters.AllowDrag;
		messageBoxModel.Window.Left = parameters.Left;
		messageBoxModel.Window.Top = parameters.Top;
		messageBoxModel.Window.Height = parameters.Height;
		messageBoxModel.Window.Width = parameters.Width;
		messageBoxModel.HideButtons = parameters.NoButtons;
		messageBoxModel.Window.SizeToContent = parameters.SizeToContent;
		messageBoxModel.Window.WindowStartupLocation = parameters.StartupLocation;
		messageBoxModel.AllowClosing = parameters.AllowClosing;
		messageBoxModel.Window.Topmost = parameters.TopMost;
		messageBoxModel.Window.MaxHeight = parameters.MaxHeight;
		messageBoxModel.Window.MaxWidth = parameters.MaxWidth;
		messageBoxModel.Window.ShowActivated = parameters.ShowActivated;
		messageBoxModel.DefaultButton = parameters.DefaultButton;
		IntPtr intPtr = parameters.Owner;
		if (intPtr == IntPtr.Zero)
		{
			intPtr = ShellWindowHandle;
		}
		if (parameters.ShowDialog)
		{
			messageBoxModel.ShowDialog(intPtr);
		}
		else
		{
			messageBoxModel.Show(intPtr);
		}
		return messageBoxModel.Result;
	}

	public INotifyWindowViewModel CreateNotificationModel(string message, WindowShowParams parameters)
	{
		MessageBoxModel notifyModel = null;
		Action action = delegate
		{
			notifyModel = new MessageBoxModel(this, message, parameters.Title, parameters.Icon, parameters.Buttons)
			{
				AllowDrag = parameters.AllowDrag
			};
			notifyModel.Window.Left = parameters.Left;
			notifyModel.Window.Top = parameters.Top;
			notifyModel.Window.Height = parameters.Height;
			notifyModel.Window.Width = parameters.Width;
			notifyModel.HideButtons = parameters.NoButtons;
			notifyModel.Window.SizeToContent = parameters.SizeToContent;
			notifyModel.Window.WindowStartupLocation = parameters.StartupLocation;
			notifyModel.AllowClosing = parameters.AllowClosing;
			notifyModel.Window.Topmost = parameters.TopMost;
			notifyModel.Window.MaxHeight = parameters.MaxHeight;
			notifyModel.Window.MaxWidth = parameters.MaxWidth;
			notifyModel.Window.ShowActivated = parameters.ShowActivated;
			notifyModel.DefaultButton = parameters.DefaultButton;
		};
		if (Application.Dispatcher.CheckAccess())
		{
			action();
		}
		else
		{
			Application.Dispatcher.Invoke(action);
		}
		return notifyModel;
	}

	private async System.Threading.Tasks.Task NotifyUserTime(int minutes, TimeLeftWarningType type = TimeLeftWarningType.All)
	{
		if (type == TimeLeftWarningType.None)
		{
			return;
		}
		string arg = minutes.ToString();
		string MESSAGE_WARNING = GetLocalizedString("MESSAGE_WARNING") ?? "WARNING";
		string NOTIFICATION_MESSAGE = GetSettingsValue<string>("TimeNotificationMessage");
		if (string.IsNullOrWhiteSpace(NOTIFICATION_MESSAGE))
		{
			NOTIFICATION_MESSAGE = GetLocalizedString("MESSAGE_TIME_RUNS_OUT");
		}
		if (string.IsNullOrWhiteSpace(NOTIFICATION_MESSAGE))
		{
			return;
		}
		if (NOTIFICATION_MESSAGE.IndexOf("{0}") != -1)
		{
			NOTIFICATION_MESSAGE = string.Format(NOTIFICATION_MESSAGE, arg);
		}
		System.Threading.Tasks.Task audibleTask = null;
		System.Threading.Tasks.Task visualTask = null;
		if (type.HasFlag(TimeLeftWarningType.MinimizeWindows))
		{
			try
			{
				await System.Threading.Tasks.Task.Run(delegate
				{
					OnMinimizeNonClientWindows();
				});
			}
			catch (Exception ex)
			{
				TraceWrite(ex, "NotifyUserTime", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Functionality.cs", 279);
			}
		}
		if (type.HasFlag(TimeLeftWarningType.Audible))
		{
			audibleTask = System.Threading.Tasks.Task.Run(async delegate
			{
				List<AudioSessionInfo> audioSessions = (from x in GetAudioSessions()
					where x.ProcessId != 0 && x.State == AudioSessionState.AudioSessionStateActive && !x.IsMuted == true && x.Volume > 0f
					select x).ToList();
				foreach (AudioSessionInfo item in audioSessions)
				{
					await VolumeFade(item.ProcessId, 0f);
				}
				if (OSVersion.Major >= 10)
				{
					try
					{
						using Windows.Media.SpeechSynthesis.SpeechSynthesizer speechSynthesizer = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
						TaskAwaiter<SpeechSynthesisStream> taskAwaiter = WindowsRuntimeSystemExtensions.GetAwaiter<SpeechSynthesisStream>(speechSynthesizer.SynthesizeTextToStreamAsync(NOTIFICATION_MESSAGE));
						if (!taskAwaiter.IsCompleted)
						{
							await taskAwaiter;
							TaskAwaiter<SpeechSynthesisStream> taskAwaiter2 = default(TaskAwaiter<SpeechSynthesisStream>);
							taskAwaiter = taskAwaiter2;
						}
						SpeechSynthesisStream result = taskAwaiter.GetResult();
						using SpeechSynthesisStream stream = result;
						byte[] bytes = new byte[stream.Size];
						IBuffer buffer = WindowsRuntimeBufferExtensions.AsBuffer(bytes);
						TaskAwaiter<IBuffer> taskAwaiter3 = WindowsRuntimeSystemExtensions.GetAwaiter<IBuffer, uint>(stream.ReadAsync(buffer, (uint)stream.Size, InputStreamOptions.None));
						if (!taskAwaiter3.IsCompleted)
						{
							await taskAwaiter3;
							TaskAwaiter<IBuffer> taskAwaiter4 = default(TaskAwaiter<IBuffer>);
							taskAwaiter3 = taskAwaiter4;
						}
						taskAwaiter3.GetResult();
						using MemoryStream stream2 = new MemoryStream(bytes);
						using SoundPlayer soundPlayer = new SoundPlayer(stream2);
						VolumeSetLevel(Kernel32.GetCurrentProcessId(), 100f);
						soundPlayer.Play();
					}
					catch (Exception ex2)
					{
						TraceWrite(ex2, "NotifyUserTime", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Functionality.cs", 328);
					}
				}
				else
				{
					try
					{
						using System.Speech.Synthesis.SpeechSynthesizer speechSynthesizer2 = new System.Speech.Synthesis.SpeechSynthesizer();
						ReadOnlyCollection<InstalledVoice> installedVoices = speechSynthesizer2.GetInstalledVoices();
						VoiceInfo voiceInfo = (from v in installedVoices
							where v.VoiceInfo.Gender == System.Speech.Synthesis.VoiceGender.Female
							where v.VoiceInfo.Culture.Equals(Thread.CurrentThread.CurrentCulture)
							select v.VoiceInfo).FirstOrDefault();
						if (voiceInfo == null)
						{
							voiceInfo = (from v in installedVoices
								where v.VoiceInfo.Culture.Equals(Thread.CurrentThread.CurrentCulture)
								select v.VoiceInfo).FirstOrDefault();
						}
						if (voiceInfo == null)
						{
							voiceInfo = (from v in installedVoices
								where v.VoiceInfo.Culture.Equals(CultureInfo.GetCultureInfo("en-us"))
								where v.VoiceInfo.Gender == System.Speech.Synthesis.VoiceGender.Female
								select v.VoiceInfo).FirstOrDefault();
						}
						if (voiceInfo != null)
						{
							speechSynthesizer2.SelectVoice(voiceInfo.Name);
						}
						Prompt prompt = new Prompt(string.Empty);
						speechSynthesizer2.Speak(prompt);
						VolumeSetLevel(Kernel32.GetCurrentProcessId(), 100f);
						prompt = new Prompt(NOTIFICATION_MESSAGE);
						speechSynthesizer2.Rate = 1;
						speechSynthesizer2.Volume = 100;
						speechSynthesizer2.Speak(prompt);
					}
					catch (Exception ex3)
					{
						TraceWrite(ex3, "NotifyUserTime", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Functionality.cs", 387);
					}
				}
				foreach (AudioSessionInfo item2 in audioSessions)
				{
					await VolumeFade(item2.ProcessId, item2.Volume.GetValueOrDefault());
				}
			});
		}
		if (type.HasFlag(TimeLeftWarningType.Visual))
		{
			if (OSVersion.Major >= 10)
			{
				visualTask = ShowToastWithResourcetAsync(ToastTemplateTypeProxy.ToastImageAndText02, new string[2] { MESSAGE_WARNING, NOTIFICATION_MESSAGE }, "Client.Resources.Icons.stopwatch.png", null, ToastNotificationPriorityProxy.High, CancellationToken.None);
			}
			else
			{
				WindowShowParams winp = new WindowShowParams
				{
					ShowDialog = false,
					ShowActivated = true,
					StartupLocation = WindowStartupLocation.CenterOwner,
					SizeToContent = SizeToContent.WidthAndHeight,
					Icon = MessageBoxImage.Exclamation,
					AllowDrag = true,
					Owner = ShellWindowHandle
				};
				visualTask = System.Threading.Tasks.Task.Run(() => NotifyUser(NOTIFICATION_MESSAGE, winp, out var _));
			}
		}
		if (audibleTask != null)
		{
			await audibleTask;
		}
		if (visualTask != null)
		{
			await visualTask;
		}
	}

	internal Task<ToastResult> ShowToastWithResourcetAsync(ToastTemplateTypeProxy templateType, string[] textLines, string imageResoucrcePath, ToastAction[] actions, ToastNotificationPriorityProxy priority, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(imageResoucrcePath))
		{
			throw new ArgumentNullException("imageResoucrcePath");
		}
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(imageResoucrcePath);
		if (stream == null)
		{
			throw new ArgumentException("Invalid resource name", "imageResoucrcePath");
		}
		string tempFileName = Path.GetTempFileName();
		using (FileStream destination = new FileStream(tempFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite))
		{
			stream.CopyTo(destination);
		}
		return ShowToastAsync(templateType, textLines, tempFileName, actions, priority, ct);
	}

	public async Task<ToastResult> ShowToastAsync(ToastTemplateTypeProxy templateType, string[] textLines, string imageFilePath, ToastAction[] actions, ToastNotificationPriorityProxy priority, CancellationToken ct)
	{
		string ARGUMENTS = null;
		ToastDismissalReasonProxy? DISMISS_REASON = null;
		TOAST_CTS = TOAST_CTS ?? new CancellationTokenSource();
		CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(TOAST_CTS.Token, ct);
		ToastNotification TOAST = CreateToast(templateType, textLines, imageFilePath, actions, priority);
		SemaphoreSlim WAIT_HANDLE = new SemaphoreSlim(0, 1);
		ToastNotification toastNotification = TOAST;
		WindowsRuntimeMarshal.AddEventHandler((Func<TypedEventHandler<ToastNotification, ToastFailedEventArgs>, EventRegistrationToken>)toastNotification.add_Failed, (Action<EventRegistrationToken>)toastNotification.remove_Failed, (TypedEventHandler<ToastNotification, ToastFailedEventArgs>)delegate
		{
			WAIT_HANDLE.Release();
		});
		toastNotification = TOAST;
		WindowsRuntimeMarshal.AddEventHandler((Func<TypedEventHandler<ToastNotification, object>, EventRegistrationToken>)toastNotification.add_Activated, (Action<EventRegistrationToken>)toastNotification.remove_Activated, (TypedEventHandler<ToastNotification, object>)delegate(ToastNotification sender, object args)
		{
			if (args is ToastActivatedEventArgs toastActivatedEventArgs)
			{
				ARGUMENTS = toastActivatedEventArgs.Arguments;
				WAIT_HANDLE.Release();
			}
		});
		toastNotification = TOAST;
		WindowsRuntimeMarshal.AddEventHandler((Func<TypedEventHandler<ToastNotification, ToastDismissedEventArgs>, EventRegistrationToken>)toastNotification.add_Dismissed, (Action<EventRegistrationToken>)toastNotification.remove_Dismissed, (TypedEventHandler<ToastNotification, ToastDismissedEventArgs>)delegate(ToastNotification sender, ToastDismissedEventArgs args)
		{
			DISMISS_REASON = (ToastDismissalReasonProxy)args.Reason;
			WAIT_HANDLE.Release();
		});
		ToastNotifier NOTIFIER = ToastNotificationManager.CreateToastNotifier("GIZMO_CLIENT_APP");
		cancellationTokenSource.Token.Register(async delegate
		{
			try
			{
				await (Application?.Dispatcher.InvokeAsync(delegate
				{
					NOTIFIER.Hide(TOAST);
				}));
			}
			catch (Exception)
			{
			}
		});
		cancellationTokenSource.Token.ThrowIfCancellationRequested();
		NOTIFIER.Show(TOAST);
		await WAIT_HANDLE.WaitAsync(cancellationTokenSource.Token);
		return new ToastResult(ARGUMENTS, DISMISS_REASON);
	}

	public ToastNotification CreateToast(ToastTemplateTypeProxy templateType, string[] textLines, string imageFilePath = null, ToastAction[] actions = null, ToastNotificationPriorityProxy priority = ToastNotificationPriorityProxy.Default)
	{
		if (textLines == null)
		{
			throw new ArgumentNullException("textLines");
		}
		int num = textLines.Length;
		if (num <= 0 || num > 3)
		{
			throw new ArgumentException("LINES_COUNT");
		}
		bool flag = !string.IsNullOrWhiteSpace(imageFilePath);
		XmlDocument templateContent = ToastNotificationManager.GetTemplateContent((ToastTemplateType)templateType);
		XmlNodeList elementsByTagName = templateContent.GetElementsByTagName("text");
		for (int i = 0; i < num; i++)
		{
			((IReadOnlyList<IXmlNode>)elementsByTagName)[i].AppendChild(templateContent.CreateTextNode(textLines[i]));
		}
		if (flag)
		{
			string value = "file:///" + Path.GetFullPath(imageFilePath);
			XmlNodeList elementsByTagName2 = templateContent.GetElementsByTagName("image");
			if (((IReadOnlyCollection<IXmlNode>)elementsByTagName2).Count > 0)
			{
				((IReadOnlyList<IXmlNode>)elementsByTagName2)[0].Attributes.GetNamedItem("src").NodeValue = value;
			}
		}
		if (actions != null)
		{
			XmlElement xmlElement = templateContent.CreateElement("actions");
			templateContent.SelectSingleNode("toast").AppendChild(xmlElement);
			foreach (ToastAction toastAction in actions)
			{
				XmlElement xmlElement2 = templateContent.CreateElement("action");
				xmlElement2.SetAttribute("arguments", toastAction.Arguments);
				xmlElement2.SetAttribute("content", toastAction.Content);
				if (toastAction.ImageUri != null)
				{
					xmlElement2.SetAttribute("imageUri", toastAction.ImageUri);
				}
				xmlElement.AppendChild(xmlElement2);
			}
		}
		return new ToastNotification(templateContent)
		{
			Priority = (ToastNotificationPriority)priority,
			ExpirationTime = null
		};
	}

	private IEnumerable<AudioSessionInfo> GetAudioSessions()
	{
		IMMDeviceEnumerator ENUMERATOR = new _MMDeviceEnumerator() as IMMDeviceEnumerator;
		if (ENUMERATOR.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var SPEAKERS) == -2147023728)
		{
			Marshal.ReleaseComObject(ENUMERATOR);
			yield break;
		}
		Guid iid = typeof(IAudioSessionManager2).GUID;
		SPEAKERS.Activate(ref iid, (CLSCTX)0u, IntPtr.Zero, out var ppInterface);
		IAudioSessionManager2 SESSION_MANAGER = (IAudioSessionManager2)ppInterface;
		SESSION_MANAGER.GetSessionEnumerator(out var SESSION_ENUMERATOR);
		SESSION_ENUMERATOR.GetCount(out var count);
		for (int i = 0; i < count; i++)
		{
			SESSION_ENUMERATOR.GetSession(i, out var ctl);
			AudioSessionState state = AudioSessionState.AudioSessionStateActive;
			ctl.GetProcessId(out var retvVal);
			ctl.GetState(out state);
			ISimpleAudioVolume volumeControl = GetISimpleAudioVolume(retvVal);
			float pfLevel = 0f;
			bool bMute = false;
			volumeControl?.GetMasterVolume(out pfLevel);
			volumeControl?.GetMute(out bMute);
			yield return new AudioSessionInfo
			{
				ProcessId = retvVal,
				State = state,
				Volume = ((volumeControl != null) ? new float?(pfLevel * 100f) : null),
				IsMuted = ((volumeControl != null) ? new bool?(bMute) : null)
			};
			Marshal.ReleaseComObject(volumeControl);
			Marshal.ReleaseComObject(ctl);
			ctl = null;
		}
		Marshal.ReleaseComObject(SESSION_ENUMERATOR);
		Marshal.ReleaseComObject(SESSION_MANAGER);
		Marshal.ReleaseComObject(SPEAKERS);
		Marshal.ReleaseComObject(ENUMERATOR);
	}

	private ISimpleAudioVolume GetISimpleAudioVolume(uint processId)
	{
		IMMDeviceEnumerator iMMDeviceEnumerator = new _MMDeviceEnumerator() as IMMDeviceEnumerator;
		iMMDeviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var ppEndpoint);
		Guid iid = typeof(IAudioSessionManager2).GUID;
		ppEndpoint.Activate(ref iid, (CLSCTX)0u, IntPtr.Zero, out var ppInterface);
		IAudioSessionManager2 audioSessionManager = (IAudioSessionManager2)ppInterface;
		audioSessionManager.GetSessionEnumerator(out var SessionEnum);
		SessionEnum.GetCount(out var SessionCount);
		ISimpleAudioVolume result = null;
		for (int i = 0; i < SessionCount; i++)
		{
			SessionEnum.GetSession(i, out var Session);
			Session.GetProcessId(out var retvVal);
			if (retvVal == processId)
			{
				result = Session as ISimpleAudioVolume;
				break;
			}
			Marshal.ReleaseComObject(Session);
		}
		Marshal.ReleaseComObject(SessionEnum);
		Marshal.ReleaseComObject(audioSessionManager);
		Marshal.ReleaseComObject(ppEndpoint);
		Marshal.ReleaseComObject(iMMDeviceEnumerator);
		return result;
	}

	private void VolumeSetLevel(uint processId, float level)
	{
		ISimpleAudioVolume iSimpleAudioVolume = GetISimpleAudioVolume(processId);
		if (iSimpleAudioVolume != null)
		{
			Guid EventContext = Guid.Empty;
			iSimpleAudioVolume.SetMasterVolume(level / 100f, ref EventContext);
			Marshal.ReleaseComObject(iSimpleAudioVolume);
		}
	}

	private float? VolumeGetLevel(uint processId)
	{
		ISimpleAudioVolume iSimpleAudioVolume = GetISimpleAudioVolume(processId);
		if (iSimpleAudioVolume == null)
		{
			return null;
		}
		iSimpleAudioVolume.GetMasterVolume(out var pfLevel);
		Marshal.ReleaseComObject(iSimpleAudioVolume);
		return pfLevel * 100f;
	}

	private async System.Threading.Tasks.Task VolumeFade(uint processId, float level, float step = 10f)
	{
		float? currentLevel = VolumeGetLevel(processId);
		if (!currentLevel.HasValue)
		{
			return;
		}
		int waitDelay = 20;
		if (currentLevel > level)
		{
			while (currentLevel >= 0f)
			{
				currentLevel -= step;
				VolumeSetLevel(processId, currentLevel.GetValueOrDefault());
				await System.Threading.Tasks.Task.Delay(waitDelay);
			}
		}
		else
		{
			while (currentLevel <= level)
			{
				currentLevel += step;
				VolumeSetLevel(processId, currentLevel.GetValueOrDefault());
				await System.Threading.Tasks.Task.Delay(waitDelay);
			}
		}
	}

	internal async System.Threading.Tasks.Task NotifyOrderStatusAsync(SharedLib.OrderStatus status)
	{
		if (OSVersion.Major < 10)
		{
			return;
		}
		string localizedString;
		string localizedString2;
		string imageResoucrcePath;
		switch (status)
		{
		default:
			return;
		case SharedLib.OrderStatus.Accepted:
			localizedString = GetLocalizedString("MESSAGE_ORDER_ACCEPTED_HEADER");
			localizedString2 = GetLocalizedString("MESSAGE_ORDER_ACCEPTED");
			imageResoucrcePath = "Client.Resources.Icons.order_accepted.png";
			break;
		case SharedLib.OrderStatus.Canceled:
			localizedString = GetLocalizedString("MESSAGE_ORDER_CANCELED_HEADER");
			localizedString2 = GetLocalizedString("MESSAGE_ORDER_CANCELED");
			imageResoucrcePath = "Client.Resources.Icons.canceled.png";
			break;
		case SharedLib.OrderStatus.OnHold:
			localizedString = GetLocalizedString("MESSAGE_ORDER_ON_HOLD_HEADER");
			localizedString2 = GetLocalizedString("MESSAGE_ORDER_ON_HOLD");
			imageResoucrcePath = "Client.Resources.Icons.order_accepted.png";
			break;
		case SharedLib.OrderStatus.Completed:
			localizedString = GetLocalizedString("MESSAGE_ORDER_COMPLETED_HEADER");
			localizedString2 = GetLocalizedString("MESSAGE_ORDER_COMPLETED");
			imageResoucrcePath = "Client.Resources.Icons.done.png";
			break;
		}
		ToastTemplateTypeProxy templateType = ToastTemplateTypeProxy.ToastImageAndText02;
		ToastNotificationPriorityProxy priority = ToastNotificationPriorityProxy.Default;
		CancellationToken none = CancellationToken.None;
		try
		{
			await ShowToastWithResourcetAsync(templateType, new string[2] { localizedString, localizedString2 }, imageResoucrcePath, null, priority, none);
		}
		catch (OperationCanceledException)
		{
		}
	}

	internal async System.Threading.Tasks.Task NotifyOrderFailedAsync(OrderFailReason reason)
	{
		if (OSVersion.Major < 10 || reason == OrderFailReason.None)
		{
			return;
		}
		string localizedString = GetLocalizedString("MESSAGE_ORDER_FAILED");
		string imageResoucrcePath = "Client.Resources.Icons.canceled.png";
		string text = reason switch
		{
			OrderFailReason.InsufficientBalance => GetLocalizedString("MESSAGE_ORDER_FAIL_REASON_INSUFFICIENT_FUNDS"), 
			OrderFailReason.InvalidPaymentMethod => GetLocalizedString("MESSAGE_ORDER_FAIL_REASON_INVALID_PAYMENT_METHOD"), 
			_ => string.Empty, 
		};
		ToastTemplateTypeProxy templateType = ToastTemplateTypeProxy.ToastImageAndText02;
		ToastNotificationPriorityProxy priority = ToastNotificationPriorityProxy.Default;
		CancellationToken none = CancellationToken.None;
		try
		{
			await ShowToastWithResourcetAsync(templateType, new string[2] { localizedString, text }, imageResoucrcePath, null, priority, none);
		}
		catch (OperationCanceledException)
		{
		}
	}

	public bool CreateReservation(int executableId, out LicenseReservation reservation)
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.ReserveLicenseBatch, executableId);
		syncOperation.StartEx();
		syncOperation.WaitCompleteEx();
		bool result = (bool)syncOperation.Data[0];
		reservation = (LicenseReservation)syncOperation.Data[1];
		return result;
	}

	public bool ReleaseReservation(int reservationId)
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.ReleaseLicenseBatch, reservationId);
		syncOperation.StartEx();
		syncOperation.WaitCompleteEx();
		return (bool)syncOperation.Data[0];
	}

	private void OnShutdownTimerCallBack(object state)
	{
		if (!Monitor.TryEnter(SHUT_DOWN_TIMER_LOCK))
		{
			return;
		}
		try
		{
			DeinitShutdownTimer();
			if (!IsInMaintenance && !IsUserLoggedIn)
			{
				Log.AddInformation("System was unused after specified amount of time and will shut down.", LogCategories.Configuration);
				PowerStates state2 = (Settings.TurnOffIdleSleepEnable ? PowerStates.Suspend : PowerStates.Shutdown);
				SetPowerState(state2);
			}
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "OnShutdownTimerCallBack", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Functionality.cs", 951);
		}
		finally
		{
			Monitor.Exit(SHUT_DOWN_TIMER_LOCK);
		}
	}

	private void OnUserIdleTimerCallBack(object state)
	{
		if (!Monitor.TryEnter(USER_IDLE_TIMER_LOCK))
		{
			return;
		}
		try
		{
			bool flag = Shell.LastUserInput >= USER_IDLE_TIME;
			if (flag != IsUserIdle)
			{
				IsUserIdle = flag;
				this.UserIdleChange?.Invoke(this, new UserIdleEventArgs(flag));
			}
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "OnUserIdleTimerCallBack", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Functionality.cs", 975);
		}
		finally
		{
			Monitor.Exit(USER_IDLE_TIMER_LOCK);
		}
	}

	private ApplicationProfile Convert(App entity)
	{
		return new ApplicationProfile
		{
			ID = entity.Id,
			AddDate = entity.CreatedTime,
			ReleaseDate = entity.ReleaseDate.GetValueOrDefault(),
			Version = entity.Version,
			HaltOnError = entity.Options.HasFlag(AppOptionType.HaltOnError),
			AgeRating = entity.AgeRating,
			Category = entity.AppCategoryId,
			Title = entity.Title,
			Description = entity.Description,
			Developer = entity.DeveloperId.GetValueOrDefault(),
			Publisher = entity.PublisherId.GetValueOrDefault(),
			Guid = entity.Guid,
			PublisherName = entity.Publisher?.Name,
			DeveloperName = entity.Developer?.Name
		};
	}

	private Executable Convert(AppExe entity)
	{
		Executable executable = new Executable();
		executable.ID = entity.Id;
		executable.ExecutableName = entity.Caption;
		executable.ExecutablePath = entity.ExecutablePath;
		executable.Arguments = entity.Arguments;
		executable.WorkingDirectory = entity.WorkingDirectory;
		executable.RunMode = entity.RunMode;
		executable.AutoLaunch = entity.Options.HasFlag(ExecutableOptionType.AutoLaunch);
		executable.KillChildren = entity.Options.HasFlag(ExecutableOptionType.KillChildren);
		executable.MonitorChildren = entity.Options.HasFlag(ExecutableOptionType.MonitorChildren);
		executable.MultiRun = entity.Options.HasFlag(ExecutableOptionType.MultiRun);
		executable.ShellExecute = entity.Options.HasFlag(ExecutableOptionType.ShellExecute);
		executable.Modes = entity.Modes;
		executable.ReservationType = entity.ReservationType;
		executable.VisualOptions.Accessible = entity.Accessible;
		executable.VisualOptions.Caption = entity.Caption;
		executable.VisualOptions.Description = entity.Description;
		executable.VirtualImages.AddRange(entity.AppExeCdImages.Select((AppExeCdImage x) => Convert(x)));
		executable.PersonalUserFiles.AddRange(from x in entity.PersonalFiles
			orderby x.UseOrder
			select Convert(x.PersonalFile));
		executable.DeploymentProfiles.AddRange(from x in entity.Deployments
			orderby x.UseOrder
			select Convert(x.Deployment));
		executable.LicenseProfiles.AddRange(from x in entity.Licenses
			orderby x.UseOrder
			select Convert(x.License));
		executable.Tasks.AddRange(from x in entity.Tasks
			orderby x.UseOrder
			select Convert(x));
		executable.Options = entity.Options;
		return executable;
	}

	private PersonalUserFile Convert(PersonalFile entity)
	{
		return new PersonalUserFile
		{
			ID = entity.Id,
			Name = entity.Name,
			SourcePath = entity.Source,
			MaxQuota = entity.MaxQuota,
			CompressionLevel = entity.CompressionLevel,
			CleanUp = entity.Options.HasFlag(PersonalUserFileOptionType.CleanUp),
			ActivationType = ((entity.Activation != 0) ? ActivationType.Login : ActivationType.Disabled),
			DeactivationType = ActivationType.Logout,
			IsStorable = entity.Options.HasFlag(PersonalUserFileOptionType.Store),
			Guid = entity.Guid,
			Type = entity.Type,
			IncludeFiles = entity.IncludeFiles,
			IncludeDirectories = entity.IncludeDirectories,
			IncludeSubDirectories = entity.Options.HasFlag(PersonalUserFileOptionType.IncludeSubDirectories),
			ExcludeFiles = entity.ExcludeFiles,
			ExcludeDirectories = entity.ExcludeDirectories,
			VisualOptions = 
			{
				Caption = entity.Caption,
				Accessible = entity.Accessible
			}
		};
	}

	private DeploymentProfile Convert(Deployment entity)
	{
		return new DeploymentProfile
		{
			ComparisonLevel = entity.ComparisonLevel,
			DeployOptions = entity.Options,
			Destination = entity.Destination,
			ExcludeDirectories = entity.ExcludeDirectories,
			ExcludeFiles = entity.ExcludeFiles,
			IncludeDirectories = entity.IncludeDirectories,
			IncludeFiles = entity.IncludeFiles,
			Name = entity.Name,
			Guid = entity.Guid,
			Source = entity.Source,
			ID = entity.Id,
			RegistryString = entity.RegistryString
		};
	}

	private LicenseProfile Convert(GizmoDALV2.Entities.License entity)
	{
		LicenseProfile licenseProfile = new LicenseProfile
		{
			Guid = entity.Guid,
			ID = entity.Id,
			ManagerPlugin = entity.Plugin,
			Name = entity.Name,
			PluginAssembly = entity.Assembly
		};
		if (entity.Settings != null)
		{
			licenseProfile.PluginSettings = Transformation.DeSerialize<IPluginSettings>(entity.Settings);
		}
		licenseProfile.Licenses.AddRange(from x in entity.LicenseKeys
			where x.IsEnabled
			select new IntegrationLib.ApplicationLicense
			{
				ID = x.Id,
				Comment = x.Comment,
				Guid = x.Guid,
				Key = Transformation.DeSerialize<IApplicationLicenseKey>(x.Value)
			});
		return licenseProfile;
	}

	private SharedLib.Tasks.Task Convert(AppExeTask entity)
	{
		SharedLib.Tasks.Task task = new SharedLib.Tasks.Task
		{
			ID = entity.Id,
			TaskName = entity.TaskBase.Name,
			Activation = (ActivationType)entity.Activation
		};
		if (!entity.IsEnabled)
		{
			task.Activation = ActivationType.Disabled;
		}
		GizmoDALV2.Entities.TaskBase taskBase = entity.TaskBase;
		if (taskBase is TaskJunction)
		{
			task.TaskType = SharedLib.TaskType.Junction;
			TaskJunction taskJunction = taskBase as TaskJunction;
			task.StartInfo.WorkingDirectory = taskJunction.DestinationDirectory;
			task.Data = taskJunction.SourceDirectory;
			task.StartInfo.CreateNoWindow = taskJunction.Options.HasFlag(TaskJunctionOptionType.DeleteDestination);
		}
		else if (taskBase is TaskProcess)
		{
			task.TaskType = SharedLib.TaskType.Process;
			TaskProcess taskProcess = taskBase as TaskProcess;
			task.StartInfo.Arguments = taskProcess.Arguments;
			task.StartInfo.FileName = taskProcess.FileName;
			task.StartInfo.WaitForTermination = taskProcess.ProcessOptions.HasFlag(TaskProcessOptionType.Wait);
			task.StartInfo.CreateNoWindow = taskProcess.ProcessOptions.HasFlag(TaskProcessOptionType.NoWindow);
			task.StartInfo.Password = taskProcess.Password;
			task.StartInfo.Username = taskProcess.Username;
			task.StartInfo.WorkingDirectory = taskProcess.WorkingDirectory;
		}
		else if (taskBase is TaskScript)
		{
			task.TaskType = SharedLib.TaskType.Script;
			TaskScript taskScript = taskBase as TaskScript;
			task.ScriptType = taskScript.ScriptType;
			task.Data = taskScript.Data;
			task.StartInfo.WaitForTermination = taskScript.ProcessOptions.HasFlag(TaskProcessOptionType.Wait);
			task.StartInfo.CreateNoWindow = taskScript.ProcessOptions.HasFlag(TaskProcessOptionType.NoWindow);
		}
		else if (taskBase is TaskNotification)
		{
			task.TaskType = SharedLib.TaskType.Notification;
			TaskNotification taskNotification = taskBase as TaskNotification;
			task.TaskName = taskNotification.Title;
			task.Data = taskNotification.Message;
			task.StartInfo.WaitForTermination = taskNotification.NotificationOptions.HasFlag(TaskNotificationOptionType.Wait);
		}
		return task;
	}

	private CDImage Convert(AppExeCdImage entity)
	{
		CDImage cDImage = new CDImage
		{
			CDImagePath = entity.Path,
			ID = entity.Id,
			MountOptions = new MountOptions
			{
				CheckExitCode = entity.CheckExitCode,
				ID = entity.Id,
				MountOptions = entity.MountOptions
			}
		};
		if (int.TryParse(entity.DeviceId, out var result))
		{
			cDImage.MountOptions.DeviceID = result;
		}
		return cDImage;
	}

	public string GetLocalizedString(string resourceKey)
	{
		return GetLocalizedObject<string>(resourceKey);
	}

	public T GetLocalizedObject<T>(string resourceKey) where T : class
	{
		return Application.Dispatcher.Invoke(() => LocalizeDictionary.Instance.GetLocalizedObject<T>(resourceKey));
	}

	public T GetLocalizedObject<T>(Enum enumValue) where T : class
	{
		return Application.Dispatcher.Invoke(() => LocalizeDictionary.Instance.GetLocalizedObject<T>(enumValue));
	}

	public void LogAdd(string message)
	{
		Log.AddInformation(message, LogCategories.Generic);
	}

	public void LogAdd(string message, LogCategories category)
	{
		Log.AddInformation(message, category);
	}

	public void LogAddError(string messgae, Exception ex, LogCategories category)
	{
		Log.AddError(messgae, ex, category);
	}

	public void LogAddError(string messgae, LogCategories category)
	{
		Log.AddError(messgae, null, category);
	}

	public void CreateUserStorage()
	{
		if (!IsUserLoggedIn)
		{
			throw new ArgumentException("Cannot create user storage. No user logged in.", "IsUserLoggedIn");
		}
		IUserProfile currentUser = CurrentUser;
		if (currentUser != null && currentUser.IsGuest)
		{
			return;
		}
		ClientSettings clientSettings = Settings;
		if (clientSettings == null || !clientSettings.IsPersonalStorageEnabled)
		{
			return;
		}
		GroupConfiguration obj = GroupConfiguration;
		if (obj == null || !obj.PersonalStorageDisabled)
		{
			Dispatcher.ThrowIfInvalidDispatcher();
			if (!CBHelperBase<Cbfs>.IsInitialized)
			{
				throw new ArgumentException("Cannot create user storage. Virtual file system dirvers not installed/initialized.", "IsInitialized");
			}
			string sourcePath = UserHomePathGet();
			MappingsConfiguration mappingsConfiguration = new MappingsConfiguration
			{
				MountPoint = Settings.PersonalStorageDriveLetter,
				Label = GetLocalizedObject<string>("PERSONAL_STORAGE"),
				SourcePath = sourcePath,
				ReadOnly = false,
				VolumeSize = Settings.PersonalStorageSize
			};
			MappingManager.Add(mappingsConfiguration);
			PersonalDriveConfiguration = mappingsConfiguration;
			RedirectSpecialFoldersPaths(Settings.RedirectedFolders, Path.GetFullPath(mappingsConfiguration.MountPoint));
		}
	}

	public void DeleteUserStorage()
	{
		try
		{
			if (PersonalDriveConfiguration != null)
			{
				MappingManager.Remove(PersonalDriveConfiguration);
			}
			RestoreSpecailFoldersPaths(KnownFolderTypes.Basic);
		}
		catch
		{
			throw;
		}
		finally
		{
			PersonalDriveConfiguration = null;
		}
	}

	private void ProcessMappings(IEnumerable<IMappingsConfiguration> mapList)
	{
		mapList = mapList ?? Enumerable.Empty<IMappingsConfiguration>();
		IMappingsConfiguration personalDriveConfiguration = PersonalDriveConfiguration;
		IEnumerable<IMappingsConfiguration> enumerable2;
		if (personalDriveConfiguration != null)
		{
			IEnumerable<IMappingsConfiguration> enumerable = new List<IMappingsConfiguration> { personalDriveConfiguration };
			enumerable2 = enumerable;
		}
		else
		{
			enumerable2 = Enumerable.Empty<IMappingsConfiguration>();
		}
		IEnumerable<IMappingsConfiguration> ignoreList = enumerable2;
		try
		{
			MappingManager.Clear(ignoreList);
		}
		catch (Exception ex)
		{
			LogAddError("Failed clearing mappings.", GetSerializableExcetpion(ex), LogCategories.FileSystem);
		}
		foreach (IMappingsConfiguration map in mapList)
		{
			try
			{
				MappingManager.Add(map);
			}
			catch (Exception ex2)
			{
				LogAddError("Error occoured durring drive mapping processing", GetSerializableExcetpion(ex2), LogCategories.FileSystem);
			}
		}
	}

	private Exception GetSerializableExcetpion(Exception ex)
	{
		if (ex == null)
		{
			throw new ArgumentNullException("ex");
		}
		try
		{
			Transformation.Serialize(ex);
			return ex;
		}
		catch
		{
		}
		return new Exception(ex.Message);
	}

	private void RestoreSpecailFoldersPaths(KnownFolderTypes byEnum)
	{
		foreach (Enum individualFlag in byEnum.GetIndividualFlags())
		{
			try
			{
				Guid guidValue = individualFlag.GetGuidValue();
				if (Environment.OSVersion.Version.Major >= 6)
				{
					cyPath.RedirectSpecialFolder(guidValue, null, restore: true);
					continue;
				}
				Environment.SpecialFolder specialFolderValue = individualFlag.GetSpecialFolderValue();
				if ((Environment.SpecialFolder)65535 != specialFolderValue)
				{
					cyPath.RedirectSpecialFolder(specialFolderValue, null, restore: true);
				}
			}
			catch (Exception ex)
			{
				LogAddError($"Default Folder redirection failed {individualFlag}.", ex, LogCategories.Operation);
			}
			finally
			{
				Shell32.SHFlushSFCache();
			}
		}
	}

	private void RedirectSpecialFoldersPaths(KnownFolderTypes byEnum, string basePath)
	{
		foreach (Enum individualFlag in byEnum.GetIndividualFlags())
		{
			try
			{
				string empty = string.Empty;
				string empty2 = string.Empty;
				string empty3 = string.Empty;
				Guid guidValue = individualFlag.GetGuidValue();
				Environment.SpecialFolder specialFolderValue = individualFlag.GetSpecialFolderValue();
				if (Environment.OSVersion.Version.Major >= 6)
				{
					empty = cyPath.GetSpecialFolderPath(guidValue);
					goto IL_0077;
				}
				if ((Environment.SpecialFolder)65535 != specialFolderValue)
				{
					empty = cyPath.GetSpecialFolderPath(specialFolderValue);
					goto IL_0077;
				}
				goto end_IL_001d;
				IL_0077:
				empty2 = Path.GetFileName(empty);
				empty3 = Path.Combine(basePath, empty2);
				if (Environment.OSVersion.Version.Major >= 6)
				{
					cyPath.SetSpecialFolderPath(guidValue, empty3);
					if (!Directory.Exists(empty3))
					{
						cyPath.GetSpecialFolderPath(guidValue, (KF_FLAG)34816u);
					}
				}
				else
				{
					cyPath.SetSpecialFolderPath(specialFolderValue, empty3);
					if (!Directory.Exists(empty3))
					{
						Directory.CreateDirectory(empty3);
					}
				}
				end_IL_001d:;
			}
			catch (Exception ex)
			{
				LogAddError("Folder redirection failed.", ex, LogCategories.Operation);
			}
			finally
			{
				Shell32.SHFlushSFCache();
			}
		}
	}

	private bool TryCreateShortcut()
	{
		if (!File.Exists(EntryPoint.WINDOWS_APP_SHORTCUT))
		{
			IShellLinkW shellLinkW = (IShellLinkW)new ShellLink();
			ErrorHelper.VerifySucceeded(shellLinkW.SetPath(EntryPoint.PROCESS_FULL_FILE_NAME));
			ErrorHelper.VerifySucceeded(shellLinkW.SetArguments(""));
			IPropertyStore propertyStore = (IPropertyStore)shellLinkW;
			using (PropVariant pv = new PropVariant("GIZMO_CLIENT_APP"))
			{
				PropertyKey key = new PropertyKey(new Guid("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}"), 5);
				ErrorHelper.VerifySucceeded(propertyStore.SetValue(ref key, pv));
				ErrorHelper.VerifySucceeded(propertyStore.Commit());
			}
			ErrorHelper.VerifySucceeded(((Win32API.Com.IPersistFile)shellLinkW).Save(EntryPoint.WINDOWS_APP_SHORTCUT, fRemember: true));
			return true;
		}
		return false;
	}

	private void Connect(bool saveSettings = false, bool randomDelay = false)
	{
		lock (NET_OP_LOCK)
		{
			ConnectionSettings connection = Settings.Connection;
			if (connection == null && !connection.IsValid && TraceEnabled)
			{
				TraceWrite("Connection settings are null or invalid.", (string)null, "Connect", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 102);
			}
			if (!IsConnecting && AllowConnecting)
			{
				try
				{
					if (TraceEnabled)
					{
						TraceWrite("Starting connection.", (string)null, "Connect", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 111);
					}
					IsConnecting = true;
					if (randomDelay)
					{
						Thread.Sleep(new Random().Next(1000, 10000));
					}
					try
					{
						if (TraceEnabled)
						{
							TraceWrite("Closing existing connection.", (string)null, "Connect", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 127);
						}
						if (StreamConnection.IsConnected)
						{
							StreamConnection.ShutDown();
							StreamConnection.Close();
						}
					}
					catch (Exception ex)
					{
						TraceWrite(ex, "Connect", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 137);
					}
					Dispatcher.DetachCurrent();
					SocketConnection socketConnection = new SocketConnection
					{
						NoDelay = true,
						IsChunkingEnabled = true
					};
					socketConnection.KeepAliveInterval = 1000u;
					socketConnection.KeepAliveTimeOut = 3000u;
					socketConnection.KeepAlive = true;
					IPEndPoint endpoint = connection.Endpoint;
					if (TraceEnabled)
					{
						TraceWrite($"Initiating connection to {endpoint}.", (string)null, "Connect", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 164);
					}
					socketConnection.Connect(endpoint);
					StreamConnection = socketConnection;
					Dispatcher.AttachConnection(socketConnection);
					if (saveSettings)
					{
						ClientSettings.TrySetRegistryConnectionSettings(endpoint.Address.ToString(), endpoint.Port);
					}
					if (TraceEnabled)
					{
						TraceWrite($"Connection initiated to {endpoint}.", (string)null, "Connect", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 182);
					}
					if (TraceEnabled)
					{
						TraceWrite($"Starting reception from {endpoint}.", (string)null, "Connect", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 185);
					}
					StreamConnection.Receive();
					StopUserDisconnectTimer();
					if (TraceEnabled)
					{
						TraceWrite($"Started reception from {endpoint}.", (string)null, "Connect", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 192);
					}
					return;
				}
				catch (Exception ex2)
				{
					TraceWrite(ex2, "Connect", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 198);
					return;
				}
				finally
				{
					IsConnecting = false;
				}
			}
			if (TraceEnabled)
			{
				TraceWrite("Connection not possible due to conditions.", (string)null, "Connect", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 208);
			}
		}
	}

	private void AttchHandlers()
	{
		lock (NET_OP_LOCK)
		{
			StreamConnection.EndpointDisconnected += OnStreamConnectionEndpointDisconnected;
			StreamConnection.Exception += OnStreamConnectionException;
		}
	}

	private void DetachHandlers()
	{
		lock (NET_OP_LOCK)
		{
			StreamConnection.EndpointDisconnected -= OnStreamConnectionEndpointDisconnected;
			StreamConnection.Exception -= OnStreamConnectionException;
		}
	}

	private void AttachUDPServerHandlers(IConnection connection)
	{
		lock (NET_OP_LOCK)
		{
			connection.Received += OnConnectionReceived;
			connection.Exception += OnConnectionException;
		}
	}

	private void DetachUDPServerHandlers(IConnection connection)
	{
		lock (NET_OP_LOCK)
		{
			connection.Received -= OnConnectionReceived;
			connection.Exception -= OnConnectionException;
		}
	}

	protected override void OnNetworkConnectionChanged(IConnection oldConnection, IConnection newConnection)
	{
		try
		{
			if (!(newConnection is ISocketConnection socketConnection))
			{
				return;
			}
			if (socketConnection.Socket.SocketType == SocketType.Stream)
			{
				DetachHandlers();
				AttchHandlers();
			}
			else if (socketConnection.Socket.SocketType == SocketType.Dgram)
			{
				if (oldConnection != null)
				{
					DetachUDPServerHandlers(oldConnection);
				}
				if (newConnection != null)
				{
					AttachUDPServerHandlers(newConnection);
				}
			}
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "OnNetworkConnectionChanged", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 300);
		}
	}

	protected override void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
	{
		try
		{
			if (TraceEnabled)
			{
				TraceWrite(string.Format("Network availability changed. {0}:{1}.", "IsAvailable", e.IsAvailable), (string)null, "OnNetworkAvailabilityChanged", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 309);
			}
			if (!e.IsAvailable)
			{
				if (base.IsNetworkInitialized)
				{
					Deinitialize();
				}
			}
			else if (!base.IsNetworkInitialized)
			{
				Initialize();
			}
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "OnNetworkAvailabilityChanged", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 324);
		}
	}

	protected override bool OnInitialize()
	{
		try
		{
			TraceWrite("Network initialization started.", (string)null, "OnInitialize", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 332);
			try
			{
				if (FireWall.IsProfileEnabled(2) || FireWall.IsProfileEnabled(4))
				{
					if (TraceEnabled)
					{
						TraceWrite("Disabling firewall.", (string)null, "OnInitialize", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 341);
					}
					if (!FireWall.IsCurrentAdded())
					{
						FireWall.AddCurrentToExceptions();
					}
					if (!FireWall.IsPortAllowed(44967, 17))
					{
						FireWall.AddRule(44967, 17, 1, "Gizmo Server Auto-Discovery", enable: true);
					}
				}
			}
			catch (Exception ex)
			{
				TraceWrite(ex, "OnInitialize", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 352);
			}
			if (base.IsAvailable)
			{
				SocketConnection socketConnection = new SocketConnection(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				socketConnection.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
				socketConnection.Bind(IPAddress.Any, 44967);
				socketConnection.Receive();
				DataGramConnection = socketConnection;
			}
			base.ConnectionTimer.AutoReset = true;
			base.ConnectionTimer.Interval = 5000.0;
			base.ConnectionTimer.Elapsed += OnConnectionTimerTick;
			base.ConnectionTimer.Enabled = true;
			return true;
		}
		catch (Exception ex2)
		{
			TraceWrite(ex2, "OnInitialize", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 389);
			return false;
		}
	}

	protected override bool OnDeinitialize()
	{
		try
		{
			TraceWrite("Network de-initialization started.", (string)null, "OnDeinitialize", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 400);
			base.ConnectionTimer.Enabled = false;
			base.ConnectionTimer.Elapsed -= OnConnectionTimerTick;
			DetachUDPServerHandlers(DataGramConnection);
			try
			{
				DataGramConnection.ShutDown();
				DataGramConnection.Close();
			}
			catch (Exception ex)
			{
				TraceWrite(ex, "OnDeinitialize", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 420);
			}
			try
			{
				StreamConnection.ShutDown();
				StreamConnection.Close();
			}
			catch (Exception ex2)
			{
				TraceWrite(ex2, "OnDeinitialize", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 433);
			}
			return true;
		}
		catch (Exception ex3)
		{
			TraceWrite(ex3, "OnDeinitialize", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 441);
			return false;
		}
	}

	private string OnSharedMacRequest()
	{
		string result = BitConverter.ToString(new byte[6], 0, 6);
		try
		{
			if (StreamConnection.IsConnected && StreamConnection is ISocketConnection socketConnection)
			{
				PhysicalAddress physicalByIpV = NetTool.GetPhysicalByIpV4(((IPEndPoint)socketConnection.Socket.LocalEndPoint).Address);
				if (physicalByIpV != null)
				{
					return BitConverter.ToString(physicalByIpV.GetAddressBytes(), 0, 6);
				}
			}
			NetworkInterface networkInterface = (from x in NetworkInterface.GetAllNetworkInterfaces()
				where x.Supports(NetworkInterfaceComponent.IPv4) && x.NetworkInterfaceType == NetworkInterfaceType.Ethernet
				select x).FirstOrDefault();
			if (networkInterface != null)
			{
				return BitConverter.ToString(networkInterface.GetPhysicalAddress().GetAddressBytes(), 0, 6);
			}
			return result;
		}
		catch
		{
			return result;
		}
	}

	private IPAddress OnSharedIPAddressRequest(IPVersion version)
	{
		try
		{
			if (version == IPVersion.IPV4)
			{
				if (StreamConnection.IsConnected && StreamConnection is ISocketConnection socketConnection)
				{
					return ((IPEndPoint)socketConnection.Socket.LocalEndPoint).Address;
				}
				return NetTool.GetFirstLocalIPV4Address();
			}
			return NetTool.GetFirstLocalIPV6Address();
		}
		catch
		{
			return IPAddress.None;
		}
	}

	private void OnDispatcherException(object sender, DispatcherExceptionEventArgs args)
	{
		Log.AddError(args.Exception.Message, args.Exception, LogCategories.Dispatcher);
		if (TraceEnabled)
		{
			TraceWrite(args.Exception, "OnDispatcherException", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 519);
		}
	}

	private void OnStreamConnectionException(object sender, ExceptionEventArgs args)
	{
		if (TraceEnabled)
		{
			TraceWrite(args.Exception, "OnStreamConnectionException", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 525);
		}
	}

	private void OnStreamConnectionEndpointDisconnected(object sender, ConnectDisconnectEventArgs args)
	{
		if (!(sender is IConnection connection))
		{
			return;
		}
		Dispatcher.DetachConnection(connection);
		StartUserDisconnectTimer();
		if (!TraceEnabled)
		{
			return;
		}
		try
		{
			if (connection is ISocketConnection socketConnection)
			{
				TraceWrite("Stream connection disconnected from: " + socketConnection.Socket.RemoteEndPoint.ToString(), (string)null, "OnStreamConnectionEndpointDisconnected", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 542);
			}
			else
			{
				TraceWrite("Stream connection disconnected.", (string)null, "OnStreamConnectionEndpointDisconnected", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 546);
			}
		}
		catch (ObjectDisposedException)
		{
			TraceWrite("Stream connection disconnected from: Unknown endpoint", (string)null, "OnStreamConnectionEndpointDisconnected", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 551);
		}
	}

	private void OnConnectionException(object sender, ExceptionEventArgs args)
	{
		if (TraceEnabled)
		{
			TraceWrite(args.Exception, "OnConnectionException", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 560);
		}
	}

	private void StopUserDisconnectTimer()
	{
		lock (USER_DISCONNECT_TIMER_LOCK)
		{
			USER_DISCONNECT_TIMER?.Dispose();
			USER_DISCONNECT_TIMER = null;
		}
	}

	private void StartUserDisconnectTimer()
	{
		bool flag = false;
		lock (LOGIN_OP_LOCK)
		{
			flag = CurrentUser != null;
		}
		if (flag && Settings.PendingSessionTimeout.HasValue)
		{
			StopUserDisconnectTimer();
			TimeSpan timeSpan = TimeSpan.FromSeconds(Settings.PendingSessionTimeout.Value);
			lock (USER_DISCONNECT_TIMER_LOCK)
			{
				USER_DISCONNECT_TIMER = new System.Threading.Timer(OnUserDisconnectTimerCallBack, null, timeSpan, timeSpan);
			}
		}
	}

	private void OnUserDisconnectTimerCallBack(object state)
	{
		StopUserDisconnectTimer();
		OnUserLogout();
	}

	private void OnConnectionReceived(object sender, SentReceivedEventArgs args)
	{
		try
		{
			if (args.DataFlags == DataFlags.ControlData || IgnoreUdpRequests || !AllowConnecting)
			{
				return;
			}
			byte[] buffer = args.Buffer;
			if (buffer == null)
			{
				return;
			}
			ConnectionSettings connectionSettings = Transformation.DeSerialize<ConnectionSettings>(buffer, args.DataOffset, args.DataSize);
			if (connectionSettings == null || !connectionSettings.IsValid || !Monitor.TryEnter(NET_OP_LOCK))
			{
				return;
			}
			try
			{
				if (!IsConnecting && !StreamConnection.IsConnected)
				{
					Settings.Connection = connectionSettings;
					Settings.Connection.IsInitialized = true;
					Connect(saveSettings: true, randomDelay: true);
				}
			}
			catch
			{
				throw;
			}
			finally
			{
				Monitor.Exit(NET_OP_LOCK);
			}
		}
		catch (Exception ex)
		{
			Log.AddError("Failed processing UDP message.", ex, LogCategories.Network);
		}
	}

	private void OnConnectionTimerTick(object sender, ElapsedEventArgs e)
	{
		if (!AllowConnecting)
		{
			return;
		}
		try
		{
			if (!Monitor.TryEnter(NET_OP_LOCK))
			{
				return;
			}
			try
			{
				if (TraceEnabled && !StreamConnection.IsConnected)
				{
					TraceWrite("Connection timer entered.\n" + string.Format("{0}: {1}\n", "IsInitialized", Settings.Connection.IsInitialized) + string.Format("{0}: {1}\n", "IsConnecting", IsConnecting) + string.Format("{0}: {1}", "IsConnected", StreamConnection.IsConnected), (string)null, "OnConnectionTimerTick", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 683);
				}
				if (!IsConnecting && Settings.Connection.IsInitialized && !StreamConnection.IsConnected)
				{
					Connect(saveSettings: false, randomDelay: true);
				}
			}
			catch
			{
				throw;
			}
			finally
			{
				Monitor.Exit(NET_OP_LOCK);
			}
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "OnConnectionTimerTick", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Network.cs", 709);
		}
	}

	public IEnumerable<IRestriction> GetRestrictionsOfType(RestrictionType type)
	{
		lock (SEC_OP_LOCK)
		{
			return Restrictions.Where((IRestriction x) => x.Type == type).ToList();
		}
	}

	public bool TryValidate(IWindowInfo windowInfo)
	{
		if (windowInfo == null)
		{
			throw new ArgumentNullException("windowInfo");
		}
		bool flag = false;
		if (windowInfo.IsValidWindow && windowInfo.ProcessId != 0)
		{
			foreach (IRestriction item in GetRestrictionsOfType(RestrictionType.WindowName))
			{
				try
				{
					if (!string.IsNullOrWhiteSpace(item.Parameter))
					{
						Wildcard wildcard = new Wildcard(item.Parameter, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
						if (windowInfo.IsValidWindow && wildcard.IsMatch(windowInfo.Title))
						{
							flag = true;
							break;
						}
					}
				}
				catch (Exception ex)
				{
					LogRestrictionValidationError(ex);
				}
			}
			foreach (IRestriction item2 in GetRestrictionsOfType(RestrictionType.ClassName))
			{
				try
				{
					if (!string.IsNullOrWhiteSpace(item2.Parameter))
					{
						Wildcard wildcard2 = new Wildcard(item2.Parameter, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
						if (windowInfo.IsValidWindow && wildcard2.IsMatch(windowInfo.ClassName))
						{
							flag = true;
							break;
						}
					}
				}
				catch (Exception ex2)
				{
					LogRestrictionValidationError(ex2);
				}
			}
		}
		if (flag)
		{
			Log.AddWarning($"Window {windowInfo.ToString()} matched security filter.", LogCategories.Configuration);
			try
			{
				if (windowInfo.Process != null)
				{
					windowInfo.Process.Kill();
					return flag;
				}
				windowInfo.Destroy();
				return flag;
			}
			catch (Win32Exception)
			{
				throw;
			}
		}
		return flag;
	}

	public bool TryValidate(ICoreProcess process)
	{
		bool flag = false;
		foreach (IRestriction item in GetRestrictionsOfType(RestrictionType.FileName))
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(item.Parameter))
				{
					Wildcard wildcard = new Wildcard(item.Parameter, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
					bool flag2 = false;
					bool num = wildcard.IsMatch(process.ProcessName) | wildcard.IsMatch(process.ProcessExeName);
					if (process.IsAccessible && !string.IsNullOrWhiteSpace(process.MainModule.FileName))
					{
						flag2 = wildcard.IsMatch(process.MainModule.FileName);
					}
					if (num || flag2)
					{
						flag = true;
						break;
					}
				}
			}
			catch (Exception ex)
			{
				LogRestrictionValidationError(ex);
			}
		}
		if (flag)
		{
			Log.AddWarning($"Process {process.ToString()} matched security filter.", LogCategories.Configuration);
			process.Kill();
		}
		return flag;
	}

	public void TerminateUserProcesses()
	{
		try
		{
			lock (UserProcesses)
			{
				foreach (ICoreProcess item in UserProcesses.Values.ToList())
				{
					try
					{
						if (!item.HasExited)
						{
							item.Kill();
						}
					}
					catch (ArgumentException)
					{
					}
					catch (Exception ex2)
					{
						Log.AddError($"Could not terminate user process. Proces Name {item.ProcessName} Process Id {item.Id}", ex2, LogCategories.Generic);
					}
					finally
					{
						UserProcesses.Remove(item.Id);
					}
				}
			}
		}
		catch (Exception ex3)
		{
			Log.AddError("Error executing user process termination routine.", ex3, LogCategories.Generic);
		}
	}

	private void OnSecurityInitialize()
	{
		ProcessTrace.ProcessCreated += OnSystemProcessStarted;
		ProcessTrace.ProcessExited += OnSystemProcessExited;
		ShellHook.WindowRedrawn += OnWindowEvent;
		ShellHook.WindowCreated += OnWindowEvent;
		ShellHook.WindowActivated += OnWindowEvent;
		ShellHook.RudeAppActivated += OnRudeWindowActivated;
		KeyBoardHook.HookEvent += OnGlobalKeyHookEvent;
		MouseHook.Event += OnGlobalMouseHookEvent;
		KeyBoardHook.AddHandler(Keys.R, defaultModifiers, KeyState.Up, OnKeyHookEvent);
		KeyBoardHook.AddHandler(Keys.S, defaultModifiers, KeyState.Up, OnKeyHookEvent);
		KeyBoardHook.AddHandler(Keys.Oemtilde, defaultModifiers, KeyState.Up, OnKeyHookEvent);
		KeyBoardHook.AddHandler(Keys.A, defaultModifiers, KeyState.Up, OnKeyHookEvent);
		if (ENABLE_DIAGNOSTIC_SHORTCUTS)
		{
			KeyBoardHook.AddHandler(Keys.D, defaultModifiers, KeyState.Up, OnKeyHookEvent);
			KeyBoardHook.AddHandler(Keys.T, defaultModifiers, KeyState.Up, OnKeyHookEvent);
		}
		if (!KeyBoardHook.IsHooked)
		{
			Application?.Dispatcher?.Invoke(delegate
			{
				KeyBoardHook.Hook();
			});
		}
		if (!WindowEventHook.IsHooked)
		{
			Application?.Dispatcher?.Invoke(delegate
			{
				WindowEventHook.Hook();
			});
		}
	}

	private void OnSecurityDeinitialize()
	{
		ProcessTrace.ProcessCreated -= OnSystemProcessExited;
		ProcessTrace.ProcessExited -= OnSystemProcessStarted;
		ShellHook.WindowRedrawn -= OnWindowEvent;
		ShellHook.WindowCreated -= OnWindowEvent;
		ShellHook.WindowActivated -= OnWindowEvent;
		ShellHook.RudeAppActivated -= OnRudeWindowActivated;
		KeyBoardHook.HookEvent -= OnGlobalKeyHookEvent;
		MouseHook.Event -= OnGlobalMouseHookEvent;
		if (KeyBoardHook.IsHooked)
		{
			Application?.Dispatcher?.Invoke(delegate
			{
				KeyBoardHook.Unhook();
			});
		}
		if (MouseHook.IsHooked)
		{
			Application?.Dispatcher?.Invoke(delegate
			{
				MouseHook.Unhook();
			});
		}
		if (WindowEventHook.IsHooked)
		{
			Application?.Dispatcher?.Invoke(delegate
			{
				WindowEventHook.UnHook();
			});
		}
	}

	private void ActivateSecurityProfile(bool isEnabled = false, bool wasEnabled = false)
	{
		ISecurityProfile securityProfile = CurrentSecurityProfile;
		if (securityProfile == null)
		{
			return;
		}
		lock (SEC_OP_LOCK)
		{
			DeactivateSecurityProfile(throwOnError: false, notify: false);
			SetDisabledDrives(securityProfile.DisabledDrives);
			Restrictions.Clear();
			Restrictions.AddRange(securityProfile.Restrictions.ToList());
			foreach (ISecurityPolicy policy in securityProfile.Policies)
			{
				Policies.SetPolicyByEnum(policy.Type, enable: true, shouldThrow: true);
			}
		}
		NotifySettingsChanged();
		RaiseSecurityChange(isEnabled, wasEnabled, activeProfile: true);
	}

	private void DeactivateSecurityProfile(bool throwOnError = true, bool notify = true)
	{
		Policies.ClearPolicies(throwOnError);
		SetDisabledDrives(0);
		if (notify)
		{
			NotifySettingsChanged();
		}
	}

	private void RevalidateRestrictions()
	{
		try
		{
			foreach (IntPtr item in WindowEnumerator.ListVisibleHandles())
			{
				OnWindowEvent(item);
			}
			foreach (ICoreProcess process in CoreProcess.GetProcesses())
			{
				TryValidate(process);
			}
		}
		catch (Exception ex)
		{
			LogRestrictionValidationError(ex);
		}
	}

	private void LogRestrictionValidationError(Exception ex)
	{
		if (ex is Win32Exception)
		{
			TraceWrite(ex, "LogRestrictionValidationError", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 654);
		}
		else
		{
			Log.AddError("Restriction validation failed.", ex, LogCategories.Operation);
		}
	}

	private void SetDisabledDrives(int drivesFlag)
	{
		try
		{
			using (RegistryKey registryKey = CoreRegistryFile.CreateKey(RegistryHive.CurrentUser, "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer"))
			{
				registryKey?.SetValue("NoDrives", drivesFlag);
			}
			using RegistryKey registryKey2 = CoreRegistryFile.CreateKey(RegistryHive.CurrentUser, "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer");
			registryKey2?.SetValue("NoViewOnDrive", drivesFlag);
		}
		catch (Exception ex)
		{
			Log.AddError("Could not set drive security settings.", ex, LogCategories.Generic);
		}
	}

	private void NotifySettingsChanged()
	{
		IntPtr intPtr = Marshal.StringToHGlobalUni("Policy");
		try
		{
			if (!User32.SendNotifyMessage(new IntPtr(65535), 26u, (IntPtr)0, intPtr))
			{
				TraceWrite("Could not notify of settings change.", (string)null, "NotifySettingsChanged", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 684);
			}
			if (!User32.SendNotifyMessage(new IntPtr(65535), 26u, (IntPtr)1, intPtr))
			{
				TraceWrite("Could not notify of settings change.", (string)null, "NotifySettingsChanged", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 687);
			}
		}
		catch
		{
			throw;
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
		}
		if (!Userenv.RefreshPolicyEx(bMachine: true, 1u))
		{
			TraceWrite("Computer policy refresh failed.", (string)null, "NotifySettingsChanged", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 699);
		}
		if (!Userenv.RefreshPolicyEx(bMachine: false, 1u))
		{
			TraceWrite("User policy refresh failed.", (string)null, "NotifySettingsChanged", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 702);
		}
	}

	private void OnKeyHookEvent(object sender, KeyboardHookEventArgs e)
	{
		try
		{
			if (e.Key == Keys.R && !IsUserLoggedIn)
			{
				SetPowerState(PowerStates.Reboot);
			}
			else if (e.Key == Keys.S && !IsUserLoggedIn)
			{
				SetPowerState(PowerStates.Shutdown);
			}
			else if (e.Key == Keys.Oemtilde || e.Key == Keys.A)
			{
				if (ManagerViewModel == null || (!ManagerViewModel.IsLoaded && Settings.IsInitialized))
				{
					ManagerViewModel = new ManagerLoginViewModel(this);
					ManagerViewModel.Closed += OnManagerUIClosed;
					ManagerViewModel.Shown += OnManagerUIShown;
					ManagerViewModel.Show(ShellWindowHandle);
					if (ShellWindowHandle != IntPtr.Zero)
					{
						User32.SetWindowPos(ShellWindowHandle, (HWND)(int)ManagerViewModel.WindowHandle, 0, 0, 0, 0, SWP.SWP_TOPMPOST);
					}
					ManagerViewModel.Window.Focus();
					Keyboard.Focus(ManagerViewModel.Window);
				}
			}
			else if (e.Key == Keys.D)
			{
				MessageBoxEx.Show(string.Format("{0}: {1}\n", "IsConnecting", IsConnecting) + string.Format("{0}: {1}\n", "AllowConnecting", AllowConnecting) + string.Format("{0}: {1}\n", "IgnoreUdpRequests", IgnoreUdpRequests) + string.Format("{0}: {1}\n", "IsNetworkInitialized", base.IsNetworkInitialized) + string.Format("{0}: {1}\n", "IsAvailable", base.IsAvailable) + string.Format("{0}: {1}\n", "ConnectionTimer", base.ConnectionTimer?.Enabled ?? false) + $"TCP Connected: {StreamConnection.IsConnected}\n" + $"TCP Keep Alive: {(StreamConnection as ISocketConnection)?.KeepAlive ?? false}\n" + $"TCP Keep Interval: {(StreamConnection as ISocketConnection)?.KeepAliveInterval ?? 0}\n" + $"TCP Keep Alive Time Out: {(StreamConnection as ISocketConnection)?.KeepAliveTimeOut ?? 0}\n" + "Settings : " + (Settings?.Connection?.ToString() ?? "None"), "Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Asterisk, DIAGNOSTIC_MESSAGE_TIMEOUT);
			}
			else if (e.Key == Keys.T)
			{
				bool flag2 = (TraceEnabled = !TraceEnabled);
				MessageBoxEx.Show("Tracing is now " + (flag2 ? "ON" : "OFF") + ".", "Tracing", MessageBoxButtons.OK, MessageBoxIcon.Asterisk, DIAGNOSTIC_MESSAGE_TIMEOUT);
			}
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "OnKeyHookEvent", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 805);
		}
	}

	private void OnGlobalMouseHookEvent(object sender, MouseLowLevelHookEventArgs e)
	{
		e.Handled = IsInputLocked && !e.IsInjected;
	}

	private void OnGlobalKeyHookEvent(object sender, KeyboardHookEventArgs e)
	{
		if (IsInputLocked && !e.IsInjected)
		{
			e.Handled = true;
		}
		else
		{
			if (!IsSecurityEnabled || IsInMaintenance)
			{
				return;
			}
			if (!IsUserLoggedIn || IsInGracePeriod || IsUserLocked)
			{
				if (e.Key == Keys.Escape && e.Modifiers.HasFlag(ModifierKeys.Alt))
				{
					e.Handled = true;
				}
				else if (e.Key == Keys.D && e.Modifiers.HasFlag(ModifierKeys.Windows) && e.Modifiers.HasFlag(ModifierKeys.Control))
				{
					e.Handled = true;
				}
				else if (((e.Key == Keys.Tab) & (e.Modifiers.HasFlag(ModifierKeys.Alt) || e.Modifiers.HasFlag(ModifierKeys.Shift))) || ((e.Key == Keys.Escape) & (e.Modifiers == ModifierKeys.Control)) || ((e.Key == Keys.Escape) & (e.Modifiers == ModifierKeys.Control)) || ((e.Key == Keys.LWin) & (e.Modifiers == ModifierKeys.None)) || ((e.Key == Keys.RWin) & (e.Modifiers == ModifierKeys.None)) || ((e.Key == Keys.F4) & (e.Modifiers == ModifierKeys.Alt)) || ((e.Key == Keys.Space) & (e.Modifiers == ModifierKeys.Alt)))
				{
					e.Handled = true;
				}
			}
			else if (StartMenuDisabled && e.Key == Keys.Escape && e.Modifiers == ModifierKeys.Control)
			{
				e.Handled = true;
			}
			else if (StickyShell && e.Key == Keys.D && e.Modifiers.HasFlag(ModifierKeys.Windows))
			{
				e.Handled = true;
			}
			else if (DisableDesktopSwitching && e.Key == Keys.D && e.Modifiers.HasFlag(ModifierKeys.Windows) && e.Modifiers.HasFlag(ModifierKeys.Control))
			{
				e.Handled = true;
			}
		}
	}

	private void OnSystemProcessStarted(object sender, ICoreProcess process)
	{
		if (IsInMaintenance)
		{
			return;
		}
		lock (UserProcesses)
		{
			if (TraceUserProcesses && !UserProcesses.ContainsKey(process.Id))
			{
				UserProcesses.Add(process.Id, process);
			}
		}
		if (IsSecurityEnabled)
		{
			TryValidate(process);
		}
	}

	private void OnSystemProcessExited(object sender, ICoreProcess process)
	{
		lock (UserProcesses)
		{
			UserProcesses.Remove(process.Id);
		}
	}

	private void OnWindowEvent(IntPtr hWnd)
	{
		try
		{
			if (IsSecurityEnabled && hWnd != IntPtr.Zero)
			{
				WindowInfo windowInfo = new WindowInfo(hWnd);
				TryValidate(windowInfo);
			}
		}
		catch (Exception ex)
		{
			Log.AddError("Window validation failed.", ex, LogCategories.Generic);
		}
	}

	private void OnRudeWindowActivated(IntPtr hWnd)
	{
		if ((!IsUserLoggedIn || IsInGracePeriod || IsUserLocked) && IsSecurityEnabled)
		{
			Shell.EnumWindows(delegate(IntPtr windowHandle, IntPtr wParam)
			{
				OnWindowEvent(windowHandle);
				return true;
			});
			if (!IsClientWindow(hWnd))
			{
				OnShellWindowFitIn();
			}
		}
	}

	private void OnManagerUIShown(object sender, EventArgs e)
	{
		ManagerLoginViewModel managerViewModel = ManagerViewModel;
		if (managerViewModel != null)
		{
			managerViewModel.Shown -= OnManagerUIShown;
		}
	}

	private void OnManagerUIClosed(object sender, EventArgs e)
	{
		ManagerLoginViewModel managerViewModel = ManagerViewModel;
		if (managerViewModel != null)
		{
			managerViewModel.Closed -= OnManagerUIClosed;
		}
	}

	private void OnWindowEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
	{
		if (IsShuttingDown || IsInMaintenance || idObject != 0)
		{
			return;
		}
		try
		{
			IntPtr shellWindowHandle = ShellWindowHandle;
			if ((shellWindowHandle != IntPtr.Zero && shellWindowHandle == hwnd) || (hwnd != IntPtr.Zero && IsClientWindow(hwnd)) || dwEventThread == Kernel32.GetCurrentThreadId())
			{
				return;
			}
			switch (eventType)
			{
			case 3u:
			case 32768u:
			case 32770u:
			case 32773u:
			case 32779u:
			{
				IntPtr intPtr = User32.GetShellWindow();
				if (hwnd == intPtr)
				{
					if (eventType != 32768)
					{
						break;
					}
					if (!IsUserLoggedIn || IsInGracePeriod || IsUserLocked)
					{
						Shell.TryHideExplorerWindows();
					}
					else if (IsSecurityEnabled)
					{
						if (StartMenuDisabled)
						{
							Shell.TryHideStartButton();
						}
						else
						{
							Shell.TryShowStartButton();
						}
						if (StickyShell)
						{
							Shell.TryHideShowDesktopButton();
						}
						else
						{
							Shell.TryShowShowDesktopButton();
						}
					}
					break;
				}
				if (StartMenuDisabled && IsSecurityEnabled)
				{
					if (!Shell.TryFindStartMenuHandle(out var hwnd2))
					{
						hwnd2 = User32.FindWindow("DV2ControlHost", "Start menu");
					}
					if (hwnd2 == hwnd)
					{
						User32.ShowWindow(hwnd2, Win32API.Headers.WinUser.Enumerations.SW.SW_HIDE);
						break;
					}
					if (Shell.TryFindSearchMenuHandle(out var hwnd3) && hwnd3 == hwnd)
					{
						User32.ShowWindow(hwnd3, Win32API.Headers.WinUser.Enumerations.SW.SW_HIDE);
						break;
					}
				}
				if (StickyShell && IsSecurityEnabled && Shell.TryFindShowDesktopButton(out var hWnd) && hWnd == hwnd)
				{
					Shell.TryHideShowDesktopButton();
				}
				if (IsUserLoggedIn && (!IsInGracePeriod & !IsUserLocked))
				{
					break;
				}
				int num = (from PROCESS in Process.GetProcessesByName("ShellExperienceHost")
					select PROCESS.Id).FirstOrDefault();
				int num2 = (from PROCESS in Process.GetProcessesByName("SearchUI")
					select PROCESS.Id).FirstOrDefault();
				User32.GetWindowThreadProcessId(intPtr, out var lpdwProcessId);
				User32.GetWindowThreadProcessId(hwnd, out var lpdwProcessId2);
				bool num3 = lpdwProcessId2 == lpdwProcessId;
				bool flag = lpdwProcessId2 == num;
				bool flag2 = lpdwProcessId2 == num2;
				if (!(num3 || flag || flag2))
				{
					IntPtr ancestor = User32.GetAncestor(hwnd, GA.GA_ROOTOWNER);
					IntPtr intPtr2 = ((ancestor != IntPtr.Zero) ? ancestor : hwnd);
					if (!(intPtr2 == ShellWindowHandle) && Shell.IsAppWindow(intPtr2) && !User32.IsIconic(intPtr2))
					{
						User32.ShowWindowAsync(intPtr2, Win32API.Headers.WinUser.Enumerations.SW.SW_MINIMIZE);
						User32.SetWindowPos(shellWindowHandle, HWND.HWND_TOPMOST, 0, 0, 0, 0, (SWP)16403u);
					}
				}
				break;
			}
			}
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "OnWindowEventProc", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 1151);
		}
	}

	private async void ProcessCreationEvent(object sender, CbprocessProcessCreationEventArgs e)
	{
		try
		{
			ProcessCreatingEventArgs processCreatingEventArgs = new ProcessCreatingEventArgs(e.ProcessId, e.ParentProcessId, e.CreatingProcessId, e.CreatingThreadId, e.ProcessName, e.ImageFileName, e.FileOpenNameAvailable, e.CommandLine);
			try
			{
				IEnumerable<EventHandler<ProcessCreatingEventArgs>> enumerable = this.ProcessCreating?.GetInvocationList().Cast<EventHandler<ProcessCreatingEventArgs>>();
				if (enumerable != null)
				{
					ParallelEx.ParallelInvoke(this, enumerable, processCreatingEventArgs);
				}
			}
			catch (AggregateException ex)
			{
				TraceWrite(ex, "ProcessCreationEvent", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 1188);
			}
			catch (Exception ex2)
			{
				TraceWrite(ex2, "ProcessCreationEvent", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 1192);
			}
			if (processCreatingEventArgs.ResultCode == 0 && processCreatingEventArgs.CreatingProcessId != ClientProcessId && processCreatingEventArgs.FileOpenNameAvailable)
			{
				if (processCreatingEventArgs.ImageFileName.Length <= 4)
				{
					return;
				}
				string text = e.ImageFileName.Substring(4);
				if (string.IsNullOrWhiteSpace(text))
				{
					return;
				}
				string FULL_PROCESS_NAME = Path.GetFullPath(text);
				string PROCESS_NAME = Path.GetFileName(FULL_PROCESS_NAME);
				string PROCESS_NAME_WITHOUT_EXTENSION = Path.GetFileNameWithoutExtension(PROCESS_NAME);
				if (IsSecurityEnabled && CurrentSecurityProfile != null)
				{
					if (Monitor.TryEnter(SEC_OP_LOCK, 1000))
					{
						try
						{
							IEnumerable<string> source = from RESTRICTION in GetRestrictionsOfType(RestrictionType.FileName)
								where !string.IsNullOrWhiteSpace(RESTRICTION.Parameter)
								select RESTRICTION.Parameter;
							IEnumerable<string> source2 = source.Where((string RESTRICTION) => RESTRICTION.IndexOf('*') != -1);
							IEnumerable<string> source3 = source.Where((string RESTRICTION) => RESTRICTION.IndexOf('*') == -1);
							if (source2.Any(delegate(string RESTRICTION)
							{
								Wildcard wildcard = new Wildcard(RESTRICTION, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
								return wildcard.IsMatch(FULL_PROCESS_NAME) || wildcard.IsMatch(PROCESS_NAME) || wildcard.IsMatch(PROCESS_NAME_WITHOUT_EXTENSION);
							}))
							{
								processCreatingEventArgs.ResultCode = 5;
							}
							if (source3.Any((string PROCES) => PROCES.IndexOf(FULL_PROCESS_NAME, StringComparison.InvariantCultureIgnoreCase) != -1 || PROCES.IndexOf(PROCESS_NAME, StringComparison.InvariantCultureIgnoreCase) != -1 || PROCES.IndexOf(PROCESS_NAME_WITHOUT_EXTENSION, StringComparison.InvariantCultureIgnoreCase) != -1))
							{
								processCreatingEventArgs.ResultCode = 5;
							}
						}
						catch
						{
							throw;
						}
						finally
						{
							Monitor.Exit(SEC_OP_LOCK);
						}
					}
					if (TaskManagerDisabled && string.Compare(FULL_PROCESS_NAME, TASK_MANAGER_FILE_NAME, ignoreCase: true) == 0)
					{
						processCreatingEventArgs.ResultCode = 5;
					}
				}
			}
			e.ResultCode = processCreatingEventArgs.ResultCode;
			try
			{
				IEnumerable<EventHandler<ProcessCreatingEventArgs>> enumerable2 = this.ProcessPostCreating?.GetInvocationList().Cast<EventHandler<ProcessCreatingEventArgs>>();
				if (enumerable2 != null)
				{
					await ParallelEx.ParallelInvokeAsync(this, enumerable2, processCreatingEventArgs).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			catch (AggregateException ex3)
			{
				TraceWrite(ex3, "ProcessCreationEvent", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 1318);
			}
			catch (Exception ex4)
			{
				TraceWrite(ex4, "ProcessCreationEvent", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 1325);
			}
		}
		catch (Exception ex5)
		{
			TraceWrite(ex5, "ProcessCreationEvent", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 1332);
		}
	}

	private async void OnProcessTermination(object sender, CbprocessProcessTerminationEventArgs e)
	{
		try
		{
			ProcessTerminatedEventArgs args = new ProcessTerminatedEventArgs(e.ProcessId, e.ProcessName);
			IEnumerable<EventHandler<ProcessTerminatedEventArgs>> enumerable = this.ProcessTerminated?.GetInvocationList().Cast<EventHandler<ProcessTerminatedEventArgs>>();
			if (enumerable != null)
			{
				await ParallelEx.ParallelInvokeAsync(this, enumerable, args).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (AggregateException ex)
		{
			TraceWrite(ex, "OnProcessTermination", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 1361);
		}
		catch (Exception ex2)
		{
			TraceWrite(ex2, "OnProcessTermination", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Securtiy.cs", 1368);
		}
	}

	private void RegistryOnBeforeOpenKey(object sender, CbregistryBeforeOpenKeyEventArgs e)
	{
		e.ResultCode = 5;
	}

	public Task<byte[]> TryGetImageFromCacheAsync(int entityId, ImageType type)
	{
		return TryGetImageFromCacheAsync(entityId, type, default(CancellationToken));
	}

	public Task<byte[]> TryGetImageFromCacheAsync(int entityId, ImageType type, CancellationToken ct)
	{
		throw new NotImplementedException();
	}

	public Task<byte[]> TryGetImageDataAsync(int entityId, ImageType type)
	{
		return TryGetImageDataAsync(entityId, type, default(CancellationToken));
	}

	public async Task<byte[]> TryGetImageDataAsync(int entityId, ImageType type, CancellationToken ct)
	{
		if (!Dispatcher.IsValid)
		{
			return null;
		}
		if (!IsUserLoggedIn && !IsUserLoggingIn)
		{
			return null;
		}
		using CancellationTokenSource GLOBAL_TOKEN_SOURCE = GetUserTokenSourceLinked(ct);
		CancellationToken GLOBAL_CANCELLATION_TOKEN = GLOBAL_TOKEN_SOURCE.Token;
		await APP_IMAGE_GET_LOCK.WaitAsync(GLOBAL_CANCELLATION_TOKEN).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			string text = Settings?.DataPath;
			string CACHE_PATH = null;
			bool CACHE_ACCESSIBLE = false;
			if (!string.IsNullOrWhiteSpace(text))
			{
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
				CACHE_PATH = Path.Combine(text, "Cache", "Image", type switch
				{
					ImageType.Application => "App", 
					ImageType.Executable => "AppExe", 
					ImageType.ProductDefault => "Product", 
					_ => throw new NotSupportedException(), 
				});
				if (!Directory.Exists(CACHE_PATH))
				{
					Directory.CreateDirectory(CACHE_PATH);
				}
				CACHE_ACCESSIBLE = true;
				try
				{
					byte[] array = await TryGetImageHashAsync(entityId, type, GLOBAL_CANCELLATION_TOKEN);
					if (array == null)
					{
						return null;
					}
					string path = array.ToHex(upperCase: true);
					string path2 = Path.Combine(CACHE_PATH, path);
					if (File.Exists(path2))
					{
						return File.ReadAllBytes(path2);
					}
				}
				catch (IOException)
				{
				}
				catch (OperationNotSupportedException)
				{
				}
				catch (OperationCanceledException)
				{
					throw;
				}
			}
			ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.GetImageData, type, entityId);
			await op.StartAsync(ct);
			await op.WaitCompleteAsync(ct);
			byte[] array2 = op.DataObject as byte[];
			if (array2 != null)
			{
				try
				{
					if (CACHE_ACCESSIBLE)
					{
						using SHA1CryptoServiceProvider sHA1CryptoServiceProvider = new SHA1CryptoServiceProvider();
						string path3 = sHA1CryptoServiceProvider.ComputeHash(array2).ToHex(upperCase: true);
						string path4 = Path.Combine(CACHE_PATH, path3);
						try
						{
							File.WriteAllBytes(path4, array2);
						}
						catch (IOException)
						{
							File.Delete(path4);
							throw;
						}
					}
				}
				catch (Exception ex5)
				{
					TraceWrite(ex5, "TryGetImageDataAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Services.cs", 188);
				}
			}
			return array2;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex7)
		{
			Log.AddError("Could not obtain image data.", ex7, LogCategories.Generic);
		}
		finally
		{
			APP_IMAGE_GET_LOCK.Release();
		}
		return null;
	}

	public async Task<byte[]> TryGetImageHashAsync(int entityId, ImageType type, CancellationToken ct)
	{
		if (!Dispatcher.IsValid)
		{
			return null;
		}
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.GetImageHash, type, entityId);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.DataObject as byte[];
	}

	private CancellationTokenSource GetUserTokenSource()
	{
		lock (USER_TOKEN_SOURCE_LOCK)
		{
			if (USER_TASK_CTS == null)
			{
				USER_TASK_CTS = new CancellationTokenSource();
			}
		}
		return USER_TASK_CTS;
	}

	private CancellationTokenSource GetUserTokenSourceLinked(CancellationToken ct)
	{
		lock (USER_TOKEN_SOURCE_LOCK)
		{
			return CancellationTokenSource.CreateLinkedTokenSource(GetUserTokenSource().Token, ct);
		}
	}

	public IEnumerable<IAppExeViewModel> Get()
	{
		return APP_EXE_LOOK_UP.Values;
	}

	IAppExeViewModel IViewModelLocator<IAppExeViewModel>.TryGetViewModel(int itemId)
	{
		if (!APP_EXE_LOOK_UP.TryGetValue(itemId, out var value))
		{
			return null;
		}
		return value;
	}

	public ICategoryDisplayViewModel TryGetViewModel(int itemId)
	{
		if (!CATEGORY_LOOK_UP.TryGetValue(itemId, out var value))
		{
			return null;
		}
		return value;
	}

	IEnumerable<ICategoryDisplayViewModel> IViewModelLocator<ICategoryDisplayViewModel>.Get()
	{
		return CATEGORY_LOOK_UP.Values;
	}

	IAppViewModel IViewModelLocator<IAppViewModel>.TryGetViewModel(int itemId)
	{
		if (!APP_LOOK_UP.TryGetValue(itemId, out var value))
		{
			return null;
		}
		return value;
	}

	IEnumerable<IAppViewModel> IViewModelLocator<IAppViewModel>.Get()
	{
		return APP_LOOK_UP.Values;
	}

	IAppEnterpriseDisplayViewModel IViewModelLocator<IAppEnterpriseDisplayViewModel>.TryGetViewModel(int itemId)
	{
		if (!APP_ENTERPRISE_LOOK_UP.TryGetValue(itemId, out var value))
		{
			return null;
		}
		return value;
	}

	IEnumerable<IAppEnterpriseDisplayViewModel> IViewModelLocator<IAppEnterpriseDisplayViewModel>.Get()
	{
		return APP_ENTERPRISE_LOOK_UP.Values;
	}

	IEnumerable<INewsViewModel> IViewModelLocator<INewsViewModel>.Get()
	{
		return NEWS_LOOKUP.Values;
	}

	INewsViewModel IViewModelLocator<INewsViewModel>.TryGetViewModel(int itemId)
	{
		if (!NEWS_LOOKUP.TryGetValue(itemId, out var value))
		{
			return null;
		}
		return value;
	}

	IEnumerable<IProductViewModel> IViewModelLocator<IProductViewModel>.Get()
	{
		return PRODUCT_LOOKUP.Values;
	}

	IProductViewModel IViewModelLocator<IProductViewModel>.TryGetViewModel(int itemId)
	{
		if (!PRODUCT_LOOKUP.TryGetValue(itemId, out var value))
		{
			return null;
		}
		return value;
	}

	IEnumerable<IProductGroupViewModel> IViewModelLocator<IProductGroupViewModel>.Get()
	{
		return PRODUCT_GROUP_LOOKUP.Values;
	}

	IProductGroupViewModel IViewModelLocator<IProductGroupViewModel>.TryGetViewModel(int itemId)
	{
		if (!PRODUCT_GROUP_LOOKUP.TryGetValue(itemId, out var value))
		{
			return null;
		}
		return value;
	}

	IEnumerable<IPaymentMethodViewModel> IViewModelLocator<IPaymentMethodViewModel>.Get()
	{
		return PAYMENT_METHOD_LOOKUP.Values;
	}

	IPaymentMethodViewModel IViewModelLocator<IPaymentMethodViewModel>.TryGetViewModel(int itemId)
	{
		if (!PAYMENT_METHOD_LOOKUP.TryGetValue(itemId, out var value))
		{
			return null;
		}
		return value;
	}

	public async System.Threading.Tasks.Task InitDataAsync()
	{
		_ = 5;
		try
		{
			try
			{
				ProcessContainer(await AppContainerGetAsync());
			}
			catch (OperationNotSupportedException)
			{
			}
			try
			{
				ProcessContainer(await AppInfoContainerGetAsync());
			}
			catch (OperationNotSupportedException)
			{
			}
			try
			{
				ProcessUserContainer(await AppInfoUserContainerGetAsync());
			}
			catch (OperationNotSupportedException)
			{
			}
			try
			{
				ProcessContainer(await NewsEntityContainerGetAsync());
			}
			catch (OperationNotSupportedException)
			{
			}
			try
			{
				UsageSessionInfo usageSessionInfo = await UsageSessionInfoGetActiveAsync();
				this.UsageSessionChanged?.Invoke(this, new UsageSessionChangedEventArgs(usageSessionInfo.UserId, usageSessionInfo.CurrentUsageType, usageSessionInfo.TimePorduct));
			}
			catch (OperationNotSupportedException)
			{
			}
			try
			{
				ProcessContainer(await OrderingDataGetAsync());
			}
			catch (OperationNotSupportedException)
			{
			}
		}
		catch (Exception ex7)
		{
			TraceWrite(ex7, "InitDataAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Services.cs", 507);
		}
		finally
		{
			GC.Collect();
		}
	}

	public async Task<AppEntityContainer> AppContainerGetAsync()
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.GetAppContainer);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<AppEntityContainer>();
	}

	public async Task<AppInfoContainer> AppInfoContainerGetAsync()
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.GetAppInfoContainer);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<AppInfoContainer>();
	}

	public async Task<AppInfoContainer> AppInfoUserContainerGetAsync()
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.GetAppInfoUserContainer);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<AppInfoContainer>();
	}

	public async Task<NewsEntityContainer> NewsEntityContainerGetAsync()
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.GetNewsContainer);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<NewsEntityContainer>();
	}

	public async Task<ClientOrderingData> OrderingDataGetAsync()
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.GetOrderingData);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<ClientOrderingData>();
	}

	private void ProcessContainer(AppEntityContainer container)
	{
		if (container == null)
		{
			throw new ArgumentNullException("container");
		}
		DataInitialized = false;
		ShellViewModel.Apps.CancelDeferredRefresh();
		ShellViewModel.Apps.CategoryFilter.CancelDeferredRefresh();
		container.Categories.Select((AppCategory cat) => CreateViewModel(cat)).ToList();
		container.Enterprises.Select((AppEnterprise ent) => CreateViewModel(ent)).ToList();
		container.Apps.Select((App app) => CreateViewModel(app)).ToList();
		container.Executables.Select((AppExe exe) => CreateViewModel(exe)).ToList();
		ShellViewModel.Apps.DeferrRefresh();
		ShellViewModel.Apps.CategoryFilter.DeferrRefresh();
		DataInitialized = true;
	}

	private void ProcessContainer(AppInfoContainer container)
	{
		if (container == null)
		{
			throw new ArgumentNullException("container");
		}
		foreach (ServerService.AppRating item in container.AppRating)
		{
			if (APP_LOOK_UP.TryGetValue(item.AppId, out var value))
			{
				value.Rating = item.Value;
				value.TotalRates = item.RatingsCount;
			}
		}
		foreach (ServerService.AppStat item2 in container.AppStat)
		{
			if (APP_LOOK_UP.TryGetValue(item2.AppId, out var value2))
			{
				value2.TotalExecutions = item2.TotalExecutions;
				value2.TotalExecutionTime = item2.TotalSpan;
			}
		}
		foreach (AppExeStat item3 in container.AppExeStat)
		{
			if (APP_EXE_LOOK_UP.TryGetValue(item3.AppExeId, out var value3))
			{
				value3.TotalExecutions = item3.TotalExecutions;
				value3.TotalTime = item3.TotalSpan;
			}
		}
	}

	private void ProcessUserContainer(AppInfoContainer container)
	{
		if (container == null)
		{
			throw new ArgumentNullException("container");
		}
		foreach (AppViewModel value3 in APP_LOOK_UP.Values)
		{
			value3.UserRatingInternal = 0;
		}
		foreach (ServerService.AppRating item in container.AppRating)
		{
			if (APP_LOOK_UP.TryGetValue(item.AppId, out var value))
			{
				value.UserRatingInternal = item.Value;
			}
		}
		foreach (ServerService.AppStat item2 in container.AppStat)
		{
			_ = item2;
		}
		foreach (AppExeViewModel value4 in APP_EXE_LOOK_UP.Values)
		{
			value4.TotalUserExecutions = 0;
			value4.TotalUserTime = 0.0;
		}
		foreach (AppExeStat item3 in container.AppExeStat)
		{
			if (APP_EXE_LOOK_UP.TryGetValue(item3.AppExeId, out var value2))
			{
				value2.TotalUserExecutions = item3.TotalExecutions;
				value2.TotalUserTime = item3.TotalSpan;
			}
		}
	}

	private void ProcessContainer(NewsEntityContainer container)
	{
		if (container == null)
		{
			throw new ArgumentNullException("container");
		}
		container.News.Select((News entity) => CreateViewModel(entity)).ToList();
		container.Feeds.Select((Feed entity) => CreateViewModel(entity)).ToList();
	}

	private void ProcessContainer(ClientOrderingData container)
	{
		if (container == null)
		{
			throw new ArgumentNullException("container");
		}
		PAYMENT_METHOD_STORE.Clear();
		PAYMENT_METHOD_LOOKUP.Clear();
		PRODUCT_STORE.Clear();
		PRODUCT_LOOKUP.Clear();
		PRODUCT_GROUP_STORE.Clear();
		PRODUCT_GROUP_LOOKUP.Clear();
		container.PaymentMethods.Select((ClientOrderingPaymentMethod entity) => CreateViewModel(entity)).ToList();
		container.Products.Select((ClientOrderingProduct entity) => CreateViewModel(entity)).ToList();
		container.ProductGroups.Select((ClientOrderingProductGroup entity) => CreateViewModel(entity)).ToList();
	}

	private AppEnterpriseDisplayViewModel CreateViewModel(AppEnterprise entity)
	{
		if (entity == null)
		{
			throw new ArgumentNullException("entity");
		}
		AppEnterpriseDisplayViewModel viewModel = APP_ENTERPRISE_LOOK_UP.GetOrAdd(entity.Id, GetExportedValue<AppEnterpriseDisplayViewModel>());
		viewModel.Id = entity.Id;
		viewModel.Name = entity.Name;
		BindingOperations.AccessCollection(APP_ENTERPRISE_STORE, delegate
		{
			if (!APP_ENTERPRISE_STORE.Contains(viewModel))
			{
				APP_ENTERPRISE_STORE.Add(viewModel);
			}
		}, writeAccess: true);
		return viewModel;
	}

	private CategoryDisplayViewModel CreateViewModel(AppCategory entity)
	{
		if (entity == null)
		{
			throw new ArgumentNullException("entity");
		}
		CategoryDisplayViewModel viewModel = CATEGORY_LOOK_UP.GetOrAdd(entity.Id, GetExportedValue<CategoryDisplayViewModel>());
		viewModel.Name = entity.Name;
		viewModel.ParentId = entity.ParentId;
		viewModel.CategoryId = entity.Id;
		BindingOperations.AccessCollection(APP_CATEGORY_STORE, delegate
		{
			if (!APP_CATEGORY_STORE.Contains(viewModel))
			{
				APP_CATEGORY_STORE.Add(viewModel);
			}
		}, writeAccess: true);
		return viewModel;
	}

	private AppExeViewModel CreateViewModel(AppExe entity)
	{
		if (entity == null)
		{
			throw new ArgumentNullException("entity");
		}
		AppExeViewModel viewModel = APP_EXE_LOOK_UP.GetOrAdd(entity.Id, GetExportedValue<AppExeViewModel>());
		if (APP_LOOK_UP.TryGetValue(entity.AppId, out var value))
		{
			viewModel.App = value;
		}
		viewModel.Caption = entity.Caption;
		viewModel.ExecutablePath = entity.ExecutablePath;
		viewModel.IsAccessible = entity.Accessible;
		viewModel.ExeId = entity.Id;
		viewModel.AppId = entity.AppId;
		viewModel.Modes = entity.Modes;
		viewModel.DisplayOrder = entity.DisplayOrder;
		viewModel.AutoStart = entity.Options.HasFlag(ExecutableOptionType.AutoLaunch);
		viewModel.IsQuickLaunch = entity.Options.HasFlag(ExecutableOptionType.QuickLaunch);
		viewModel.IsIgnoringConcurrentExecutionLimit = entity.Options.HasFlag(ExecutableOptionType.IgnoreConcurrentExecutionLimit);
		viewModel.HasDeploymentProfiles = entity.Deployments.Any();
		viewModel.PersonalFiles = null;
		viewModel.ImageData = null;
		viewModel.ExecutableFileExists = null;
		BindingOperations.AccessCollection(APP_EXE_STORE, delegate
		{
			if (!APP_EXE_STORE.Contains(viewModel))
			{
				APP_EXE_STORE.Add(viewModel);
			}
		}, writeAccess: true);
		return viewModel;
	}

	private AppViewModel CreateViewModel(App entity)
	{
		if (entity == null)
		{
			throw new ArgumentNullException("entity");
		}
		AppViewModel viewModel = APP_LOOK_UP.GetOrAdd(entity.Id, GetExportedValue<AppViewModel>());
		viewModel.AppId = entity.Id;
		viewModel.Title = entity.Title;
		viewModel.Description = entity.Description;
		viewModel.CategoryId = entity.AppCategoryId;
		viewModel.AddDate = entity.CreatedTime;
		viewModel.ReleaseDate = entity.ReleaseDate;
		viewModel.PublisherId = entity.PublisherId;
		viewModel.DeveloperId = entity.DeveloperId;
		viewModel.DefaultExecutableId = entity.DefaultExecutableId;
		viewModel.AgeRating = entity.AgeRating;
		viewModel.AgeRatingType = entity.AgeRatingType;
		viewModel.ImageData = null;
		viewModel.AppLinks = null;
		BindingOperations.AccessCollection(APP_STORE, delegate
		{
			if (!APP_STORE.Contains(viewModel))
			{
				APP_STORE.Add(viewModel);
			}
		}, writeAccess: true);
		return viewModel;
	}

	private NewsViewModel CreateViewModel(News entity)
	{
		if (entity == null)
		{
			throw new ArgumentException("entity");
		}
		NewsViewModel viewModel = NEWS_LOOKUP.GetOrAdd(entity.Id, GetExportedValue<NewsViewModel>()) as NewsViewModel;
		viewModel.Id = entity.Id;
		viewModel.Title = entity.Title;
		viewModel.CreatedTime = entity.CreatedTime;
		viewModel.Data = entity.Data;
		viewModel.EndDate = entity.EndDate;
		viewModel.StartDate = entity.StartDate;
		viewModel.Url = entity.Url;
		viewModel.MediaUrl = entity.MediaUrl;
		viewModel.ImageData = null;
		viewModel.MediaTypeTask = null;
		viewModel.ResetCommands();
		BindingOperations.AccessCollection(NEWS_STORE, delegate
		{
			if (!NEWS_STORE.Contains(viewModel))
			{
				NEWS_STORE.Add(viewModel);
			}
		}, writeAccess: true);
		return viewModel;
	}

	private FeedSourceViewModel CreateViewModel(Feed entity)
	{
		if (entity == null)
		{
			throw new ArgumentException("entity");
		}
		FeedSourceViewModel viewModel = FEEDS_LOOKUP.GetOrAdd(entity.Id, GetExportedValue<FeedSourceViewModel>()) as FeedSourceViewModel;
		viewModel.MaxResults = entity.Maximum;
		viewModel.Title = entity.Title;
		viewModel.Url = entity.Url;
		viewModel.HasEnumerated = false;
		BindingOperations.AccessCollection(NEWS_STORE, delegate
		{
			if (!NEWS_STORE.Contains(viewModel))
			{
				NEWS_STORE.Add(viewModel);
			}
		}, writeAccess: true);
		return viewModel;
	}

	private ProductViewModel CreateViewModel(ClientOrderingProduct entity)
	{
		if (entity == null)
		{
			throw new ArgumentException("entity");
		}
		ProductViewModel viewModel = PRODUCT_LOOKUP.GetOrAdd(entity.ProductId, GetExportedValue<ProductViewModel>());
		viewModel.ProductId = entity.ProductId;
		viewModel.ProductGroupId = entity.ProductGroupId;
		viewModel.Name = entity.Name;
		viewModel.Description = entity.Description;
		viewModel.Award = entity.Award;
		viewModel.Price = entity.Price;
		viewModel.PointsPrice = entity.PointsPrice;
		viewModel.PurchaseOptions = entity.PurchaseOptions;
		viewModel.Type = entity.Type;
		viewModel.HasImage = entity.HasImage;
		viewModel.ImageData = null;
		viewModel.AddDate = entity.CreatedTime;
		BindingOperations.AccessCollection(PRODUCT_STORE, delegate
		{
			if (!PRODUCT_STORE.Contains(viewModel))
			{
				PRODUCT_STORE.Add(viewModel);
			}
		}, writeAccess: true);
		return viewModel;
	}

	private ProductGroupViewModel CreateViewModel(ClientOrderingProductGroup entity)
	{
		if (entity == null)
		{
			throw new ArgumentException("entity");
		}
		ProductGroupViewModel viewModel = PRODUCT_GROUP_LOOKUP.GetOrAdd(entity.ProductGroupId, GetExportedValue<ProductGroupViewModel>());
		viewModel.ProductGroupId = entity.ProductGroupId;
		viewModel.Name = entity.Name;
		BindingOperations.AccessCollection(PRODUCT_GROUP_STORE, delegate
		{
			if (!PRODUCT_GROUP_STORE.Contains(viewModel))
			{
				PRODUCT_GROUP_STORE.Add(viewModel);
			}
		}, writeAccess: true);
		return viewModel;
	}

	private PaymentMethodViewModel CreateViewModel(ClientOrderingPaymentMethod entity)
	{
		if (entity == null)
		{
			throw new ArgumentException("entity");
		}
		PaymentMethodViewModel viewModel = PAYMENT_METHOD_LOOKUP.GetOrAdd(entity.PaymentMethodId, GetExportedValue<PaymentMethodViewModel>());
		viewModel.PaymentMethodId = entity.PaymentMethodId;
		viewModel.Name = entity.Name;
		viewModel.Description = entity.Description;
		viewModel.DisplayOrder = entity.DisplayOrder;
		viewModel.LocalizedName = ((entity.PaymentMethodId < 0) ? GetLocalizedObject<string>((PaymentMethodType)entity.PaymentMethodId) : entity.Name);
		BindingOperations.AccessCollection(PAYMENT_METHOD_STORE, delegate
		{
			if (!PAYMENT_METHOD_STORE.Contains(viewModel))
			{
				PAYMENT_METHOD_STORE.Add(viewModel);
			}
		}, writeAccess: true);
		return viewModel;
	}

	private void OnShellWindowShow()
	{
		Window shellWindow = ShellWindow;
		if (shellWindow == null)
		{
			return;
		}
		Application.Dispatcher.Invoke(delegate
		{
			shellWindow.ShowActivated = true;
			shellWindow.Show();
			shellWindow.Activate();
		});
		OnShellWindowFitIn();
		if (!IsUserLoggedIn || IsInGracePeriod || IsUserLocked)
		{
			OnMinimizeNonClientWindows();
		}
		ManagerLoginViewModel managerViewModel = ManagerViewModel;
		if (managerViewModel != null && managerViewModel.IsLoaded)
		{
			Application.Dispatcher.Invoke(() => ManagerViewModel.Window.Activate());
		}
	}

	private void OnShellWindowClose()
	{
		Window shellWindow = ShellWindow;
		if (shellWindow == null)
		{
			return;
		}
		shellWindow.Closing -= OnShellWindowClosing;
		System.Windows.Application application = Application;
		if (application == null)
		{
			return;
		}
		try
		{
			application.Dispatcher.Invoke(delegate
			{
				shellWindow.Close();
			});
		}
		catch (TimeoutException ex)
		{
			TraceWrite(ex, "OnShellWindowClose", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Shell.cs", 128);
		}
		catch (InvalidOperationException ex2)
		{
			TraceWrite(ex2, "OnShellWindowClose", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Shell.cs", 133);
		}
	}

	private void OnShellWindowHide()
	{
		Window shellWindow = ShellWindow;
		if (shellWindow == null)
		{
			return;
		}
		System.Windows.Application application = Application;
		if (application == null)
		{
			return;
		}
		try
		{
			application.Dispatcher.Invoke(delegate
			{
				shellWindow.Hide();
			});
		}
		catch (TimeoutException ex)
		{
			TraceWrite(ex, "OnShellWindowHide", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Shell.cs", 161);
		}
		catch (InvalidOperationException ex2)
		{
			TraceWrite(ex2, "OnShellWindowHide", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Shell.cs", 166);
		}
	}

	private void OnShellWindowFitIn(bool ignoreLoggedInUser = false)
	{
		if (Monitor.TryEnter(WINDOW_POSITION_LOCK, WINDOW_POSTION_LOCK_WAIT_SPAN))
		{
			try
			{
				IntPtr shellWindowHandle = ShellWindowHandle;
				if (!(shellWindowHandle == IntPtr.Zero))
				{
					if (IsUserLoggedIn && !ignoreLoggedInUser)
					{
						Rect workingAreaRect = GetWorkingAreaRect();
						User32.SetWindowPos(shellWindowHandle, HWND.HWND_BOTTOM, (int)workingAreaRect.X, (int)workingAreaRect.Y, (int)workingAreaRect.Width, (int)workingAreaRect.Height, (SWP)16400u);
					}
					else
					{
						Rect fullScreenArea = GetFullScreenArea();
						IntPtr intPtr = User32.FindWindow("Shell_TrayWnd");
						if (intPtr != IntPtr.Zero)
						{
							User32.SetWindowPos(intPtr, HWND.HWND_BOTTOM, 0, 0, 0, 0, (SWP)16403u);
						}
						User32.SetWindowPos(shellWindowHandle, HWND.HWND_TOPMOST, (int)fullScreenArea.X, (int)fullScreenArea.Y, (int)fullScreenArea.Width, (int)fullScreenArea.Height, SWP.SWP_ASYNCWINDOWPOS);
						User32.SetForegroundWindowEx(shellWindowHandle);
					}
				}
				return;
			}
			catch (Exception ex)
			{
				LogAddError("OnShellWindowFitIn", ex, LogCategories.UserInterface);
				return;
			}
			finally
			{
				Monitor.Exit(WINDOW_POSITION_LOCK);
			}
		}
		Trace.WriteLine("OnShellWindowFitIn timed out while waiting to acquire WINDOW_POSITION_LOCK.");
	}

	private void OnMinimizeNonClientWindows()
	{
		try
		{
			foreach (IntPtr item in from hwnd in WindowEnumerator.ListHandles()
				where !IsClientWindow(hwnd) && !User32.IsIconic(hwnd) && Shell.IsAppWindow(hwnd)
				select hwnd)
			{
				User32.ShowWindowAsync(item, Win32API.Headers.WinUser.Enumerations.SW.SW_MINIMIZE);
			}
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "OnMinimizeNonClientWindows", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Shell.cs", 258);
		}
	}

	public bool IsClientWindow(IntPtr hWnd)
	{
		if (hWnd == IntPtr.Zero)
		{
			return false;
		}
		User32.GetWindowThreadProcessId(hWnd, out var lpdwProcessId);
		return lpdwProcessId == ClientProcessId;
	}

	public static Rect GetWorkingAreaRect(bool multiScreen = false)
	{
		int num = 0;
		Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
		if (multiScreen)
		{
			Screen[] allScreens = Screen.AllScreens;
			foreach (Screen screen in allScreens)
			{
				num += screen.Bounds.Width;
			}
		}
		else
		{
			num = workingArea.Width;
		}
		int height = workingArea.Height;
		return new Rect(workingArea.Location.X, workingArea.Location.Y, num, height);
	}

	public static Rect GetFullScreenArea(bool multiScreen = false)
	{
		int num = 0;
		Screen primaryScreen = Screen.PrimaryScreen;
		if (multiScreen)
		{
			Screen[] allScreens = Screen.AllScreens;
			foreach (Screen screen in allScreens)
			{
				num += screen.Bounds.Width;
			}
		}
		else
		{
			num = primaryScreen.Bounds.Width;
		}
		int height = primaryScreen.Bounds.Height;
		return new Rect(0.0, 0.0, num, height);
	}

	public SkinConfig JsonDeserializeConfig(string fileName)
	{
		return JsonDeserializeConfig<SkinConfig>(fileName);
	}

	public T JsonDeserializeConfig<T>(string fileName) where T : SkinConfig
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			throw new ArgumentNullException("fileName");
		}
		if (!File.Exists(fileName))
		{
			throw new FileNotFoundException("File not found.", fileName);
		}
		using FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
		using StreamReader reader = new StreamReader(stream);
		using JsonTextReader reader2 = new JsonTextReader(reader);
		return new JsonSerializer
		{
			MissingMemberHandling = MissingMemberHandling.Ignore,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate
		}.Deserialize<T>(reader2);
	}

	private void OnShellWindowClosing(object sender, CancelEventArgs e)
	{
		e.Cancel = true;
	}

	private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
	{
		TraceWrite($"Windows user preferences change event of catgeroy : {e.Category} occured.", (string)null, "OnUserPreferenceChanged", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Shell.cs", 359);
		if (e.Category == UserPreferenceCategory.Desktop)
		{
			OnShellWindowFitIn();
		}
	}

	private void OnDisplaySettingsChanged(object sender, EventArgs e)
	{
		TraceWrite("Windows display settings changed.", (string)null, "OnDisplaySettingsChanged", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Shell.cs", 368);
		OnShellWindowFitIn();
	}

	private void OnShellWindowSourceInitialized(object sender, EventArgs e)
	{
		if (!(sender is Window window))
		{
			return;
		}
		IntPtr intPtr = (ShellWindowHandle = new WindowInteropHelper(window).Handle);
		ClientSettings clientSettings = Settings;
		if (clientSettings != null && clientSettings.StickyShell)
		{
			try
			{
				WS wS = (WS)User32.GetWindowLong(intPtr, GWL.GWL_STYLE);
				if ((wS.HasFlag(WS.WS_MAXIMIZEBOX) || wS.HasFlag(WS.WS_GROUP)) && User32.SetWindowLongPtr(intPtr, -16, (IntPtr)(uint)((int)wS & -65537 & -131073)) == IntPtr.Zero)
				{
					throw new Win32Exception();
				}
			}
			catch (Win32Exception ex)
			{
				if (TraceEnabled)
				{
					TraceWrite(ex, "OnShellWindowSourceInitialized", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Shell.cs", 404);
				}
			}
		}
		HwndSource.FromHwnd(intPtr).AddHook(ShellWindowProc);
	}

	private IntPtr ShellWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (IsInMaintenance)
		{
			return IntPtr.Zero;
		}
		try
		{
			switch (msg)
			{
			case 70:
			{
				WINDOWPOS structure = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
				if (!IsShellWindowInBackground)
				{
					IntPtr hwndInsertAfter = (IntPtr)(-1);
					if ((int)structure.hwndInsertAfter > 1 && IsClientWindow(hwnd))
					{
						hwndInsertAfter = hwnd;
					}
					structure.hwndInsertAfter = hwndInsertAfter;
					Marshal.StructureToPtr(structure, lParam, fDeleteOld: true);
				}
				else if (StickyShell && structure.x == 0 && structure.y > 0)
				{
					int cx = (int)(ShellWindow?.ActualWidth ?? 0.0);
					int cy = (int)(ShellWindow?.ActualHeight ?? 0.0);
					structure.x = 0;
					structure.y = 0;
					structure.cx = cx;
					structure.cy = cy;
					Marshal.StructureToPtr(structure, lParam, fDeleteOld: true);
					User32.ShowWindow(ShellWindowHandle, Win32API.Headers.WinUser.Enumerations.SW.SW_RESTORE);
				}
				break;
			}
			case 274:
				if (wParam.ToInt32() == 61472)
				{
					if (IsUserLoggedIn)
					{
						handled = StickyShell;
					}
					else
					{
						handled = true;
					}
				}
				break;
			}
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "ShellWindowProc", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_Shell.cs", 491);
		}
		return IntPtr.Zero;
	}

	public Task<LoginResult> LoginAsync(string username, string password)
	{
		return LoginAsync(username, password, allowEmptyPasswords: true);
	}

	public Task<LoginResult> LoginAsync(string username, string password, bool allowEmptyPasswords = true)
	{
		return System.Threading.Tasks.Task.Run(() => Login(username, password, allowEmptyPasswords));
	}

	public LoginResult Login(string username, string password)
	{
		return Login(username, password, allowEmptyPasswords: true);
	}

	public LoginResult Login(string username, string password, bool allowEmptyPasswords = true)
	{
		return Login(new Dictionary<string, object>
		{
			{ "USERNAME", username },
			{ "PASSWORD", password }
		}, allowEmptyPasswords);
	}

	public LoginResult Login(Dictionary<string, object> authHeaders, bool allowEmptyPasswords)
	{
		if (authHeaders == null)
		{
			throw new ArgumentNullException("authHeaders", "Authentication headers dictionary may not be null");
		}
		if (!authHeaders.ContainsKey("USERNAME") || string.IsNullOrWhiteSpace(authHeaders["USERNAME"].ToString()))
		{
			throw new ArgumentNullException("UserName", "Username may not be null or empty");
		}
		if (!allowEmptyPasswords && (!authHeaders.ContainsKey("PASSWORD") || string.IsNullOrWhiteSpace(authHeaders["PASSWORD"].ToString())))
		{
			throw new ArgumentNullException("Password", "Password may not be null or empty");
		}
		lock (LOGIN_OP_LOCK)
		{
			InitShutdownTimer();
			if (IsUserLoggedIn)
			{
				return LoginResult.AlreadyLoggedIn;
			}
			if (IsUserLoggingIn)
			{
				return LoginResult.LoginInProgress;
			}
			try
			{
				OnUserSwitchState(LoginState.LoggingIn);
				ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.Login, authHeaders);
				syncOperation.StartEx();
				syncOperation.WaitCompleteEx();
				AuthResult authResult = syncOperation.Data[0] as AuthResult;
				IUserProfile userProfile = syncOperation.Data[1] as IUserProfile;
				LoginResult result = authResult.Result;
				if (result == LoginResult.Sucess)
				{
					OnUserLoggedIn(authResult.Identity, userProfile, authResult.RequiredInfo);
				}
				else
				{
					OnUserSwitchState(LoginState.LoginFailed, result);
				}
				return authResult.Result;
			}
			catch (Exception ex)
			{
				OnUserSwitchState(LoginState.LoginFailed, LoginResult.Failed);
				Log.AddError("Login procedure failed", ex, LogCategories.User);
				return LoginResult.Failed;
			}
		}
	}

	public System.Threading.Tasks.Task LogoutAsync()
	{
		return System.Threading.Tasks.Task.Run(delegate
		{
			Logout();
		});
	}

	public void Logout()
	{
		OnUserLogout();
	}

	internal void OnUserLogin(IUserIdentity identity, IUserProfile profile, UserInfoTypes requiredInfo = UserInfoTypes.None)
	{
		if (identity == null)
		{
			throw new ArgumentNullException("identity");
		}
		if (profile == null)
		{
			throw new ArgumentNullException("profile");
		}
		lock (LOGIN_OP_LOCK)
		{
			if (!IsInitialized)
			{
				StoredIdentity = identity;
				StoredRequestedInfo = requiredInfo;
				StoredUserProfile = profile;
			}
			else
			{
				OnUserSwitchState(LoginState.LoggingIn);
				OnUserLoggedIn(identity, profile, requiredInfo);
			}
		}
	}

	private void OnUserLoggedIn(IUserIdentity identity, IUserProfile userProfile, UserInfoTypes requiredInfo = UserInfoTypes.None)
	{
		TraceWrite("Executing OnUserLoggedIn", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 315);
		CurrentUserIdentity = identity ?? throw new ArgumentNullException("identity");
		if (userProfile == null)
		{
			throw new ArgumentNullException("userProfile");
		}
		TraceWrite("Entering LOGIN_OP_LOCK", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 322);
		lock (LOGIN_OP_LOCK)
		{
			TraceWrite("Entered LOGIN_OP_LOCK", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 325);
			try
			{
				bool flag = false;
				PagedList<UserAgreement> result = UserAgreementGetAsync(new UserAgreementsFilter
				{
					Limit = 1000000,
					IsEnabled = true
				}).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
				List<UserAgreementState> result2 = UserAgreementStatesGetAsync(userProfile.Id).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
				foreach (UserAgreement item in result.Data)
				{
					if (!result2.Where((UserAgreementState a) => a.UserAgreementId == item.Id).Any())
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					List<UserAgreement> userAgreements = result.Data.OrderBy((UserAgreement a) => a.DisplayOrder).ToList();
					RaiseUserAgreementsLoaded(userProfile.Id, flag, userAgreements, result2);
				}
			}
			catch (Exception ex)
			{
				TraceWrite(ex, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 356);
			}
			CurrentUser = userProfile;
			TraceWrite("Executing OnUserSwitchState", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 363);
			OnUserSwitchState(LoginState.LoggedIn, LoginResult.Sucess, requiredInfo);
			TraceWrite("Executing OnResetGroupConfiguration", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 367);
			OnResetGroupConfiguration();
			TraceWrite("Executing SetUserEnvironmentArgs", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 371);
			SetUserEnvironmentArgs();
			TraceWrite("Executing OnProcessLoggedIn", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 375);
			OnProcessLoggedIn();
			TraceWrite("Executing RaiseLoginPropertiesChanged", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 379);
			RaiseLoginPropertiesChanged();
			try
			{
				TraceWrite("Executing InitDataAsync", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 384);
				InitDataAsync().ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
				TraceWrite("Executed InitDataAsync", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 386);
				ShellViewModel.SelectFirstOrDefault();
			}
			catch (Exception ex2)
			{
				TraceWrite(ex2, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 391);
			}
			TraceWrite("Executing OnUserSwitchState", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 395);
			OnUserSwitchState(LoginState.LoginCompleted, LoginResult.Sucess, requiredInfo);
			TraceWrite("Exiting LOGIN_OP_LOCK", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 398);
		}
		TraceWrite("Executing TryShowExplorerWindows", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 402);
		Shell.TryShowExplorerWindows(includeStart: false);
		TraceWrite("Executed TryShowExplorerWindows", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 404);
		if (StickyShell)
		{
			TraceWrite("Executing StickyShell functions", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 409);
			Shell.TryHideDesktop();
			Shell.TryHideShowDesktopButton();
			TraceWrite("Executed StickyShell functions", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 412);
		}
		if (StartMenuDisabled)
		{
			TraceWrite("Executing TryHideStartButton", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 418);
			Shell.TryHideStartButton();
			TraceWrite("Executed TryHideStartButton", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 420);
		}
		else
		{
			TraceWrite("Executing TryShowStartButton", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 424);
			Shell.TryShowStartButton();
			TraceWrite("Executed TryShowStartButton", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 426);
		}
		IsShellWindowInBackground = true;
		TraceWrite("Executing OnShellWindowFitIn", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 431);
		OnShellWindowFitIn();
		TraceWrite("Executed OnShellWindowFitIn", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 433);
		TraceWrite("Executed OnUserLoggedIn", (string)null, "OnUserLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 435);
	}

	private void OnProcessLoggedIn()
	{
		TraceWrite("Executing OnProcessLoggedIn", (string)null, "OnProcessLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 440);
		DeinitShutdownTimer();
		TraceUserProcesses = true;
		try
		{
			RaiseActivityChange(ClientStartupActivity.CreatingUserStorage);
			TraceWrite("Executing CreateUserStorage", (string)null, "OnProcessLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 456);
			CreateUserStorage();
			TraceWrite("Executed CreateUserStorage", (string)null, "OnProcessLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 458);
		}
		catch (CBFSConnectException ex)
		{
			Log.AddError("User storage creation failed.", GetSerializableExcetpion(ex), LogCategories.Configuration);
		}
		catch (Exception ex2)
		{
			Log.AddError("User storage creation failed.", ex2, LogCategories.Configuration);
		}
		if (!IsCurrentUserIsGuest)
		{
			TraceWrite("Executing PUF processing", (string)null, "OnProcessLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 475);
			try
			{
				foreach (KeyValuePair<int, HashSet<int>> item in AppExePersonalFileByActivationGet(SharedLib.PersonalFileActivationType.Login))
				{
					int key = item.Key;
					IEnumerable<int> enumerable = item.Value.Where((int PUF_ID) => !IsMarkedPersonalUserFile(PUF_ID));
					if (enumerable.Count() <= 0)
					{
						continue;
					}
					AppExe appExe = AppExeExecutionGraphGet(key);
					if (appExe == null)
					{
						continue;
					}
					foreach (int PERSONAL_FILE_ID in enumerable)
					{
						PersonalFile personalFile = appExe.PersonalFiles.SingleOrDefault((AppExePersonalFile PUF) => PUF.PersonalFileId == PERSONAL_FILE_ID)?.PersonalFile;
						if (personalFile == null)
						{
							continue;
						}
						App app = appExe.App;
						if (app == null)
						{
							continue;
						}
						string name = personalFile.Name;
						string source = personalFile.Source;
						if (string.IsNullOrWhiteSpace(source))
						{
							throw new ArgumentNullException("SOURCE_PATH");
						}
						string dESTINATION_PATH = Environment.ExpandEnvironmentVariables(source.Replace("%ENTRYPUBLISHER%", app?.Publisher?.Name ?? string.Empty, StringComparison.InvariantCultureIgnoreCase).Replace("%ENTRYDEVELOPER%", app?.Developer?.Name ?? string.Empty, StringComparison.InvariantCultureIgnoreCase).Replace("%ENTRYTITLE%", app?.Title ?? string.Empty, StringComparison.InvariantCultureIgnoreCase));
						string fILE_FILTER = BuildFilter(personalFile.IncludeFiles, personalFile.ExcludeFiles);
						string dIRECTORY_FILTER = BuildFilter(personalFile.IncludeDirectories, personalFile.ExcludeDirectories);
						bool iNCLUDE_SUB_DIRECTORIES = personalFile.Options.HasFlag(PersonalUserFileOptionType.IncludeSubDirectories);
						bool iS_REGISTRY = personalFile.Type == SharedLib.PersonalUserFileType.Registry;
						try
						{
							if (personalFile.Options.HasFlag(PersonalUserFileOptionType.CleanUp))
							{
								CleanPersonalFile(dESTINATION_PATH, fILE_FILTER, dIRECTORY_FILTER, iNCLUDE_SUB_DIRECTORIES, name, iS_REGISTRY);
							}
							DeployPersonalFile(PERSONAL_FILE_ID, dESTINATION_PATH, name, iS_REGISTRY);
						}
						catch (Exception ex3)
						{
							string informationMessage = "Failed processing personal user file " + name + ".";
							Log.AddError(informationMessage, ex3, LogCategories.Operation);
						}
						finally
						{
							MarkPersonalUserFile(PERSONAL_FILE_ID, key);
						}
					}
				}
			}
			catch (Exception ex4)
			{
				TraceWrite(ex4, "OnProcessLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 551);
			}
			TraceWrite("Executed PUF processing", (string)null, "OnProcessLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 553);
		}
		RaiseActivityChange(ClientStartupActivity.ProcessingTasks);
		TraceWrite("Executing ProcessTasks (login)", (string)null, "OnProcessLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 563);
		ProcessTasks(ActivationType.Login);
		TraceWrite("Executed ProcessTasks (login)", (string)null, "OnProcessLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 565);
		TraceWrite("Executed OnProcessLoggedIn", (string)null, "OnProcessLoggedIn", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 568);
	}

	internal void OnUserLogout(bool userInitiated = true, UserLogoutFlags logoutFlags = UserLogoutFlags.None)
	{
		lock (LOGIN_OP_LOCK)
		{
			StoredIdentity = null;
			StoredRequestedInfo = UserInfoTypes.None;
			StoredUserProfile = null;
			try
			{
				if (!IsUserLoggedIn || IsUserLoggedOut)
				{
					return;
				}
				OnUserSwitchState(LoginState.LoggingOut);
				LogoutAction logoutAction = Settings.LogoutAction;
				OnUserLoggingOut(logoutAction, logoutFlags);
				try
				{
					if (userInitiated)
					{
						ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.Logout);
						syncOperation.StartEx();
						syncOperation.WaitCompleteEx();
					}
				}
				catch (Exception ex)
				{
					LogAddError("Could not logout from server.", ex, LogCategories.Operation);
				}
				OnUserLoggedOut(logoutAction, logoutFlags);
			}
			catch (Exception ex2)
			{
				OnUserSwitchState(LoginState.LoggedOut);
				LogAddError("Logout procedure failed.", ex2, LogCategories.User);
			}
		}
	}

	private void OnUserLoggingOut(LogoutAction logoutAction, UserLogoutFlags logoutFlags = UserLogoutFlags.None)
	{
		TraceWrite("Executing OnUserLoggingOut", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 623);
		IsShellWindowInBackground = false;
		try
		{
			if (ShellViewModel != null)
			{
				TraceWrite("Resetting view models", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 632);
				ShellViewModel.ShopViewModel.Filter = null;
				ShellViewModel.ShopViewModel.CurrentProductGroup = null;
				ShellViewModel.ShopViewModel.Order.SetPaymentMethod(null);
				ShellViewModel.ShopViewModel.Order.Reset();
				TraceWrite("View models reset completed", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 644);
			}
			else
			{
				TraceWrite("ShellViewModel is null, should had value", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 648);
			}
		}
		catch (Exception ex)
		{
			TraceWrite(ex, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 653);
		}
		try
		{
			TraceWrite("Hiding any open UI dialog", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 658);
			ShellViewModel.DialogService?.HideCurrentDialog();
			TraceWrite("Hidden any open UI dialog", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 661);
		}
		catch (ImportCardinalityMismatchException)
		{
		}
		try
		{
			TraceWrite("Hiding current UI overlay", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 670);
			(ShellWindow as IShellWindow)?.HideCurrentOverlay(cancel: true);
			TraceWrite("Hidden current UI overlay", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 673);
		}
		catch (Exception ex3)
		{
			TraceWrite(ex3, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 677);
		}
		if (!logoutFlags.HasFlag(UserLogoutFlags.SupressLogoutAction))
		{
			TraceWrite("Executing OnMinimizeNonClientWindows", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 683);
			OnMinimizeNonClientWindows();
			TraceWrite("Executed OnMinimizeNonClientWindows", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 685);
		}
		TraceWrite("Executing OnShellWindowFitIn", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 689);
		OnShellWindowFitIn();
		TraceWrite("Executed OnShellWindowFitIn", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 691);
		TraceWrite("Disabling TraceUserProcesses", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 694);
		TraceUserProcesses = false;
		TraceWrite("Disabled TraceUserProcesses", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 696);
		RaiseActivityChange(ClientStartupActivity.ProcessingTasks);
		TraceWrite("Executing ProcessTasks", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 705);
		ProcessTasks(ActivationType.Logout);
		TraceWrite("Executed ProcessTasks", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 707);
		TraceWrite("Executing DestroyContexts", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 714);
		DestroyContexts(release: false, 5000);
		TraceWrite("Executed DestroyContexts", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 716);
		if (!logoutFlags.HasFlag(UserLogoutFlags.SupressLogoutAction) && logoutAction != LogoutAction.NoAction)
		{
			RaiseActivityChange(ClientStartupActivity.DestroyingUserContexts);
			TraceWrite("Executing TerminateUserProcesses", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 724);
			TerminateUserProcesses();
			TraceWrite("Executed TerminateUserProcesses", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 726);
		}
		try
		{
			TraceWrite("Closing client process windows", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 734);
			foreach (WindowInfo item in from info in WindowEnumerator.ListVisibleWindows(ClientProcessId)
				where info.Handle != ShellWindowHandle
				select info)
			{
				try
				{
					if (!item.Close())
					{
						TraceWrite("Could not close process window " + item.Title, (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 747);
					}
					item.PostClose();
				}
				catch (Exception ex4)
				{
					TraceWrite("Could not close client owned open window due to " + ex4.Message, (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 754);
				}
			}
			TraceWrite("Closed client process windows", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 758);
		}
		catch (Exception ex5)
		{
			Log.AddError("Could not list client open windows for closing.", ex5, LogCategories.Generic);
		}
		try
		{
			TOAST_CTS?.Cancel();
			TOAST_CTS = null;
		}
		catch (Exception ex6)
		{
			TraceWrite(ex6, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 775);
		}
		try
		{
			USER_TASK_CTS?.Cancel();
			USER_TASK_CTS = null;
		}
		catch (Exception ex7)
		{
			TraceWrite(ex7, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 788);
		}
		try
		{
			if (!IsCurrentUserIsGuest)
			{
				TraceWrite("Handling personal user files", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 798);
				RaiseActivityChange(ClientStartupActivity.SettingPersonalUserFiles);
				int PERSONAL_FILE_ID;
				foreach (KeyValuePair<int, int> markedPersonalUserFile in GetMarkedPersonalUserFiles())
				{
					try
					{
						int value = markedPersonalUserFile.Value;
						PERSONAL_FILE_ID = markedPersonalUserFile.Key;
						AppExe appExe = AppExeExecutionGraphGet(value);
						if (appExe == null)
						{
							continue;
						}
						PersonalFile personalFile = appExe.PersonalFiles.SingleOrDefault((AppExePersonalFile PUF) => PUF.PersonalFileId == PERSONAL_FILE_ID)?.PersonalFile;
						if (personalFile == null || !personalFile.Options.HasFlag(PersonalUserFileOptionType.Store))
						{
							continue;
						}
						App app = appExe.App;
						if (app == null)
						{
							continue;
						}
						string name = personalFile.Name;
						string source = personalFile.Source;
						if (string.IsNullOrWhiteSpace(source))
						{
							throw new ArgumentNullException("SOURCE_PATH");
						}
						string text = Environment.ExpandEnvironmentVariables(source.Replace("%ENTRYPUBLISHER%", app?.Publisher?.Name ?? string.Empty, StringComparison.InvariantCultureIgnoreCase).Replace("%ENTRYDEVELOPER%", app?.Developer?.Name ?? string.Empty, StringComparison.InvariantCultureIgnoreCase).Replace("%ENTRYTITLE%", app?.Title ?? string.Empty, StringComparison.InvariantCultureIgnoreCase));
						string fileFilter = BuildFilter(personalFile.IncludeFiles, personalFile.ExcludeFiles);
						string directoryFilter = BuildFilter(personalFile.IncludeDirectories, personalFile.ExcludeDirectories);
						bool recursive = personalFile.Options.HasFlag(PersonalUserFileOptionType.IncludeSubDirectories);
						bool flag = personalFile.Type == SharedLib.PersonalUserFileType.Registry;
						int num = personalFile.MaxQuota * 1024 * 1024;
						int compressionLevel = personalFile.CompressionLevel;
						string tempFileName = Path.GetTempFileName();
						using (ZipOutputStream zipOutputStream = new ZipOutputStream(new FileStream(tempFileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None)))
						{
							zipOutputStream.SetLevel(compressionLevel);
							if (!flag)
							{
								if (Directory.Exists(text))
								{
									cyStructure cyStructure = new cyStructure(new cyDirectoryInfo(text));
									cyStructure.FileFilter = fileFilter;
									cyStructure.DirectoryFilter = directoryFilter;
									cyStructure.Get(recursive);
									foreach (IcyFileSystemInfo entry2 in cyStructure.Entries)
									{
										string text2 = ZipEntry.CleanName(entry2.RelativePath);
										if (entry2.IsDirectory && (!text2.EndsWith(Path.DirectorySeparatorChar.ToString()) & !text2.EndsWith('/'.ToString())))
										{
											text2 += "/";
										}
										ZipEntry zipEntry = new ZipEntry(text2)
										{
											IsUnicodeText = true,
											DateTime = entry2.LastWriteTime,
											ExternalFileAttributes = (int)entry2.Attributes,
											Size = (entry2.IsDirectory ? 0 : entry2.Length)
										};
										if (entry2.IsFile)
										{
											try
											{
												using Stream stream = new FileStream(entry2.FullName, FileMode.Open, FileAccess.Read, FileShare.None);
												zipEntry.Size = stream.Length;
												if (zipEntry.Size != entry2.Length)
												{
													Log.AddError($"File {entry2.FullName} sizes did not match {entry2.Length}:{zipEntry.Size}", null, LogCategories.FileSystem);
												}
												zipOutputStream.PutNextEntry(zipEntry);
												StreamUtils.Copy(stream, zipOutputStream, new byte[524288]);
											}
											catch (Exception ex8)
											{
												Log.AddError($"Could not add file {entry2.FullName} while storing user profile {name}.", ex8, LogCategories.FileSystem);
												continue;
											}
										}
										else
										{
											zipOutputStream.PutNextEntry(zipEntry);
										}
										zipOutputStream.CloseEntry();
									}
								}
							}
							else
							{
								CoreRegistryKey coreRegistryKey = new CoreRegistryKey(text);
								if (coreRegistryKey.Exists)
								{
									coreRegistryKey.Read();
									CoreRegistryFile coreRegistryFile = new CoreRegistryFile();
									coreRegistryFile.Keys.Add(coreRegistryKey);
									using (MemoryStream memoryStream = new MemoryStream())
									{
										coreRegistryFile.SaveToStream(memoryStream);
										memoryStream.Seek(0L, SeekOrigin.Begin);
										ZipEntry entry = new ZipEntry(ZipEntry.CleanName(name + ".reg"))
										{
											IsUnicodeText = true,
											DateTime = DateTime.Now,
											Size = memoryStream.Length
										};
										zipOutputStream.PutNextEntry(entry);
										StreamUtils.Copy(memoryStream, zipOutputStream, new byte[524288]);
									}
									zipOutputStream.CloseEntry();
								}
							}
						}
						FileInfo fileInfo = new FileInfo(tempFileName);
						if (num <= 0 || num >= fileInfo.Length)
						{
							try
							{
								using FileStream source2 = fileInfo.OpenRead();
								using Stream stream2 = PersonalFileStreamGet(PERSONAL_FILE_ID, store: true);
								stream2.SetLength(fileInfo.Length);
								StreamUtils.Copy(source2, stream2, new byte[524288]);
							}
							catch (Exception ex9)
							{
								Log.AddError($"Could not copy personal user file to destination {name}.", ex9, LogCategories.FileSystem);
							}
							finally
							{
								try
								{
									fileInfo.Delete();
								}
								catch (Exception ex10)
								{
									Log.AddError($"Could not delete temporary profile file {tempFileName}.", ex10, LogCategories.FileSystem);
								}
							}
						}
						else
						{
							Log.AddInformation($"Personal User File {name} exceeded maximum quota.", LogCategories.FileSystem);
						}
					}
					catch (Exception ex11)
					{
						Log.AddError("Could not set personal user file.", ex11, LogCategories.Operation);
					}
				}
				TraceWrite("Handled personal user files", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 1076);
			}
		}
		catch (Exception ex12)
		{
			Log.AddError("Failed setting personal user files.", ex12, LogCategories.Operation);
		}
		try
		{
			if (PersonalDriveConfiguration != null)
			{
				TraceWrite("Personal drive configuration found", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 1091);
				RaiseActivityChange(ClientStartupActivity.DestroyingUserStorage);
				TraceWrite("Executing DeleteUserStorage", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 1095);
				DeleteUserStorage();
				TraceWrite("Executed DeleteUserStorage", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 1097);
			}
		}
		catch (Exception ex13)
		{
			Log.AddError("User storage deletion failed.", ex13, LogCategories.FileSystem);
		}
		TraceWrite("Executing ClearMarkedPersonalUserFiles & ClearMarkedDeploymentProfiles", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 1107);
		ClearMarkedDeploymentProfiles();
		ClearMarkedPersonalUserFiles();
		TraceWrite("Executed ClearMarkedPersonalUserFiles & ClearMarkedDeploymentProfiles", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 1110);
		TraceWrite("Executed OnUserLoggingOut", (string)null, "OnUserLoggingOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 1113);
	}

	private void OnUserLoggedOut(LogoutAction logoutAction, UserLogoutFlags logoutFlags)
	{
		lock (LOGIN_OP_LOCK)
		{
			TraceWrite("Executing OnUserLoggedOut", (string)null, "OnUserLoggedOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 1120);
			CurrentUser = null;
			if (!logoutFlags.HasFlag(UserLogoutFlags.SupressLogoutAction))
			{
				RaiseActivityChange(ClientStartupActivity.ExecutingLogoutAction);
				switch (logoutAction)
				{
				case LogoutAction.LogOff:
					SetPowerState(PowerStates.Logoff);
					break;
				case LogoutAction.Reboot:
					SetPowerState(PowerStates.Reboot);
					break;
				case LogoutAction.StandBy:
					SetPowerState(PowerStates.Suspend);
					break;
				case LogoutAction.TurnOff:
					SetPowerState(PowerStates.Shutdown);
					break;
				}
			}
			OnUserSwitchState(LoginState.LoggedOut);
			OnResetGroupConfiguration();
			RaiseLoginPropertiesChanged();
			SetUserEnvironmentArgs();
			USER_TIME_NOTIFIED = false;
			PreferedUILanguage = null;
			if (logoutAction != 0 && (uint)(logoutAction - 2) > 2u)
			{
				InitShutdownTimer();
			}
			TraceWrite("Executed OnUserLoggedOut", (string)null, "OnUserLoggedOut", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 1183);
		}
	}

	public System.Threading.Tasks.Task EnableMaintenanceAsync()
	{
		return System.Threading.Tasks.Task.Run(delegate
		{
			EnableMaintenance();
		});
	}

	public System.Threading.Tasks.Task DisableMaintenanceAsync()
	{
		return System.Threading.Tasks.Task.Run(delegate
		{
			DisableMaintenance();
		});
	}

	public void EnableMaintenance()
	{
		try
		{
			Monitor.Enter(MAINTENANCE_OP_LOCK);
			IsInMaintenance = true;
			DeinitShutdownTimer();
			if (IsSecurityEnabled)
			{
				IsSecurityEnabled = false;
			}
			OnShellWindowHide();
			SetIconVisible(isVisible: true);
		}
		catch
		{
			throw;
		}
		finally
		{
			Monitor.Exit(MAINTENANCE_OP_LOCK);
		}
	}

	public void DisableMaintenance()
	{
		try
		{
			Monitor.Enter(MAINTENANCE_OP_LOCK);
			SetIconVisible(isVisible: false);
			if (!IsSecurityEnabled)
			{
				IsSecurityEnabled = true;
			}
			OnShellWindowShow();
			InitShutdownTimer();
			IsInMaintenance = false;
		}
		catch
		{
			throw;
		}
		finally
		{
			Monitor.Exit(MAINTENANCE_OP_LOCK);
		}
	}

	private void SetUserEnvironmentArgs()
	{
		IUserProfile currentUser = CurrentUser;
		if (currentUser != null)
		{
			Environment.SetEnvironmentVariable("CUR_USER", currentUser.UserName);
			if (currentUser.IsAdmin)
			{
				Environment.SetEnvironmentVariable("CUR_USER_TYPE", "Admin");
			}
			else if (currentUser.IsGuest)
			{
				Environment.SetEnvironmentVariable("CUR_USER_TYPE", "Guest");
			}
			else
			{
				Environment.SetEnvironmentVariable("CUR_USER_TYPE", "Normal");
			}
			Environment.SetEnvironmentVariable("CUR_USER_GROUP", currentUser.GroupName ?? string.Empty);
			Environment.SetEnvironmentVariable("CUR_USER_ID", currentUser.Id.ToString());
		}
		else
		{
			Environment.SetEnvironmentVariable("CUR_USER_TYPE", string.Empty);
			Environment.SetEnvironmentVariable("CUR_USER", string.Empty);
			Environment.SetEnvironmentVariable("CUR_USER_GROUP", string.Empty);
			Environment.SetEnvironmentVariable("CUR_USER_ID", string.Empty);
			Environment.SetEnvironmentVariable("USERMINUTESLEFT", string.Empty);
		}
	}

	internal void OnEnterGracePeriod(int gracePeriodTime)
	{
		lock (USER_UI_LOCK_OP_LOCK)
		{
			IsInGracePeriod = true;
			RaiseGracePeriodChange(isInGracePeriod: true, gracePeriodTime);
			OnShellWindowFitIn(ignoreLoggedInUser: true);
			OnMinimizeNonClientWindows();
		}
	}

	internal void OnExitGracePeriod()
	{
		lock (USER_UI_LOCK_OP_LOCK)
		{
			if (IsInGracePeriod)
			{
				IsInGracePeriod = false;
				RaiseGracePeriodChange(isInGracePeriod: false);
				OnShellWindowFitIn();
			}
			if (!IsUserLocked)
			{
				OnShellWindowFitIn();
			}
		}
	}

	internal void OnEnterUserLock()
	{
		lock (USER_UI_LOCK_OP_LOCK)
		{
			IsUserLocked = true;
			RaiseUserLockChange(IsUserLocked);
			OnShellWindowFitIn(ignoreLoggedInUser: true);
			OnMinimizeNonClientWindows();
		}
	}

	internal void OnExitUserLock()
	{
		lock (USER_UI_LOCK_OP_LOCK)
		{
			IsUserLocked = false;
			RaiseUserLockChange(IsUserLocked);
			OnShellWindowFitIn();
		}
	}

	public System.Threading.Tasks.Task SetUserPasswordAsync(string password)
	{
		return System.Threading.Tasks.Task.Run(delegate
		{
			SetUserPassword(password);
		});
	}

	public void SetUserPassword(string newPassword)
	{
		if (string.IsNullOrWhiteSpace(newPassword))
		{
			throw new ArgumentNullException("newPassword");
		}
		ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.SetUserPassword, newPassword);
		syncOperation.StartEx();
		syncOperation.WaitCompleteEx();
		RaiseUserPasswordChange(newPassword);
	}

	public System.Threading.Tasks.Task ChangeUserPasswordAsync(string oldPassword, string newPassword)
	{
		return System.Threading.Tasks.Task.Run(delegate
		{
			ChangeUserPassword(oldPassword, newPassword);
		});
	}

	public void ChangeUserPassword(string oldPassword, string newPassword)
	{
		if (string.IsNullOrWhiteSpace(oldPassword))
		{
			throw new ArgumentNullException("oldPassword");
		}
		if (string.IsNullOrWhiteSpace(newPassword))
		{
			throw new ArgumentNullException("newPassword");
		}
		ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.ChangeUserPassword, oldPassword, newPassword);
		syncOperation.StartEx();
		syncOperation.WaitCompleteEx();
		RaiseUserPasswordChange(newPassword);
	}

	public System.Threading.Tasks.Task SetUserInfoAsync(IUserProfile profile)
	{
		return System.Threading.Tasks.Task.Run(delegate
		{
			SetUserInfo(profile);
		});
	}

	public void SetUserInfo(IUserProfile profile)
	{
		if (profile == null)
		{
			throw new ArgumentNullException("profile");
		}
		ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.SetUserProfile, profile);
		syncOperation.StartEx();
		syncOperation.WaitCompleteEx();
		IUserProfile currentUser = CurrentUser;
		RaiseUserProfileChange(profile, currentUser);
	}

	public async Task<IUserProfile> UserProfileGetAsync()
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.GetUserProfile);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.Data[0] as IUserProfile;
	}

	public async Task<UsageSessionInfo> UsageSessionInfoGetActiveAsync()
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.GetUsageSessionInfo);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<UsageSessionInfo>();
	}

	public async Task<UserBalance> UserBalanceGetAsync()
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.UserBalanceGet);
		await op.StartAsync();
		await op.WaitCompleteAsync();
		return op.GetProtoResult<UserBalance>();
	}

	public void AppStatSet(int appExeId, DateTime lastRun, double span)
	{
		try
		{
			if (!IsUserLoggedOut && Dispatcher.IsValid)
			{
				int? num = CurrentUser?.Id;
				if (num.HasValue)
				{
					ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.SetApplicationStat, appExeId, num, span, lastRun);
					syncOperation.StartEx();
					syncOperation.WaitCompleteEx();
				}
			}
		}
		catch (AccessDeniedException ex)
		{
			TraceWrite(ex, "AppStatSet", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\Code\\Client_User.cs", 1510);
		}
		catch (Exception ex2)
		{
			Log.AddError("Could not set application stat", ex2, LogCategories.Operation);
		}
	}

	public System.Threading.Tasks.Task AppRatingSetAsync(int appId, int value)
	{
		return System.Threading.Tasks.Task.Run(delegate
		{
			AppRatingSet(appId, value);
		});
	}

	public void AppRatingSet(int appId, int value)
	{
		if (IsUserLoggedIn && Dispatcher.IsValid)
		{
			int num = CurrentUser.Id;
			ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.ApplicationsManagement, ApplicationManagement.SetApplicationRating, appId, num, value);
			syncOperation.StartEx();
			syncOperation.WaitCompleteEx();
			Rating overallRating = (Rating)syncOperation.Data[0];
			RaiseApplicationRated(appId, overallRating, new Rating(appId, value, 1, DateTime.Now));
		}
	}

	public Stream PersonalFileStreamGet(int profileId, bool store = false)
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		bool isGuest = (CurrentUser ?? throw new ArgumentException("No user logged in")).IsGuest;
		if (store && isGuest)
		{
			throw new ArgumentException("Current user type cannot store Personal user files.", "User");
		}
		IMessageDispatcher messageDispatcher = Dispatcher;
		FileAccess access = FileAccess.Read;
		FileMode mode = FileMode.Open;
		if (store)
		{
			access = FileAccess.ReadWrite;
			mode = FileMode.OpenOrCreate;
		}
		if (isGuest)
		{
			string text = PersonalFileDefaultPathGet(profileId);
			if (!cyFile.Exists(text, messageDispatcher))
			{
				return null;
			}
			return new cyRemoteFileStream(text, mode, access, FileShare.ReadWrite, 262144, FileOptions.SequentialScan, messageDispatcher);
		}
		string text2 = PersonalFilePathGet(profileId);
		if (!store && !cyFile.Exists(text2, messageDispatcher))
		{
			text2 = PersonalFileDefaultPathGet(profileId);
			if (!cyFile.Exists(text2, messageDispatcher))
			{
				return null;
			}
		}
		return new cyRemoteFileStream(text2, mode, access, FileShare.ReadWrite, 262144, FileOptions.SequentialScan, messageDispatcher);
	}

	public string PersonalFileDefaultPathGet(int profileId)
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.GetDefaultProfilePath, profileId);
		syncOperation.StartEx();
		syncOperation.WaitCompleteEx();
		return syncOperation.Data[0] as string;
	}

	public string PersonalFilePathGet(int profileId)
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.GetProfilePath, profileId);
		syncOperation.StartEx();
		syncOperation.WaitCompleteEx();
		return syncOperation.Data[0] as string;
	}

	public string UserHomePathGet()
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.GetHomePath);
		syncOperation.StartEx();
		syncOperation.WaitCompleteEx();
		return syncOperation.Data[0] as string;
	}

	private void OnUserSwitchState(LoginState newState, LoginResult result = LoginResult.Sucess, UserInfoTypes requiredInfo = UserInfoTypes.None)
	{
		lock (LOGIN_OP_LOCK)
		{
			LoginState loginState = LoginState;
			LoginState = newState;
			Environment.SetEnvironmentVariable("CUR_USER_STATE", newState.ToString());
			RaiseLoginStateChange(this, new UserEventArgs(CurrentUser, newState, loginState, result, requiredInfo));
		}
	}

	private void RaiseLoginPropertiesChanged()
	{
		RaisePropertyChanged("CurrentUser");
		RaisePropertyChanged("IsUserLoggingIn");
		RaisePropertyChanged("IsUserLoggedIn");
		RaisePropertyChanged("IsUserLoggedOut");
		RaisePropertyChanged("CanLogin");
		RaisePropertyChanged("CanLogout");
		RaisePropertyChanged("IsCurrentUserIsGuest");
	}

	public GroupConfiguration GetGroupConfiguration()
	{
		if (!Dispatcher.IsValid)
		{
			return null;
		}
		ISyncOperation syncOperation = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.GetGroupConfiguration);
		syncOperation.StartEx();
		syncOperation.WaitCompleteEx();
		return (GroupConfiguration)syncOperation.Data[0];
	}

	public async Task<bool> UsernameExistAsync(string username, CancellationToken ct = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(username))
		{
			throw new ArgumentNullException("username");
		}
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.UsernameExist, username);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.GetProtoResult<bool>();
	}

	public async Task<bool> MobilePhoneExistAsync(string mobile, CancellationToken ct = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(mobile))
		{
			throw new ArgumentNullException("mobile");
		}
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.MobileExist, mobile);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.GetProtoResult<bool>();
	}

	public async Task<bool> UserEmailExistAsync(string emailAddress, CancellationToken ct = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(emailAddress))
		{
			throw new ArgumentNullException("emailAddress");
		}
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.UserEmailExist, emailAddress);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.GetProtoResult<bool>();
	}

	public async Task<UserVerificationStateInfo> EmailVerificationStateInfoGetAsync(string emailAddress, CancellationToken ct = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(emailAddress))
		{
			throw new ArgumentNullException("emailAddress");
		}
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.EmailVerificationStateInfoGet, emailAddress);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.GetProtoResult<UserVerificationStateInfo>();
	}

	public async Task<UserVerificationStateInfo> MobilePhoneVerificationStateInfoGetAsync(string mobilePhoneNumber, CancellationToken ct = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(mobilePhoneNumber))
		{
			throw new ArgumentNullException("mobilePhoneNumber");
		}
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.MobilePhoneVerificationStateInfoGet, mobilePhoneNumber);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.GetProtoResult<UserVerificationStateInfo>();
	}

	public async Task<ProductOrderResult> ProductOrderCreateAsync(ClientProductOrder order, int? paymentMethodId, CancellationToken ct = default(CancellationToken))
	{
		if (order == null)
		{
			throw new ArgumentNullException("order");
		}
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.ProductOrderCreate, order, paymentMethodId);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.GetProtoResult<ProductOrderResult>();
	}

	public async Task<ProductOrderPassResult> ProductOrderPassAsync(int productId, int? paymentMethodId, CancellationToken ct = default(CancellationToken))
	{
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.ProductOrderPass, productId, paymentMethodId);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.GetProtoResult<ProductOrderPassResult>();
	}

	public async Task<AccountCreationByMobilePhoneResult> AccountCreationByMobilePhoneStartAsync(string mobilePhoneNumber, CancellationToken ct = default(CancellationToken))
	{
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.AccountCreateByMobilePhoneStart, mobilePhoneNumber);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.GetProtoResult<AccountCreationByMobilePhoneResult>();
	}

	public async Task<AccountCreationByEmailResult> AccountCreationByEmailStartAsync(string emailAddress, CancellationToken ct = default(CancellationToken))
	{
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.AccountCreateByEmailStart, emailAddress);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.GetProtoResult<AccountCreationByEmailResult>();
	}

	public async Task<AccountCreationByTokenCompleteResult> AccountCreationByTokenCompleteAsync(string token, UserMember userProfile, CancellationToken ct = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			throw new ArgumentNullException("token");
		}
		if (userProfile == null)
		{
			throw new ArgumentNullException("userProfile");
		}
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.AccountCreateByTokenComplete, token, userProfile);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.GetProtoResult<AccountCreationByTokenCompleteResult>();
	}

	public async Task<AccountCreationCompleteResult> AccountCreationCompleteAsync(UserMember userProfile, string password, CancellationToken ct = default(CancellationToken))
	{
		if (userProfile == null)
		{
			throw new ArgumentNullException("userProfile");
		}
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.AccountCreateComplete, userProfile, password);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.GetProtoResult<AccountCreationCompleteResult>();
	}

	public async Task<ClientReservationData> ReservationDataGetAsync(CancellationToken ct = default(CancellationToken))
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.ReservationDataGet);
		await op.StartAsync(ct);
		await op.WaitCompleteAsync(ct);
		return op.GetProtoResult<ClientReservationData>();
	}

	public async Task<UserInfoTypes?> UserGroupDefaultRequiredInfoGetAsync(CancellationToken ct = default(CancellationToken))
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.UserGroupDefaultRequiredInfoGet);
		await op.StartAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		await op.WaitCompleteAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		return op.GetProtoResult<UserInfoTypes?>();
	}

	public async Task<AgreementResult> AgreementGetAsync(AgreementType agreementType = AgreementType.User, CancellationToken ct = default(CancellationToken))
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.AgreementGet, agreementType);
		await op.StartAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		await op.WaitCompleteAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		return op.GetProtoResult<AgreementResult>();
	}

	public async Task<PagedList<UserAgreement>> UserAgreementGetAsync(UserAgreementsFilter filters, CancellationToken ct = default(CancellationToken))
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.UserAgreementGet, filters);
		await op.StartAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		await op.WaitCompleteAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		return op.GetProtoResult<PagedList<UserAgreement>>();
	}

	public async Task<List<UserAgreementState>> UserAgreementStatesGetAsync(int userId, CancellationToken ct = default(CancellationToken))
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.UserAgreementStatesGet, userId);
		await op.StartAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		await op.WaitCompleteAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		return op.GetProtoResult<List<UserAgreementState>>();
	}

	public async Task<UpdateResult> UserAgreementSetStateAsync(int userAgreementId, int userId, UserAgreementAcceptState state, CancellationToken ct = default(CancellationToken))
	{
		Dispatcher.ThrowIfInvalidDispatcher();
		ISyncOperation op = Dispatcher.CreateSyncOperation(CommandType.UserOperation, UserOperations.UserAgreementSetState, userAgreementId, userId, state);
		await op.StartAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		await op.WaitCompleteAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		return op.GetProtoResult<UpdateResult>();
	}
}
