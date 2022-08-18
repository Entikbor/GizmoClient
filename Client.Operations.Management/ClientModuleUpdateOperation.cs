using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CoreLib.Tools;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class ClientModuleUpdateOperation : IOperationBase, IOperation
{
	public ClientModuleUpdateOperation(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		if (base.Command.ParamsArray.Length > 2)
		{
			try
			{
				RaiseStarted();
				byte[] bytes = (byte[])base.Command.ParamsArray[1];
				byte[] bytes2 = (byte[])base.Command.ParamsArray[2];
				string text = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), RandomGenerator.RandomString(10));
				string text2 = Path.Combine(Environment.ExpandEnvironmentVariables("%TEMP%"), "Updater");
				File.WriteAllBytes(text, bytes);
				File.WriteAllBytes(text2, bytes2);
				if (File.Exists(text))
				{
					if (File.Exists(text2))
					{
						try
						{
							EntryPoint.TryStopService();
							Thread.Sleep(5000);
							Process process = new Process();
							process.StartInfo.FileName = text2;
							process.StartInfo.LoadUserProfile = false;
							process.StartInfo.UseShellExecute = false;
							process.StartInfo.Arguments = "\"" + text + "\" " + Process.GetCurrentProcess().Id + " \"" + Process.GetCurrentProcess().MainModule.FileName + "\" \"" + GizmoClient.Current.InitialCommandLine + "\"";
							process.Start();
							RaiseCompleted(process.Id);
							return;
						}
						catch (Exception ex)
						{
							RaiseFailed(ex.Message);
							return;
						}
						finally
						{
							GizmoClient.Current.Stop();
						}
					}
					RaiseFailed("Updater module not found.");
				}
				else
				{
					RaiseFailed("Updated Client module file not found.");
				}
				return;
			}
			catch (Exception ex2)
			{
				RaiseFailed(ex2);
				return;
			}
		}
		RaiseOperationStateChange(OperationState.InvalidParameters);
	}
}
