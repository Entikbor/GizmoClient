using System;

namespace Client.ViewModels;

public class ClientSplashWindowModel : NotifyWindowViewModelBase
{
	private GizmoClient client;

	private string activityString;

	private ClientStartupActivity currentActivity;

	public GizmoClient Client
	{
		get
		{
			return client;
		}
		protected set
		{
			client = value;
		}
	}

	public ClientStartupActivity CurrentActivity
	{
		get
		{
			return currentActivity;
		}
		protected set
		{
			currentActivity = value;
			RaisePropertyChanged("CurrentActivity");
		}
	}

	public string ActivityString
	{
		get
		{
			return activityString;
		}
		protected set
		{
			activityString = value;
			RaisePropertyChanged("ActivityString");
		}
	}

	public ClientSplashWindowModel(GizmoClient client)
		: base(new SplashWindow
		{
			Topmost = false
		})
	{
		Client = client;
		Client.ActivityChange += OnClientActivityChanged;
		base.AllowDrag = true;
	}

	private void OnClientActivityChanged(object sender, ClientActivityEventArgs args)
	{
		ClientStartupActivity clientStartupActivity = (CurrentActivity = args.Activity);
		ActivityString = Client.GetLocalizedObject<string>(clientStartupActivity);
	}

	protected override void OnMainWindowClosed(object sender, EventArgs e)
	{
		base.OnMainWindowClosed(sender, e);
		Client.ActivityChange -= OnClientActivityChanged;
	}
}
