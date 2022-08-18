using System;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using SharedLib;
using SharedLib.Applications;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
public class AppsViewModel : SelectViewModelBaseOfType<IAppViewModel, NullView>, IPartImportsSatisfiedNotification
{
	private readonly int MAX_APPS_PER_PAGE = 5;

	private readonly int MAX_EXECUTABLES_PER_PAGE = 47;

	private string filter;

	private int? filterCategory;

	private IExecutionChangedAwareCommand resetFilterCommand;

	private RatingFilterType ratingFilter;

	private ApplicationModes accessType;

	private ApplicationModes type;

	private ApplicationModes playerMode;

	private AppSort appSort = AppSort.DateAdded;

	private ICollectionView TOP_APP_CV;

	private ICollectionView RECENTLY_ADDED_CV;

	private ICollectionView TOP_RATED_CV;

	private ICollectionView ACTIVE_EXECUTABLE_CV;

	private ICollectionView QUICK_LAUNCH_EXECUTABLES_CV;

	private ICollectionView MOST_USED_EXECUTABLE_CV;

	[Import]
	private IViewModelLocatorList<IAppExeViewModel> AppExeLocator { get; set; }

	[Import]
	private IViewModelLocatorList<IAppViewModel> AppLocator { get; set; }

	[Import]
	public CategoryFilterViewModel CategoryFilter { get; set; }

	[Import]
	private GizmoClient Client { get; set; }

	public ICollectionView TopUsedApps
	{
		get
		{
			if (TOP_APP_CV == null)
			{
				LimitedListCollectionView limitedListCollectionView = new LimitedListCollectionView(AppLocator.EnumerableSource as IList)
				{
					MaxItems = MAX_APPS_PER_PAGE,
					IsLiveSorting = true,
					IsLiveFiltering = true
				};
				using (limitedListCollectionView.DeferRefresh())
				{
					limitedListCollectionView.LiveFilteringProperties.Add("TotalExecutionTime");
					limitedListCollectionView.LiveFilteringProperties.Add("TotalExecutions");
					limitedListCollectionView.LiveSortingProperties.Add("TotalExecutionTime");
					limitedListCollectionView.LiveSortingProperties.Add("TotalExecutions");
					TOP_APP_CV = limitedListCollectionView;
					TOP_APP_CV.SortDescriptions.Add(new SortDescription("TotalExecutionTime", ListSortDirection.Descending));
					TOP_APP_CV.SortDescriptions.Add(new SortDescription("TotalExecutions", ListSortDirection.Descending));
					TOP_APP_CV.Filter = TopAppsFilter;
				}
			}
			return TOP_APP_CV;
		}
	}

	public ICollectionView RecentlyAddedApps
	{
		get
		{
			if (RECENTLY_ADDED_CV == null)
			{
				LimitedListCollectionView limitedListCollectionView = new LimitedListCollectionView(AppLocator.ListSource as IList)
				{
					MaxItems = MAX_APPS_PER_PAGE,
					IsLiveSorting = true,
					IsLiveFiltering = true
				};
				using (limitedListCollectionView.DeferRefresh())
				{
					limitedListCollectionView.LiveFilteringProperties.Add("AddDate");
					limitedListCollectionView.LiveSortingProperties.Add("AddDate");
					RECENTLY_ADDED_CV = limitedListCollectionView;
					RECENTLY_ADDED_CV.SortDescriptions.Add(new SortDescription("AddDate", ListSortDirection.Descending));
					RECENTLY_ADDED_CV.Filter = RecentlyAddedFilter;
				}
			}
			return RECENTLY_ADDED_CV;
		}
	}

	public ICollectionView TopRatedApps
	{
		get
		{
			if (TOP_RATED_CV == null)
			{
				LimitedListCollectionView limitedListCollectionView = new LimitedListCollectionView(AppLocator.ListSource as IList)
				{
					MaxItems = MAX_APPS_PER_PAGE,
					IsLiveSorting = true,
					IsLiveFiltering = true
				};
				using (limitedListCollectionView.DeferRefresh())
				{
					limitedListCollectionView.LiveFilteringProperties.Add("Rating");
					limitedListCollectionView.LiveSortingProperties.Add("Rating");
					TOP_RATED_CV = limitedListCollectionView;
					TOP_RATED_CV.SortDescriptions.Add(new SortDescription("Rating", ListSortDirection.Descending));
					TOP_RATED_CV.Filter = TopRatedFilter;
				}
			}
			return TOP_RATED_CV;
		}
	}

