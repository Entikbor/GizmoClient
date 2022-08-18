using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.Shared)]
[Export(typeof(ISelectViewModelOfType<ICategoryDisplayViewModel>))]
public class CategoryFilterViewModel : SelectViewModelBaseOfType<ICategoryDisplayViewModel, NullView>, IPartImportsSatisfiedNotification
{
	[Import]
	private IViewModelLocatorList<ICategoryDisplayViewModel> CategoryLocator { get; set; }

	[ImportingConstructor]
	public CategoryFilterViewModel()
	{
	}

	protected override bool OnFilter(object obj)
	{
		if (obj is CategoryDisplayViewModel categoryDisplayViewModel)
		{
			if (categoryDisplayViewModel.ParentId.HasValue)
			{
				return false;
			}
			if (categoryDisplayViewModel.HasApps || categoryDisplayViewModel.HasSubcategoryWithApps)
			{
				return true;
			}
			return false;
		}
		return base.OnFilter(obj);
	}

	protected override void OnItemsCollectionViewChanged(ICollectionView previousView, ICollectionView newView)
	{
		base.OnItemsCollectionViewChanged(previousView, newView);
		if (newView != null)
		{
			using (ItemsView.DeferRefresh())
			{
				base.LiveShapingView.IsLiveSorting = true;
				base.LiveShapingView.LiveSortingProperties.Add("Name");
				ItemsView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
			}
		}
	}

	protected override void OnRefresh()
	{
		base.OnRefresh();
		foreach (CategoryDisplayViewModel item in ItemsView.OfType<CategoryDisplayViewModel>())
		{
			item.Refresh();
		}
	}

	public void OnImportsSatisfied()
	{
		SetItemSource(CategoryLocator.ListSource);
	}
}
