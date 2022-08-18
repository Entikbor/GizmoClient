#define TRACE
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Client.Drivers;
using CoreLib;
using CoreLib.Diagnostics;
using CoreLib.Tools;
using Microsoft.Extensions.Hosting.WindowsServices;
using SharedLib.Configuration;
using Unosquare.FFME;
using Win32API.Headers;
using Win32API.Modules;

namespace Client;

public class EntryPoint
{
	public const string CLIENT_MUTEX_NAME = "Global\\{a9004961-1841-4990-a29f-d81804df1b67}";

	public const string CLIENT_SERVICE_MUTEX_NAME = "{16243237-ce25-4bde-957e-82639dca0bde}";

	public const string CALLBACK_CLIENT_APP_NAME = "{db531ebb-5c4f-457a-bba0-33ef7f2171c0}";

	public const string CALLBACK_SERVICE_APP_NAME = "{b222bb8c-d45b-4c2d-aac1-b5968f2fbec6}";

	public const string CLIENT_SERVICE_NAME = "GizmoClientService";

	public const string CLIENT_SERVICE_DISPLAY_NAME = "Gizmo Client Service";

	public const string WINDOWS_APP_ID = "GIZMO_CLIENT_APP";

	public const int SERVICE_STOP_CODE = 233;

	private const int INSTANCE_WAIT_SECONDS = 5;

	public static bool ENABLE_CLIENT_SERVICE;

	public static string WINDOWS_APP_SHORTCUT;

	public static readonly uint CURRENT_PROCESS_ID;

	public static readonly string PROCESS_FULL_FILE_NAME;

	public static readonly string PROCESS_DIRECTORY;

	public static readonly string PRCOESS_FILE_NAME_WITHOUT_EXTENSION;

	public static readonly string DEPENDENCY_BASE_PATH;

	public static readonly string DEPENDENCY_FULL_PATH;

	private static readonly bool INTERACTIVE_PROCESS;

	private static readonly string[] DEPRECATED_ASSEMBLIES;

	private static readonly string[] NATIVE_DEPENDENCIES;

	private static readonly string CACHE_LOG_FILE_NAME;

	private static readonly object LOG_LOCK;

	static EntryPoint()
	{
		ENABLE_CLIENT_SERVICE = true;
		WINDOWS_APP_SHORTCUT = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Microsoft\\Windows\\Start Menu\\Programs\\Gizmo Client.lnk";
		CURRENT_PROCESS_ID = Kernel32.GetCurrentProcessId();
		PROCESS_FULL_FILE_NAME = Process.GetCurrentProcess().MainModule.FileName;
		PROCESS_DIRECTORY = Path.GetDirectoryName(PROCESS_FULL_FILE_NAME);
		PRCOESS_FILE_NAME_WITHOUT_EXTENSION = Path.GetFileNameWithoutExtension(PROCESS_FULL_FILE_NAME);
		DEPENDENCY_BASE_PATH = Path.Combine(PROCESS_DIRECTORY, "References");
		DEPENDENCY_FULL_PATH = Path.Combine(DEPENDENCY_BASE_PATH, Environment.Is64BitProcess ? "x64" : "x86");
		INTERACTIVE_PROCESS = !WindowsServiceHelpers.IsWindowsService();
		DEPRECATED_ASSEMBLIES = new string[3] { "Win32APIC.dll", "SharedControlsLib.dll", "ffme.common.dll" };
		NATIVE_DEPENDENCIES = new string[0];
		CACHE_LOG_FILE_NAME = Path.Combine(PROCESS_DIRECTORY, "cachelog.txt");
		LOG_LOCK = new object();
		if (INTERACTIVE_PROCESS)
		{
			Kernel32.SetErrorMode(Kernel32.SetErrorMode(ErrorModes.SYSTEM_DEFAULT) | ErrorModes.SEM_FAILCRITICALERRORS | ErrorModes.SEM_NOGPFAULTERRORBOX | ErrorModes.SEM_NOOPENFILEERRORBOX);
			try
			{
				int num = Wer.WerAddExcludedApplication(PROCESS_FULL_FILE_NAME, bAllUsers: true);
				if (num != 0)
				{
					throw new Win32Exception(num);
				}
				return;
			}
			catch (Exception ex)
			{
				TryWriteToCacheLog("WerAddExcludedApplication failed.", ex.Message, ".cctor", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 43);
				return;
			}
		}
		TryWriteToCacheLog("Processing EntryPoint", null, ".cctor", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 48);
		Directory.SetCurrentDirectory(PROCESS_DIRECTORY);
	}

