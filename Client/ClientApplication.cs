using System;
using System.Windows;
using CoreLib.Diagnostics;
using GizmoDALV2;

namespace Client;

public partial class ClientApplication : Application
{
	public ClientApplication()
	{
		GizmoClient.Current = new GizmoClient();
		base.ShutdownMode = ShutdownMode.OnExplicitShutdown;
	}

	protected override async void OnStartup(StartupEventArgs e)
	{
		try
		{
			base.OnStartup(e);
			ProtoSetup.InitTypes();
			await GizmoClient.Current.StartAsync(e.Args);
		}
		catch (Exception ex)
		{
			CoreProcess.CrashExitCurrent(ex);
		}
	}
}
