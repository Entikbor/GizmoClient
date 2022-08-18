using System;
using System.ComponentModel.Composition;
using System.Threading;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export(typeof(IShellViewModel))]
[Export]
public class ClientShellViewModel : ItemsListViewModelBaseOfType<SectionModuleViewModel<IClientSectionModule>, NullView>, IPartImportsSatisfiedNotification, IShellViewModel
{
	private CancellationTokenSource MODULE_SWITCH_CTS;

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand HideOverlayCommand => new SimpleCommand<object, object>((object ob) => true, delegate
	{
		Shell.HideCurrentOverlay();
	});

	[Import]
	[IgnorePropertyModification]
	private GizmoClient GizmoClient { get; set; }

	[Import]
	[IgnorePropertyModification]
	private IClinetCompositionService CompositionService { get; set; }

	[Import]
	[IgnorePropertyModification]
	public ClientInfoViewModel Client { get; set; }

	[IgnorePropertyModification]
	public IDialogService DialogService => CompositionService.GetExportedValue<IDialogService>();

	[IgnorePropertyModification]
	public IShellWindow Shell => CompositionService.GetExportedValue<IShellWindow>();

	[Import]
	internal Lazy<UserSettingsViewModel> UserSettingsViewModelLazy { get; set; }

	[Import]
	private Lazy<LoginRotatorViewModel> LoginRotatorLazy { get; set; }

	[Import]
	private Lazy<NewsFeedsViewModel> NewsLazy { get; set; }

	[Import]
	private Lazy<AppsViewModel> AppsLazy { get; set; }

	[Import]
	private Lazy<ClientUserViewModel> UserViewModelLzay { get; set; }

	[Import]
	private Lazy<UserLoginViewModel> UserLoginViewModelLzay { get; set; }

	[Import]
	private Lazy<ShellSettingsViewModel> ShellSettingsViewModelLazy { get; set; }

	[Import]
	private Lazy<ClientPurchaseViewModel> ShopViewModelLazy { get; set; }

	[Import]
	private Lazy<ClientReservationViewModel> ReservationViewModelLazy { get; set; }

	[Import]
	private Lazy<ClientGracePeriodViewModel> GracePeriodViewModelLazy { get; set; }

	[Import]
	private Lazy<ClientUserAgreementsViewModel> UserAgreementsViewModelLazy { get; set; }

	[Import]
	private Lazy<ClientUserLockViewModel> UserLockViewModelLazy { get; set; }

	public AppsViewModel Apps => AppsLazy.Value;

	public NewsFeedsViewModel News => NewsLazy.Value;

	public ClientUserViewModel UserViewModel => UserViewModelLzay.Value;

	public UserLoginViewModel UserLoginViewModel => UserLoginViewModelLzay.Value;

	public UserSettingsViewModel UserSettingsViewModel => UserSettingsViewModelLazy.Value;

	public LoginRotatorViewModel LoginRotator => LoginRotatorLazy.Value;

	public ShellSettingsViewModel ShellSettingsViewModel => ShellSettingsViewModelLazy.Value;

	public ClientPurchaseViewModel ShopViewModel => ShopViewModelLazy.Value;

	public ClientReservationViewModel ReservationViewModel => ReservationViewModelLazy.Value;

	public ClientGracePeriodViewModel GracePeriodViewModel => GracePeriodViewModelLazy.Value;

	public ClientUserAgreementsViewModel UserAgreementsViewModel => UserAgreementsViewModelLazy.Value;

	public ClientUserLockViewModel UserLockViewModel => UserLockViewModelLazy.Value;

	protected override async void OnSelectedItemChangedCompleted(SectionModuleViewModel<IClientSectionModule> newItem, SectionModuleViewModel<IClientSectionModule> oldItem)
	{
		base.OnSelectedItemChangedCompleted(newItem, oldItem);
		if (oldItem == newItem)
		{
			return;
		}
		MODULE_SWITCH_CTS?.Cancel();
		MODULE_SWITCH_CTS = new CancellationTokenSource();
		try
		{
			if (oldItem != null)
			{
				await (oldItem?.Module.SwitchOutAsync(MODULE_SWITCH_CTS.Token));
			}
			if (newItem != null)
			{
				await (newItem?.Module.SwitchInAsync(MODULE_SWITCH_CTS.Token));
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch
		{
		}
	}

	private void OnClientLanguageChange(object sender, LanguageChangeEventArgs e)
	{
		try
		{
			foreach (SectionModuleViewModel<IClientSectionModule> item in Items)
			{
				IClientSkinModuleMetadata metaData = item.MetaData;
				if (metaData != null && metaData.IsLocalized)
				{
					item.Title = ((metaData.IsLocalized && !string.IsNullOrWhiteSpace(metaData.Title)) ? GizmoClient.GetLocalizedString(metaData.Title) : metaData.Title);
					item.Description = ((metaData.IsLocalized && !string.IsNullOrWhiteSpace(metaData.Description)) ? GizmoClient.GetLocalizedString(metaData.Description) : metaData.Description);
				}
			}
		}
		catch (Exception ex)
		{
			GizmoClient.TraceWrite(ex, "OnClientLanguageChange", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\ShellViewModel.cs", 279);
		}
	}

	public void OnImportsSatisfied()
	{
		GizmoClient.LanguageChange -= OnClientLanguageChange;
		GizmoClient.LanguageChange += OnClientLanguageChange;
	}
}