	[STAThread]
	public static void Main(string[] args)
	{
		try
		{
			if (args.Any((string arg) => arg.Equals("-restart-service", StringComparison.InvariantCultureIgnoreCase)))
			{
				try
				{
					if (TryStopService(TimeSpan.FromSeconds(5.0)))
					{
						TryStartService();
					}
					else
					{
						TryWriteToCacheLog("-restart-service Command failed.", null, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 118);
					}
					return;
				}
				catch (Exception ex)
				{
					TryWriteToCacheLog("Could not execute -restart-service command.", ex.Message, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 123);
					return;
				}
			}
			ProfileOptimization.SetProfileRoot(Environment.CurrentDirectory);
			ProfileOptimization.StartProfile("Startup.Profile");
			ENABLE_CLIENT_SERVICE = ENABLE_CLIENT_SERVICE && !args.Any((string arg) => arg.Equals("-no-service", StringComparison.InvariantCultureIgnoreCase));
			bool flag = false;
			string name = (INTERACTIVE_PROCESS ? "Global\\{a9004961-1841-4990-a29f-d81804df1b67}" : "{16243237-ce25-4bde-957e-82639dca0bde}");
			bool createdNew;
			using Mutex mutex = new Mutex(initiallyOwned: true, name, out createdNew);
			try
			{
				try
				{
					if (!mutex.WaitOne(TimeSpan.FromSeconds(5.0), exitContext: true))
					{
						TraceWriter.TraceMessage("Another instance is already running.", "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 150);
					}
				}
				catch (AbandonedMutexException)
				{
					flag = true;
					TraceWriter.TraceMessage("First instance exited during wait.", "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 155);
				}
				if (createdNew || flag)
				{
					if (!INTERACTIVE_PROCESS)
					{
						using (ClientService service = new ClientService())
						{
							ServiceBase.Run(service);
							return;
						}
					}
					if (!CoreProcess.IsAdministrator)
					{
						MessageBoxEx.Show("Application can only run under administrative account.\nLogging off.", PRCOESS_FILE_NAME_WITHOUT_EXTENSION, MessageBoxButtons.OK, MessageBoxIcon.Hand, 10000u);
						PowerTool.LogOff(force: true);
						CoreProcess.ExitCurrent(ProcessExitCode.SessionEnded);
					}
					if (!CoreProcess.IsElevated)
					{
						if (CoreProcess.IsElevatedAdministrator)
						{
							bool.TryParse(Environment.GetEnvironmentVariable("CLIENT_ELEVATE", EnvironmentVariableTarget.User), out var result);
							if (result)
							{
								Environment.SetEnvironmentVariable("CLIENT_ELEVATE", null, EnvironmentVariableTarget.User);
								System.Windows.MessageBox.Show("Application loading cannot continue.\nReason:UAC Enabled or limited account.", PRCOESS_FILE_NAME_WITHOUT_EXTENSION, MessageBoxButton.OK, MessageBoxImage.Hand);
							}
							else
							{
								Environment.SetEnvironmentVariable("CLIENT_ELEVATE", true.ToString(), EnvironmentVariableTarget.User);
								string arguments = (args.Any() ? args.Aggregate((string first, string next) => first + " " + next) : string.Empty);
								ProcessStartInfo startInfo = new ProcessStartInfo
								{
									UseShellExecute = true,
									WorkingDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath),
									FileName = System.Windows.Forms.Application.ExecutablePath,
									Arguments = arguments,
									Verb = "runas"
								};
								try
								{
									Process.Start(startInfo);
								}
								catch
								{
									throw;
								}
							}
						}
						else
						{
							System.Windows.MessageBox.Show("Application can only run under administrative account.", PRCOESS_FILE_NAME_WITHOUT_EXTENSION, MessageBoxButton.OK, MessageBoxImage.Hand);
							CoreProcess.ExitCurrent(ProcessExitCode.ShutDown);
						}
					}
					else
					{
						Environment.SetEnvironmentVariable("CLIENT_ELEVATE", null, EnvironmentVariableTarget.User);
					}
					try
					{
						if (args.Length >= 2 && args[0].Compare("connect", ignoreCase: true))
						{
							string text = args[1];
							if (!string.IsNullOrWhiteSpace(text))
							{
								int? preferedPort = null;
								if (args.Length >= 3 && int.TryParse(args[2], out var result2) && result2 > 0 && result2 < 65536)
								{
									preferedPort = result2;
								}
								ClientSettings.TrySetRegistryConnectionSettings(text, preferedPort);
							}
						}
					}
					catch (Exception ex3)
					{
						TryWriteToCacheLog("Coud not parse/save connection settings.", ex3.Message, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 286);
					}
					string[] dEPRECATED_ASSEMBLIES = DEPRECATED_ASSEMBLIES;
					foreach (string path in dEPRECATED_ASSEMBLIES)
					{
						try
						{
							if (File.Exists(path))
							{
								File.Delete(path);
							}
						}
						catch (Exception ex4)
						{
							TraceWriter.TraceException(ex4, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 301);
						}
					}
					if (ENABLE_CLIENT_SERVICE)
					{
						try
						{
							IntPtr intPtr = AdvApi32.OpenSCManager(null, null, SC_MANAGER_ACCESS_RIGHTS.SC_MANAGER_ALL_ACCESS);
							if (intPtr == IntPtr.Zero)
							{
								throw new Win32Exception();
							}
							IntPtr intPtr2 = AdvApi32.OpenService(intPtr, "GizmoClientService", 983551u);
							try
							{
								bool flag2 = false;
								if (intPtr2 == IntPtr.Zero)
								{
									int lastWin32Error = Marshal.GetLastWin32Error();
									if (lastWin32Error != 1060)
									{
										throw new Win32Exception(lastWin32Error);
									}
								}
								if (intPtr2 != IntPtr.Zero && !AdvApi32.QueryServiceConfig(intPtr2, IntPtr.Zero, 0u, out var pcbBytesNeeded))
								{
									int lastWin32Error2 = Marshal.GetLastWin32Error();
									if (lastWin32Error2 != 122)
									{
										throw new Win32Exception(lastWin32Error2);
									}
									IntPtr intPtr3 = Marshal.AllocHGlobal((int)pcbBytesNeeded);
									if (!AdvApi32.QueryServiceConfig(intPtr2, intPtr3, pcbBytesNeeded, out var _))
									{
										throw new Win32Exception();
									}
									QUERY_SERVICE_CONFIG qUERY_SERVICE_CONFIG = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(intPtr3);
									flag2 = string.Compare(qUERY_SERVICE_CONFIG.lpBinaryPathName, PROCESS_FULL_FILE_NAME, ignoreCase: true) != 0 || qUERY_SERVICE_CONFIG.dwStartType != 2 || qUERY_SERVICE_CONFIG.dwServiceType != 16 || qUERY_SERVICE_CONFIG.lpServiceStartName != "LocalSystem";
								}
								SERVICE_STATUS lpServiceStatus = default(SERVICE_STATUS);
								if (flag2 && intPtr2 != IntPtr.Zero)
								{
									try
									{
										if (!AdvApi32.QueryServiceStatus(intPtr2, out lpServiceStatus))
										{
											throw new Win32Exception();
										}
										if (lpServiceStatus.dwCurrentState == SERVICE_STATE.SERVICE_RUNNING && !AdvApi32.ControlService(intPtr2, 233, out lpServiceStatus))
										{
											throw new Win32Exception();
										}
										if (!AdvApi32.DeleteService(intPtr2))
										{
											throw new Win32Exception();
										}
									}
									catch
									{
										throw;
									}
									finally
									{
										if (!AdvApi32.CloseServiceHandle(intPtr2))
										{
											throw new Win32Exception();
										}
									}
								}
								if (flag2 || intPtr2 == IntPtr.Zero)
								{
									intPtr2 = AdvApi32.CreateService(intPtr, "GizmoClientService", "Gizmo Client Service", 983551u, SERVICE_TYPES.SERVICE_WIN32_OWN_PROCESS, SERVICE_START_TYPE.SERVICE_AUTO_START, SERVICE_ERROR_CONTROL.SERVICE_ERROR_NORMAL, PROCESS_FULL_FILE_NAME, string.Empty, IntPtr.Zero, null, null, string.Empty);
									if (intPtr2 == IntPtr.Zero)
									{
										throw new Win32Exception();
									}
								}
								if (!AdvApi32.QueryServiceStatus(intPtr2, out lpServiceStatus))
								{
									throw new Win32Exception();
								}
								if (lpServiceStatus.dwCurrentState != SERVICE_STATE.SERVICE_RUNNING)
								{
									switch (lpServiceStatus.dwCurrentState)
									{
									case SERVICE_STATE.SERVICE_START_PENDING:
									case SERVICE_STATE.SERVICE_RUNNING:
										return;
									default:
										if (!AdvApi32.StartService(intPtr2, 0u))
										{
											throw new Win32Exception();
										}
										CoreProcess.ExitCurrent(ProcessExitCode.ShutDown);
										break;
									}
								}
							}
							catch
							{
								throw;
							}
							finally
							{
								AdvApi32.CloseServiceHandle(intPtr);
								AdvApi32.CloseServiceHandle(intPtr2);
							}
						}
						catch (Exception ex5)
						{
							TryWriteToCacheLog("Could not initialize client service.", ex5.Message, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 453);
						}
					}
					try
					{
						Library.FFmpegDirectory = Path.Combine(DEPENDENCY_FULL_PATH, "FFMpeg");
					}
					catch (Exception ex6)
					{
						TryWriteToCacheLog("Failed to initialize FFMpeg.", ex6.Message, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 467);
					}
					if (Kernel32.SetDllDirectory(DEPENDENCY_FULL_PATH))
					{
						try
						{
							dEPRECATED_ASSEMBLIES = NATIVE_DEPENDENCIES;
							foreach (string text2 in dEPRECATED_ASSEMBLIES)
							{
								string text3 = Path.Combine(DEPENDENCY_FULL_PATH, text2);
								if (File.Exists(text3) && Kernel32.LoadLibrary(text3) == IntPtr.Zero)
								{
									TryWriteToCacheLog("Could not load native dependency " + text2 + ", full path " + text3 + ".", null, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 493);
								}
							}
						}
						catch
						{
							throw;
						}
						finally
						{
							Kernel32.SetDllDirectory();
						}
					}
					else
					{
						TryWriteToCacheLog("SetDllDirectory failed.", new Win32Exception(Marshal.GetLastWin32Error()).Message, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 509);
					}
					if (Kernel32.SetDllDirectory(DEPENDENCY_FULL_PATH))
					{
						try
						{
							try
							{
								using CBProcessHelper cBProcessHelper = new CBProcessHelper();
								cBProcessHelper.Initialize("{db531ebb-5c4f-457a-bba0-33ef7f2171c0}");
							}
							catch (Exception ex7)
							{
								TryWriteToCacheLog("Failed to initialize CB Process 2020 driver.", ex7.Message, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 530);
							}
							try
							{
								using CBFilterHelper cBFilterHelper = new CBFilterHelper();
								cBFilterHelper.Initialize("{db531ebb-5c4f-457a-bba0-33ef7f2171c0}");
							}
							catch (Exception ex8)
							{
								TryWriteToCacheLog("Failed to initialize CB Filter 2020 driver.", ex8.Message, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 546);
							}
							try
							{
								using CBRegistryHelper cBRegistryHelper = new CBRegistryHelper();
								cBRegistryHelper.Initialize("{db531ebb-5c4f-457a-bba0-33ef7f2171c0}");
							}
							catch (Exception ex9)
							{
								TryWriteToCacheLog("Failed to initialize CB Registry 2020 driver.", ex9.Message, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 562);
							}
							try
							{
								using CBConnectHelper cBConnectHelper = new CBConnectHelper();
								cBConnectHelper.Initialize("{db531ebb-5c4f-457a-bba0-33ef7f2171c0}");
							}
							catch (Exception ex10)
							{
								TryWriteToCacheLog("Failed to initialize CB Connect 2020 driver.", ex10.Message, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 577);
							}
						}
						catch
						{
							throw;
						}
						finally
						{
							Kernel32.SetDllDirectory();
						}
					}
					else
					{
						TryWriteToCacheLog("SetDllDirectory failed.", new Win32Exception(Marshal.GetLastWin32Error()).Message, "Main", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 592);
					}
					new ClientApplication().Run();
				}
				else
				{
					CoreProcess.ExitCurrent(ProcessExitCode.AnotherInstanceRunning);
				}
			}
			catch
			{
				throw;
			}
			finally
			{
				if (createdNew || flag)
				{
					mutex.ReleaseMutex();
				}
			}
		}
		catch (Exception ex11)
		{
			CoreProcess.CrashExitCurrent(ex11);
		}
	}

	public static void TryWriteToCacheLog(string message, string exceptionMessage = null, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
	{
		lock (LOG_LOCK)
		{
			try
			{
				string text = "RELEASE";
				string text2 = $"{DateTime.Now} [{text}] [{memberName}] {message} [{sourceLineNumber}][{sourceFilePath}]";
				text2 = ((exceptionMessage != null) ? (text2 + Environment.NewLine + exceptionMessage) : text2);
				using StreamWriter streamWriter = new StreamWriter(CACHE_LOG_FILE_NAME, append: true);
				streamWriter.WriteLine(text2);
			}
			catch (Exception arg)
			{
				try
				{
					Trace.WriteLine($"Could not write cache log entry. {arg}");
				}
				catch
				{
				}
			}
		}
	}

	public static void Uninstall()
	{
		try
		{
			IntPtr intPtr = AdvApi32.OpenSCManager(null, null, SC_MANAGER_ACCESS_RIGHTS.SC_MANAGER_ALL_ACCESS);
			if (intPtr == IntPtr.Zero)
			{
				throw new Win32Exception();
			}
			IntPtr intPtr2 = AdvApi32.OpenService(intPtr, "GizmoClientService", 983551u);
			try
			{
				if (intPtr2 == IntPtr.Zero)
				{
					int lastWin32Error = Marshal.GetLastWin32Error();
					if (lastWin32Error != 1060)
					{
						throw new Win32Exception(lastWin32Error);
					}
				}
				if (intPtr2 != IntPtr.Zero)
				{
					SERVICE_STATUS lpServiceStatus = default(SERVICE_STATUS);
					if (!AdvApi32.QueryServiceStatus(intPtr2, out lpServiceStatus))
					{
						throw new Win32Exception();
					}
					SERVICE_STATE dwCurrentState = lpServiceStatus.dwCurrentState;
					if ((dwCurrentState == SERVICE_STATE.SERVICE_START_PENDING || dwCurrentState == SERVICE_STATE.SERVICE_RUNNING) && !AdvApi32.ControlService(intPtr2, 233, out lpServiceStatus))
					{
						throw new Win32Exception();
					}
					if (!AdvApi32.DeleteService(intPtr2))
					{
						throw new Win32Exception();
					}
				}
			}
			catch
			{
				throw;
			}
			finally
			{
				AdvApi32.CloseServiceHandle(intPtr);
				AdvApi32.CloseServiceHandle(intPtr2);
			}
		}
		catch (Exception ex)
		{
			TryWriteToCacheLog("EntryPoint.Uninstall failed to disable/delete cleint service.", ex.Message, "Uninstall", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 750);
		}
		try
		{
			using CBProcessHelper cBProcessHelper = new CBProcessHelper();
			cBProcessHelper.Uninstall("{db531ebb-5c4f-457a-bba0-33ef7f2171c0}");
		}
		catch (Exception ex2)
		{
			TryWriteToCacheLog(string.Format("CB Process 2020 driver unsinstall failed, app guid {0}, error message {1}.", "{db531ebb-5c4f-457a-bba0-33ef7f2171c0}", 0), ex2.Message, "Uninstall", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 768);
		}
		try
		{
			using CBProcessHelper cBProcessHelper2 = new CBProcessHelper();
			cBProcessHelper2.Uninstall("{b222bb8c-d45b-4c2d-aac1-b5968f2fbec6}");
		}
		catch (Exception ex3)
		{
			TryWriteToCacheLog(string.Format("CB Process 2020 driver unsinstall failed, app guid {0}, error message {1}.", "{b222bb8c-d45b-4c2d-aac1-b5968f2fbec6}", 0), ex3.Message, "Uninstall", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 784);
		}
		try
		{
			using CBFilterHelper cBFilterHelper = new CBFilterHelper();
			cBFilterHelper.Uninstall("{db531ebb-5c4f-457a-bba0-33ef7f2171c0}");
		}
		catch (Exception ex4)
		{
			TryWriteToCacheLog(string.Format("CB Filter 2020 driver unsinstall failed, app guid {0}, error message {1}.", "{db531ebb-5c4f-457a-bba0-33ef7f2171c0}", 0), ex4.Message, "Uninstall", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 800);
		}
		try
		{
			using CBRegistryHelper cBRegistryHelper = new CBRegistryHelper();
			cBRegistryHelper.Uninstall("{db531ebb-5c4f-457a-bba0-33ef7f2171c0}");
		}
		catch (Exception ex5)
		{
			TryWriteToCacheLog(string.Format("CB Registry 2020 driver unsinstall failed, app guid {0}, error message {1}.", "{db531ebb-5c4f-457a-bba0-33ef7f2171c0}", 0), ex5.Message, "Uninstall", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 816);
		}
		try
		{
			using CBConnectHelper cBConnectHelper = new CBConnectHelper();
			cBConnectHelper.Uninstall("{db531ebb-5c4f-457a-bba0-33ef7f2171c0}");
		}
		catch (Exception ex6)
		{
			TryWriteToCacheLog(string.Format("CB Connect 2020 driver unsinstall failed, app guid {0}, error message {1}.", "{db531ebb-5c4f-457a-bba0-33ef7f2171c0}", 0), ex6.Message, "Uninstall", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 832);
		}
		try
		{
			int num = Wer.WerRemoveExcludedApplication(PROCESS_FULL_FILE_NAME, bAllUsers: true);
			if (num != 0)
			{
				throw new Win32Exception(num);
			}
		}
		catch (Exception ex7)
		{
			TryWriteToCacheLog("WerRemoveExcludedApplication failed.", ex7.Message, "Uninstall", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\App.xaml.cs", 848);
		}
	}

	public static bool TryStopService()
	{
		return TryStopService(TimeSpan.FromSeconds(30.0));
	}

	public static bool TryStartService()
	{
		IntPtr intPtr = AdvApi32.OpenSCManager(null, null, SC_MANAGER_ACCESS_RIGHTS.SC_MANAGER_ALL_ACCESS);
		if (intPtr == IntPtr.Zero)
		{
			throw new Win32Exception();
		}
		IntPtr intPtr2 = AdvApi32.OpenService(intPtr, "GizmoClientService", 983551u);
		try
		{
			if (intPtr2 == IntPtr.Zero)
			{
				return false;
			}
			AdvApi32.StartService(intPtr2, 0u);
			return true;
		}
		finally
		{
			AdvApi32.CloseServiceHandle(intPtr);
			AdvApi32.CloseServiceHandle(intPtr2);
		}
	}

	public static bool TryStopService(TimeSpan waitSpan)
	{
		try
		{
			IntPtr intPtr = AdvApi32.OpenSCManager(null, null, SC_MANAGER_ACCESS_RIGHTS.SC_MANAGER_ALL_ACCESS);
			if (intPtr == IntPtr.Zero)
			{
				throw new Win32Exception();
			}
			IntPtr intPtr2 = AdvApi32.OpenService(intPtr, "GizmoClientService", 983551u);
			try
			{
				if (intPtr2 == IntPtr.Zero)
				{
					int lastWin32Error = Marshal.GetLastWin32Error();
					if (lastWin32Error != 1060)
					{
						throw new Win32Exception(lastWin32Error);
					}
				}
				if (intPtr2 != IntPtr.Zero)
				{
					SERVICE_STATUS lpServiceStatus = default(SERVICE_STATUS);
					if (!AdvApi32.QueryServiceStatus(intPtr2, out lpServiceStatus))
					{
						throw new Win32Exception();
					}
					switch (lpServiceStatus.dwCurrentState)
					{
					case SERVICE_STATE.SERVICE_START_PENDING:
					case SERVICE_STATE.SERVICE_RUNNING:
						if (!AdvApi32.ControlService(intPtr2, 233, out lpServiceStatus))
						{
							throw new Win32Exception();
						}
						break;
					case SERVICE_STATE.SERVICE_STOPPED:
						return true;
					}
					return WaitForServiceState(intPtr2, SERVICE_STATE.SERVICE_STOPPED, waitSpan);
				}
				return true;
			}
			catch
			{
				throw;
			}
			finally
			{
				AdvApi32.CloseServiceHandle(intPtr);
				AdvApi32.CloseServiceHandle(intPtr2);
			}
		}
		catch
		{
			return false;
		}
	}

	public static IntPtr OpenClientServiceHandle()
	{
		IntPtr intPtr = AdvApi32.OpenSCManager(null, null, SC_MANAGER_ACCESS_RIGHTS.SC_MANAGER_ALL_ACCESS);
		if (intPtr == IntPtr.Zero)
		{
			throw new Win32Exception();
		}
		IntPtr intPtr2 = AdvApi32.OpenService(intPtr, "GizmoClientService", 983551u);
		if (intPtr2 == IntPtr.Zero)
		{
			throw new Win32Exception();
		}
		return intPtr2;
	}

	public static bool TryOpentClientServiceHandle(out IntPtr serviceHandle)
	{
		serviceHandle = IntPtr.Zero;
		try
		{
			serviceHandle = OpenClientServiceHandle();
			return true;
		}
		catch (Win32Exception)
		{
		}
		catch
		{
			throw;
		}
		return false;
	}

	public unsafe static SERVICE_STATUS_PROCESS GetServiceStatusProcess(IntPtr serviceHandle)
	{
		IntPtr intPtr = Marshal.AllocHGlobal(sizeof(SERVICE_STATUS_PROCESS));
		if (!AdvApi32.QueryServiceStatusEx(serviceHandle, SC_STATUS_TYPE.SC_STATUS_PROCESS_INFO, intPtr, sizeof(SERVICE_STATUS_PROCESS), out var _))
		{
			throw new Win32Exception();
		}
		return (SERVICE_STATUS_PROCESS)Marshal.PtrToStructure(intPtr, typeof(SERVICE_STATUS_PROCESS));
	}

	public static bool TryGetServiceStatusProcess(IntPtr serviceHandle, out SERVICE_STATUS_PROCESS status)
	{
		status = default(SERVICE_STATUS_PROCESS);
		try
		{
			status = GetServiceStatusProcess(serviceHandle);
			return true;
		}
		catch (Win32Exception)
		{
		}
		catch
		{
			throw;
		}
		return false;
	}

	public static bool WaitForServiceState(IntPtr hService, SERVICE_STATE state)
	{
		return WaitForServiceState(hService, state, TimeSpan.MaxValue);
	}

	public static bool WaitForServiceState(IntPtr hService, SERVICE_STATE state, TimeSpan timeout)
	{
		if (!Enum.IsDefined(typeof(SERVICE_STATE), state))
		{
			throw new InvalidEnumArgumentException("state", (int)state, typeof(SERVICE_STATE));
		}
		if (!AdvApi32.QueryServiceStatus(hService, out var lpServiceStatus))
		{
			throw new Win32Exception();
		}
		if (lpServiceStatus.dwCurrentState == state)
		{
			return true;
		}
		DateTime utcNow = DateTime.UtcNow;
		while (lpServiceStatus.dwCurrentState != state)
		{
			if (DateTime.UtcNow - utcNow > timeout)
			{
				return false;
			}
			Thread.Sleep(250);
			if (!AdvApi32.QueryServiceStatus(hService, out lpServiceStatus))
			{
				throw new Win32Exception();
			}
		}
		return true;
	}
}
