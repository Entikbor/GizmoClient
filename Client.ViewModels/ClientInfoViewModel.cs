using System.ComponentModel.Composition;
using SharedLib;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
public class ClientInfoViewModel : ViewModel, IPartImportsSatisfiedNotification
{
	private bool isConnected;

	private bool isOutOfOrder;

	private bool isLocked;

	private string version;

	private int number;

	[Import]
	public GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public int Number
	{
		get
		{
			return number;
		}
		private set
		{
			SetProperty(ref number, value, "Number");
		}
	}

	[IgnorePropertyModification]
	public bool IsConnected
	{
		get
		{
			return isConnected;
		}
		private set
		{
			SetProperty(ref isConnected, value, "IsConnected");
		}
	}

	[IgnorePropertyModification]
	public string Version
	{
		get
		{
			return version;
		}
		set
		{
			SetProperty(ref version, value, "Version");
		}
	}

	[IgnorePropertyModification]
	public bool IsLocked
	{
		get
		{
			return isLocked;
		}
		set
		{
			SetProperty(ref isLocked, value, "IsLocked");
		}
	}

	[IgnorePropertyModification]
	public bool IsOutOfOrder
	{
		get
		{
			return isOutOfOrder;
		}
		set
		{
			SetProperty(ref isOutOfOrder, value, "IsOutOfOrder");
		}
	}

	private void OnClientNumberChange(object sender, IdChangeEventArgs e)
	{
		Number = e.NewId;
	}

	private void OnClientLockStateChange(object sender, LockStateEventArgs e)
	{
		IsLocked = e.IsLocked;
	}

	private void OnClientOutOfOrderStateChange(object sender, OutOfOrderStateEventArgs e)
	{
		IsOutOfOrder = e.IsOutOfOrder;
	}

	public void OnImportsSatisfied()
	{
		Client.IdChange += OnClientNumberChange;
		Client.LockStateChange += OnClientLockStateChange;
		Client.OutOfOrderStateChange += OnClientOutOfOrderStateChange;
		IsOutOfOrder = Client.IsOutOfOrder;
		IsLocked = Client.IsInputLocked;
		Number = Client.Id;
		Version = Client.VersionInfo;
	}
}
