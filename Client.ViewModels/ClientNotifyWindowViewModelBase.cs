using System;
using System.Windows;

namespace Client.ViewModels;

public class ClientNotifyWindowViewModelBase : NotifyWindowViewModelBase
{
	public GizmoClient Client { get; protected set; }

	public ClientNotifyWindowViewModelBase(GizmoClient client, Window window)
		: base(window)
	{
		if (client != null)
		{
			Client = client;
			base.AllowDrag = false;
			return;
		}
		throw new NullReferenceException();
	}
}