	public ICollectionView QuickLaunchExecutables
	{
		get
		{
			if (QUICK_LAUNCH_EXECUTABLES_CV == null)
			{
				LimitedListCollectionView limitedListCollectionView = new LimitedListCollectionView(AppExeLocator.ListSource as IList)
				{
					MaxItems = MAX_EXECUTABLES_PER_PAGE,
					IsLiveFiltering = true,
					IsLiveSorting = true
				};
				using (limitedListCollectionView.DeferRefresh())
				{
					limitedListCollectionView.LiveSortingProperties.Add("Caption");
					limitedListCollectionView.LiveFilteringProperties.Add("IsQuickLaunch");
					limitedListCollectionView.LiveFilteringProperties.Add("IsAccessible");
					QUICK_LAUNCH_EXECUTABLES_CV = limitedListCollectionView;
					QUICK_LAUNCH_EXECUTABLES_CV.Filter = QuickLaunchExecutableFilter;
					QUICK_LAUNCH_EXECUTABLES_CV.SortDescriptions.Add(new SortDescription("Caption", ListSortDirection.Ascending));
				}
			}
			return QUICK_LAUNCH_EXECUTABLES_CV;
		}
	}

	public ICollectionView TopExecutables
	{
		get
		{
			if (MOST_USED_EXECUTABLE_CV == null)
			{
				LimitedListCollectionView limitedListCollectionView = new LimitedListCollectionView(AppExeLocator.ListSource as IList)
				{
					MaxItems = MAX_EXECUTABLES_PER_PAGE,
					IsLiveFiltering = true,
					IsLiveSorting = true
				};
				using (limitedListCollectionView.DeferRefresh())
				{
					limitedListCollectionView.LiveSortingProperties.Add("TotalUserTime");
					limitedListCollectionView.LiveFilteringProperties.Add("TotalUserTime");
					limitedListCollectionView.LiveFilteringProperties.Add("IsAccessible");
					MOST_USED_EXECUTABLE_CV = limitedListCollectionView;
					MOST_USED_EXECUTABLE_CV.SortDescriptions.Add(new SortDescription("TotalUserTime", ListSortDirection.Descending));
					MOST_USED_EXECUTABLE_CV.Filter = TopExecutableFilter;
				}
			}
			return MOST_USED_EXECUTABLE_CV;
		}
	}

	public ICollectionView ActiveExecutables
	{
		get
		{
			if (ACTIVE_EXECUTABLE_CV == null)
			{
				ListCollectionView listCollectionView = new ListCollectionView(AppExeLocator.ListSource as IList)
				{
					IsLiveFiltering = true
				};
				using (listCollectionView.DeferRefresh())
				{
					listCollectionView.LiveFilteringProperties.Add("IsActive");
					listCollectionView.LiveFilteringProperties.Add("IsReady");
					listCollectionView.LiveFilteringProperties.Add("IsAccessible");
					ACTIVE_EXECUTABLE_CV = listCollectionView;
					ACTIVE_EXECUTABLE_CV.Filter = ActiveExecutableFilter;
				}
			}
			return ACTIVE_EXECUTABLE_CV;
		}
	}

	public IExecutionChangedAwareCommand ResetFilterCommand
	{
		get
		{
			if (resetFilterCommand == null)
			{
				resetFilterCommand = new SimpleCommand<object, object>((object param) => true, OnResetFilterCommand);
			}
			return resetFilterCommand;
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
			RaisePropertyChanged("HasFilter");
		}
	}

	public int? FilterCategory
	{
		get
		{
			return filterCategory;
		}
		set
		{
			SetProperty(ref filterCategory, value, "FilterCategory");
			RaisePropertyChanged("HasFilter");
		}
	}

	public RatingFilterType RatingFilter
	{
		get
		{
			return ratingFilter;
		}
		set
		{
			SetProperty(ref ratingFilter, value, "RatingFilter");
			RaisePropertyChanged("HasFilter");
		}
	}

	public ApplicationModes AccessType
	{
		get
		{
			return accessType;
		}
		set
		{
			SetProperty(ref accessType, value, "AccessType");
			RaisePropertyChanged("HasFilter");
		}
	}

	public ApplicationModes Type
	{
		get
		{
			return type;
		}
		set
		{
			SetProperty(ref type, value, "Type");
			RaisePropertyChanged("HasFilter");
		}
	}

	public ApplicationModes PlayerMode
	{
		get
		{
			return playerMode;
		}
		set
		{
			SetProperty(ref playerMode, value, "PlayerMode");
			RaisePropertyChanged("HasFilter");
		}
	}

