using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
public class UserSettingsViewModel : ItemsListViewModelBaseOfType<SectionModuleViewModel<IClientUserSettingsModule>, NullView>, IPartImportsSatisfiedNotification
{
	protected override async void OnSelectedItemChangedCompleted(SectionModuleViewModel<IClientUserSettingsModule> newItem, SectionModuleViewModel<IClientUserSettingsModule> oldItem)
	{
		base.OnSelectedItemChangedCompleted(newItem, oldItem);
		if (newItem != null)
		{
			await newItem.Module.SwitchInAsync(CancellationToken.None);
		}
		if (oldItem != null)
		{
			await oldItem.Module.SwitchOutAsync(CancellationToken.None);
		}
	}

	protected override void OnItemsCollectionChanged(IList<SectionModuleViewModel<IClientUserSettingsModule>> oldSource, IList<SectionModuleViewModel<IClientUserSettingsModule>> newSource)
	{
		base.OnItemsCollectionChanged(oldSource, newSource);
	}

	public void OnImportsSatisfied()
	{
	}
}
