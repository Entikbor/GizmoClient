using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using SharedLib;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class CategoryDisplayViewModel : SelectViewModelBaseOfType<ICategoryDisplayViewModel, NullView>, ICategoryDisplayViewModel, IPartImportsSatisfiedNotification
{
	private bool isSelected;

	private bool isExpanded;

	private string name;

	private int categoryId;

	private int? parentId;

	[Import]
	private IViewModelLocatorList<ICategoryDisplayViewModel> CategoryLocator { get; set; }

	[Import]
	private IViewModelLocatorList<IAppViewModel> AppLocator { get; set; }

	[Import]
	private AppsViewModel AppsViewModel { get; set; }

	[Import]
	private GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public string Name
	{
		get
		{
			return name;
		}
		set
		{
			SetProperty(ref name, value, "Name");
		}
	}

	[IgnorePropertyModification]
	public int? ParentId
	{
		get
		{
			return parentId;
		}
		set
		{
			SetProperty(ref parentId, value, "ParentId");
		}
	}

	[IgnorePropertyModification]
	public int CategoryId
	{
		get
		{
			return categoryId;
		}
		set
		{
			SetProperty(ref categoryId, value, "CategoryId");
		}
	}

	[IgnorePropertyModification]
	public bool IsExpanded
	{
		get
		{
			return isExpanded;
		}
		set
		{
			SetProperty(ref isExpanded, value, "IsExpanded");
		}
	}

	[IgnorePropertyModification]
	public bool IsSelected
	{
		get
		{
			return isSelected;
		}
		set
		{
			SetProperty(ref isSelected, value, "IsSelected");
		}
	}

	[IgnorePropertyModification]
	public bool HasApps
	{
		get
		{
			if (!Client.DataInitialized)
			{
				return false;
			}
			return AppLocator.EnumerableSource.Where((IAppViewModel app) => app.CategoryId == CategoryId && AppsViewModel.PassBaseFilter(app)).Any();
		}
	}

	[IgnorePropertyModification]
	public bool HasSubcategoryWithApps
	{
		get
		{
			if (!Client.DataInitialized)
			{
				return false;
			}
			return (from cat in CategoryLocator.EnumerableSource
				where cat.ParentId == CategoryId
				where cat.HasApps || cat.HasSubcategoryWithApps
				select cat).Any();
		}
	}

	[ImportingConstructor]
	public CategoryDisplayViewModel()
	{
	}

	protected override bool OnFilter(object obj)
	{
		if (obj is CategoryDisplayViewModel categoryDisplayViewModel)
		{
			if (categoryDisplayViewModel.ParentId != CategoryId)
			{
				return false;
			}
			if (!categoryDisplayViewModel.HasApps)
			{
				return categoryDisplayViewModel.HasSubcategoryWithApps;
			}
			return true;
		}
		return false;
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

	public void OnImportsSatisfied()
	{
		SetItemSource(CategoryLocator.ListSource);
	}
}