	[IgnorePropertyModification]
	public bool HasFilter
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Filter))
			{
				return true;
			}
			if (FilterCategory.HasValue)
			{
				return true;
			}
			if (RatingFilter != 0)
			{
				return true;
			}
			if (AccessType != 0)
			{
				return true;
			}
			if (Type != 0)
			{
				return true;
			}
			if (PlayerMode != 0)
			{
				return true;
			}
			return false;
		}
	}

	[IgnorePropertyModification]
	public AppSort AppSort
	{
		get
		{
			return appSort;
		}
		set
		{
			SetProperty(ref appSort, value, "AppSort");
			using (ItemsView.DeferRefresh())
			{
				ItemsView.SortDescriptions.Clear();
				switch (value)
				{
				case AppSort.Title:
					ItemsView.SortDescriptions.Add(new SortDescription("Title", ListSortDirection.Ascending));
					break;
				case AppSort.Rating:
					ItemsView.SortDescriptions.Add(new SortDescription("Rating", ListSortDirection.Descending));
					break;
				case AppSort.Use:
					ItemsView.SortDescriptions.Add(new SortDescription("TotalExecutionTime", ListSortDirection.Descending));
					break;
				case AppSort.ReleaseDate:
					ItemsView.SortDescriptions.Add(new SortDescription("ReleaseDate", ListSortDirection.Descending));
					break;
				case AppSort.DateAdded:
					ItemsView.SortDescriptions.Add(new SortDescription("AddDate", ListSortDirection.Descending));
					break;
				}
			}
		}
	}

	[ImportingConstructor]
	public AppsViewModel()
	{
		base.DefaultRefreshDelay = TimeSpan.FromMilliseconds(500.0);
	}

	protected override void OnItemsCollectionViewChanged(ICollectionView previousView, ICollectionView newView)
	{
		base.OnItemsCollectionViewChanged(previousView, newView);
		if (newView != null)
		{
			using (ItemsView.DeferRefresh())
			{
				ItemsView.SortDescriptions.Add(new SortDescription("AddDate", ListSortDirection.Descending));
				base.LiveShapingView.IsLiveSorting = true;
				base.LiveShapingView.IsLiveFiltering = true;
				base.LiveShapingView.LiveFilteringProperties.Add("CategoryId");
				base.LiveShapingView.LiveSortingProperties.Add("Title");
			}
		}
	}

	protected override void OnPropertyChanged(object sender, PropertyChangedEventArgsEx args)
	{
		base.OnPropertyChanged(sender, args);
		if (!IsIgnoredProperty(args.PropertyName) && (args.PropertyName == "Filter" || args.PropertyName == "FilterCategory" || args.PropertyName == "RatingFilter" || args.PropertyName == "PlayerMode" || args.PropertyName == "AccessType" || args.PropertyName == "Type"))
		{
			DeferrRefresh();
			if (args.PropertyName != "FilterCategory")
			{
				CategoryFilter.DeferrRefresh();
			}
		}
	}

	protected override bool OnFilter(object obj)
	{
		if (obj is AppViewModel appViewModel)
		{
			if (!BaseAppFilter(appViewModel))
			{
				return false;
			}
			if (FilterCategory.HasValue && appViewModel.CategoryId != FilterCategory)
			{
				return false;
			}
		}
		return base.OnFilter(obj);
	}

	protected override void OnRefresh()
	{
		try
		{
			base.OnRefresh();
			TOP_APP_CV?.Refresh();
			TOP_RATED_CV?.Refresh();
			RECENTLY_ADDED_CV?.Refresh();
			ACTIVE_EXECUTABLE_CV?.Refresh();
			QUICK_LAUNCH_EXECUTABLES_CV?.Refresh();
			MOST_USED_EXECUTABLE_CV?.Refresh();
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnRefresh", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\AppsViewModel.cs", 481);
		}
	}

	private void OnResetFilterCommand(object param)
	{
		Filter = null;
		CategoryFilter.SelectedItem = null;
		RatingFilter = RatingFilterType.Any;
		Type = ApplicationModes.DefaultMode;
		PlayerMode = ApplicationModes.DefaultMode;
		AccessType = ApplicationModes.DefaultMode;
		CategoryFilter.Items.ToList().ForEach(delegate(ICategoryDisplayViewModel item)
		{
			item.IsExpanded = false;
			item.IsSelected = false;
		});
	}

	private void OnAppProfilesChange(object sender, ProfilesChangeEventArgs e)
	{
		if (!e.IsInitial)
		{
			DeferrRefresh();
			CategoryFilter.DeferrRefresh();
		}
	}

	private void OnCategoryFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		FilterCategory = e.AddedItems.Cast<ICategoryDisplayViewModel>().FirstOrDefault()?.CategoryId;
	}

	private bool TopAppsFilter(object obj)
	{
		if (obj is AppViewModel appViewModel)
		{
			if (!AppGroupFilter(appViewModel))
			{
				return false;
			}
			if (appViewModel.TotalExecutionTime <= 0.0)
			{
				return false;
			}
		}
		return true;
	}

	private bool TopRatedFilter(object obj)
	{
		if (obj is AppViewModel appViewModel)
		{
			if (!AppGroupFilter(appViewModel))
			{
				return false;
			}
			if (appViewModel.Rating <= 0)
			{
				return false;
			}
		}
		return true;
	}

	private bool RecentlyAddedFilter(object obj)
	{
		if (obj is AppViewModel app && !AppGroupFilter(app))
		{
			return false;
		}
		return true;
	}

	private bool BaseAppFilter(IAppViewModel appModel)
	{
		if (appModel == null)
		{
			return false;
		}
		if (!AppGroupFilter(appModel))
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(Filter))
		{
			if (appModel.Title.IndexOf(Filter, StringComparison.InvariantCultureIgnoreCase) != -1)
			{
				return true;
			}
			if (appModel.ExecutableTitles.Any((string exeName) => exeName.IndexOf(Filter, StringComparison.InvariantCultureIgnoreCase) != -1))
			{
				return true;
			}
			return false;
		}
		if (RatingFilter != 0)
		{
			switch (RatingFilter)
			{
			case RatingFilterType.Unrated:
				if (appModel.Rating != 0)
				{
					return false;
				}
				break;
			default:
			{
				if (appModel.Rating == 0)
				{
					return false;
				}
				int num = (int)RatingFilter;
				if (appModel.Rating < num)
				{
					return false;
				}
				break;
			}
			case RatingFilterType.Any:
				break;
			}
		}
		if (AccessType != 0 && !appModel.ExeModes.Any((ApplicationModes FLAG) => AccessType.HasFlag(FLAG)))
		{
			return false;
		}
		if (Type != 0 && !appModel.ExeModes.Any((ApplicationModes FLAG) => Type.HasFlag(FLAG)))
		{
			return false;
		}
		if (PlayerMode != 0 && !appModel.ExeModes.Any((ApplicationModes FLAG) => PlayerMode.HasFlag(FLAG)))
		{
			return false;
		}
		return true;
	}

	private bool AppGroupFilter(IAppViewModel app)
	{
		if (app == null)
		{
			return false;
		}
		IAppProfile currentAppProfile = Client.CurrentAppProfile;
		if (currentAppProfile == null)
		{
			return true;
		}
		return currentAppProfile.Profiles?.Contains(app.AppId) != false;
	}

	private bool ActiveExecutableFilter(object obj)
	{
		if (obj is AppExeViewModel appExeViewModel)
		{
			if (!appExeViewModel.IsAccessible || !appExeViewModel.IsActive)
			{
				if (appExeViewModel.IsReady)
				{
					return AppGroupFilter(appExeViewModel.App);
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private bool QuickLaunchExecutableFilter(object obj)
	{
		if (obj is AppExeViewModel appExeViewModel)
		{
			if (appExeViewModel.IsAccessible && appExeViewModel.IsQuickLaunch)
			{
				return AppGroupFilter(appExeViewModel.App);
			}
			return false;
		}
		return false;
	}

	private bool TopExecutableFilter(object obj)
	{
		if (obj is AppExeViewModel appExeViewModel)
		{
			if (appExeViewModel.IsAccessible && appExeViewModel.TotalUserTime > 0.0)
			{
				return AppGroupFilter(appExeViewModel.App);
			}
			return false;
		}
		return false;
	}

	public bool PassBaseFilter(IAppViewModel appViewModel)
	{
		return BaseAppFilter(appViewModel);
	}

	public void OnImportsSatisfied()
	{
		CategoryFilter.SelectionChanged -= OnCategoryFilterSelectionChanged;
		CategoryFilter.SelectionChanged += OnCategoryFilterSelectionChanged;
		Client.AppProfilesChange -= OnAppProfilesChange;
		Client.AppProfilesChange += OnAppProfilesChange;
		SetItemSource(AppLocator.ListSource);
	}
}
