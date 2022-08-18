using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using ServerService;
using SharedLib;
using SharedLib.Dispatcher.Exceptions;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
public class ClientReservationViewModel : ViewModel, IPartImportsSatisfiedNotification
{
	private bool isReseved;

	private bool isLoginBlocked;

	private DateTime? time;

	private Timer timer;

	[Import]
	public GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public bool IsReserved
	{
		get
		{
			return isReseved;
		}
		protected set
		{
			SetProperty(ref isReseved, value, "IsReserved");
		}
	}

	[IgnorePropertyModification]
	public DateTime? Time
	{
		get
		{
			return time;
		}
		protected set
		{
			SetProperty(ref time, value, "Time");
		}
	}

	[IgnorePropertyModification]
	public bool IsLoginBlocked
	{
		get
		{
			return isLoginBlocked;
		}
		protected set
		{
			SetProperty(ref isLoginBlocked, value, "IsLoginBlocked");
		}
	}

	private ClientReservationData Data { get; set; }

	private async void OnClientReservationChange(object sender, ReservationChangeEventArgs e)
	{
		await DetermineAsync().ConfigureAwait(continueOnCapturedContext: false);
	}

	private void OnTimerCallback(object state)
	{
		ResetConfiguration();
	}

	private async Task DetermineAsync()
	{
		try
		{
			Data = await Client.ReservationDataGetAsync().ConfigureAwait(continueOnCapturedContext: false);
			ResetConfiguration();
		}
		catch (OperationNotSupportedException)
		{
		}
		catch (Exception ex2)
		{
			Client.TraceWrite(ex2, "DetermineAsync", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Reservations\\ReservationViewModel.cs", 94);
		}
	}

	private void ResetConfiguration()
	{
		ClientReservationData data = Data;
		int? num = data?.NextReservationId;
		DateTime? dateTime = data?.NextReservationTime;
		if (num.HasValue && dateTime.HasValue)
		{
			DateTime now = DateTime.Now;
			Time = dateTime;
			DateTime value = DateTime.Now.AddHours(1.0);
			DateTime? dateTime2 = dateTime;
			IsReserved = value >= dateTime2;
			if (data.EnableLoginBlock)
			{
				DateTime dateTime3 = dateTime.Value.AddMinutes(data.LoginBlockTime * -1);
				if (data.EnableLoginUnblock)
				{
					DateTime dateTime4 = dateTime.Value.AddMinutes(data.LoginUnblockTime);
					IsLoginBlocked = now <= dateTime4;
				}
				else
				{
					IsLoginBlocked = now >= dateTime3;
				}
			}
			else
			{
				IsLoginBlocked = false;
			}
		}
		else
		{
			IsReserved = false;
			IsLoginBlocked = false;
			Time = null;
		}
	}

	public void OnImportsSatisfied()
	{
		timer = new Timer(OnTimerCallback, null, 0, 30000);
		Client.ReservationChange += OnClientReservationChange;
		DetermineAsync().ContinueWith(delegate
		{
		}).ConfigureAwait(continueOnCapturedContext: false);
	}
}
