using System;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Data;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
[Export(typeof(IClientShopViewModel))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class ClientPurchaseViewModel : ViewModel, IPartImportsSatisfiedNotification, IClientShopViewModel
{
	private ICollectionView productGroupsView;

	private ICollectionView productsView;

	private string filter;

	private ProductGroupViewModel currentProductGroup;

	private IExecutionChangedAwareCommand resetFilterCommand;

	private ProductSort productSort;

	[Import]
	public ProductOrderViewModel Order { get; protected set; }

	[Import]
	public GizmoClient Client { get; set; }

	[Import]
	private IViewModelLocatorList<IProductGroupViewModel> ProductGroupLocator { get; set; }

	[Import]
	private IViewModelLocatorList<IProductViewModel> ProductLocator { get; set; }

	public IExecutionChangedAwareCommand ResetFilterCommand
	{
		get
		{
			if (resetFilterCommand == null)
			{
				resetFilterCommand = new SimpleCommand<object, object>(OnCanResetFilter, OnResetFilterCommand);
			}
			return resetFilterCommand;
		}
	}

	public ICollectionView Products
	{
		get
		{
			if (productsView == null)
			{
				ListCollectionView listCollectionView = new ListCollectionView(ProductLocator.ListSource as IList)
				{
					Filter = OnProductFilter
				};
				productsView = listCollectionView;
			}
			return productsView;
		}
	}

	public ICollectionView ProductGroups
	{
		get
		{
			if (productGroupsView == null)
			{
				ListCollectionView listCollectionView = new ListCollectionView(ProductGroupLocator.ListSource as IList)
				{
					Filter = OnProductGroupFilter
				};
				productGroupsView = listCollectionView;
			}
			return productGroupsView;
		}
	}

	public string Filter
	{
		get
		{
			return filter;
		}
		set
		{
			SetProperty(ref filter, value, "Filter");
			CurrentProductGroup = null;
			RaisePropertyChanged("HasFilter");
		}
	}

	public ProductGroupViewModel CurrentProductGroup
	{
		get
		{
			return currentProductGroup;
		}
		set
		{
			SetProperty(ref currentProductGroup, value, "CurrentProductGroup");
			RaisePropertyChanged("CurrentProductGroupId");
			RaisePropertyChanged("HasFilter");
		}
	}

	public int? CurrentProductGroupId => CurrentProductGroup?.ProductGroupId;

	public bool HasFilter
	{
		get
		{
			if (string.IsNullOrWhiteSpace(Filter))
			{
				return CurrentProductGroupId.HasValue;
			}
			return true;
		}
	}

	[IgnorePropertyModification]
	public ProductSort ProductSort
	{
		get
		{
			return productSort;
		}
		set
		{
			SetProperty(ref productSort, value, "ProductSort");
			using (Products.DeferRefresh())
			{
				Products.SortDescriptions.Clear();
				switch (value)
				{
				case ProductSort.Name:
					Products.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
					break;
				case ProductSort.DateAdded:
					Products.SortDescriptions.Add(new SortDescription("AddDate", ListSortDirection.Descending));
					break;
				case ProductSort.Price:
					Products.SortDescriptions.Add(new SortDescription("Price", ListSortDirection.Ascending));
					break;
				case ProductSort.PointsPrice:
					Products.SortDescriptions.Add(new SortDescription("PointsPrice", ListSortDirection.Ascending));
					break;
				}
			}
		}
	}

	IProductOrderViewModel IClientShopViewModel.Order => Order;

	[ImportingConstructor]
	public ClientPurchaseViewModel()
	{
	}

	private bool OnCanResetFilter(object param)
	{
		return HasFilter;
	}

	private void OnResetFilterCommand(object param)
	{
		Filter = null;
		CurrentProductGroup = null;
	}

	private bool OnProductGroupFilter(object obj)
	{
		ProductGroupViewModel model = obj as ProductGroupViewModel;
		if (model != null)
		{
			if (!ProductLocator.EnumerableSource.Where((IProductViewModel product) => product.ProductGroupId == model.ProductGroupId).Any())
			{
				return false;
			}
			return true;
		}
		return false;
	}

	private bool OnProductFilter(object obj)
	{
		string value = Filter;
		int? currentProductGroupId = CurrentProductGroupId;
		if (!currentProductGroupId.HasValue && string.IsNullOrWhiteSpace(value))
		{
			return true;
		}
		if (obj is ProductViewModel productViewModel)
		{
			if (currentProductGroupId.HasValue && productViewModel.ProductGroupId != currentProductGroupId)
			{
				return false;
			}
			if (!string.IsNullOrWhiteSpace(value))
			{
				string name = productViewModel.Name;
				if (!string.IsNullOrWhiteSpace(name))
				{
					return name.IndexOf(value, StringComparison.InvariantCultureIgnoreCase) != -1;
				}
			}
		}
		return true;
	}

	protected override void OnPropertyChanged(object sender, PropertyChangedEventArgsEx args)
	{
		base.OnPropertyChanged(sender, args);
		if (args.PropertyName == "Filter" || args.PropertyName == "CurrentProductGroup")
		{
			DeferrRefresh();
			ResetCommands();
		}
	}

	protected override void OnRefresh()
	{
		base.OnRefresh();
		Products?.Refresh();
	}

	protected override void OnResetCommands()
	{
		base.OnResetCommands();
		ResetFilterCommand?.RaiseCanExecuteChanged();
	}

	public void OnImportsSatisfied()
	{
	}
}
