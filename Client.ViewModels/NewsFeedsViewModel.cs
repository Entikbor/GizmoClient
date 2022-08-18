using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
public class NewsFeedsViewModel : ItemsListViewModelBaseOfType<INewsViewModel, NullView>, IPartImportsSatisfiedNotification
{
	private int selectedIndex;

	private Timer ROTATE_TIMER;

	private readonly object ROTATE_TIMER_LOCK = new object();

	private readonly int ROTATE_SPAN = 10000;

	private IExecutionChangedAwareCommand moveNextCommand;

	private IExecutionChangedAwareCommand movePreviousCommand;

	private IExecutionChangedAwareCommand mouseCommand;

	private int filteredItems;

	[Import]
	private GizmoClient Client { get; set; }

	[Import]
	private IViewModelLocatorList<INewsViewModel> Locator { get; set; }

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand MoveNextCommand
	{
		get
		{
			if (moveNextCommand == null)
			{
				moveNextCommand = new SimpleCommand<object, object>((object o) => true, OnMoveNextCommand);
			}
			return moveNextCommand;
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand MovePreviousCommand
	{
		get
		{
			if (movePreviousCommand == null)
			{
				movePreviousCommand = new SimpleCommand<object, object>((object o) => true, OnMovePreviousCommand);
			}
			return movePreviousCommand;
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand MouseCommand
	{
		get
		{
			if (mouseCommand == null)
			{
				mouseCommand = new SimpleCommand<object, object>((object o) => true, delegate(object o)
				{
					if (o is MouseEventArgs mouseEventArgs)
					{
						if (mouseEventArgs.RoutedEvent == Mouse.MouseLeaveEvent)
						{
							StartRotation(ROTATE_SPAN);
						}
						else if (mouseEventArgs.RoutedEvent == Mouse.MouseEnterEvent)
						{
							StopRotation();
						}
					}
				});
			}
			return mouseCommand;
		}
	}

	[IgnorePropertyModification]
	public int SelectedIndex
	{
		get
		{
			return selectedIndex;
		}
		set
		{
			SetProperty(ref selectedIndex, value, "SelectedIndex");
		}
	}

	[IgnorePropertyModification]
	public int TotalFilteredItems
	{
		get
		{
			return filteredItems;
		}
		set
		{
			SetProperty(ref filteredItems, value, "TotalFilteredItems");
		}
	}

	private void OnMoveNextCommand(object param)
	{
		RotationChange();
		if (base.SelectedItem is NewsViewModel newsViewModel)
		{
			newsViewModel.StopMedia();
		}
		int itemsViewCount = base.ItemsViewCount;
		int num = SelectedIndex + 1;
		SelectedIndex = ((num < itemsViewCount) ? num : 0);
	}

	private void OnMovePreviousCommand(object param)
	{
		RotationChange();
		if (base.SelectedItem is NewsViewModel newsViewModel)
		{
			newsViewModel.StopMedia();
		}
		int itemsViewCount = base.ItemsViewCount;
		int num = SelectedIndex - 1;
		SelectedIndex = ((num < 0) ? (itemsViewCount - 1) : num);
	}

	private void RotationChange()
	{
		ROTATE_TIMER?.Change(ROTATE_SPAN, ROTATE_SPAN);
	}

	private void StartRotation(int dueTime = 0)
	{
		lock (ROTATE_TIMER_LOCK)
		{
			ROTATE_TIMER?.Dispose();
			ROTATE_TIMER = new Timer(OnRotateCallBack, null, dueTime, ROTATE_SPAN);
		}
	}

	private void StopRotation()
	{
		lock (ROTATE_TIMER_LOCK)
		{
			ROTATE_TIMER?.Dispose();
			ROTATE_TIMER = null;
		}
	}

	private void OnRotateCallBack(object state)
	{
		if (!Monitor.TryEnter(ROTATE_TIMER_LOCK))
		{
			return;
		}
		try
		{
			int totalFilteredItems = TotalFilteredItems;
			int num = SelectedIndex;
			if (totalFilteredItems <= 0)
			{
				return;
			}
			if (base.SelectedItem is NewsViewModel newsViewModel)
			{
				MediaVideoViewModel mediaModel = newsViewModel.MediaModel;
				if (mediaModel != null && mediaModel.IsViewAttached)
				{
					return;
				}
				newsViewModel.StopMedia();
			}
			int num2 = num + 1;
			SelectedIndex = ((num2 < totalFilteredItems) ? num2 : 0);
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnRotateCallBack", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\NewsFeedsViewModel.cs", 197);
		}
		finally
		{
			Monitor.Exit(ROTATE_TIMER_LOCK);
		}
	}

	private void OnItemsViewINotifyCollectionChangedCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		TotalFilteredItems = base.ItemsViewCount;
	}

	private void OnUserLoginStateChange(object sender, UserEventArgs e)
	{
		if (e.State == LoginState.LoginCompleted)
		{
			StartRotation(ROTATE_SPAN);
		}
		else if (e.State == LoginState.LoggingOut)
		{
			Items.OfType<NewsViewModel>().ToList().ForEach(delegate(NewsViewModel ITEM)
			{
				ITEM.StopMedia();
			});
		}
		else if (e.State == LoginState.LoggedOut)
		{
			StopRotation();
		}
	}

	protected override bool OnFilter(object obj)
	{
		if (obj is NewsViewModel newsViewModel)
		{
			DateTime now = DateTime.Now;
			DateTime? startDate = newsViewModel.StartDate;
			if (now < startDate)
			{
				return false;
			}
			now = DateTime.Now;
			startDate = newsViewModel.EndDate;
			if (now >= startDate)
			{
				return false;
			}
		}
		return base.OnFilter(obj);
	}

	protected override void OnItemsCollectionViewChanged(ICollectionView previousView, ICollectionView newView)
	{
		base.OnItemsCollectionViewChanged(previousView, newView);
		if (newView != null)
		{
			base.ItemsViewINotifyCollectionChanged.CollectionChanged += OnItemsViewINotifyCollectionChangedCollectionChanged;
			using (ItemsView.DeferRefresh())
			{
				ItemsView.SortDescriptions.Add(new SortDescription("CreatedTime", ListSortDirection.Descending));
				base.LiveShapingView.IsLiveFiltering = true;
				base.LiveShapingView.LiveFilteringProperties.Add("StartDate");
				base.LiveShapingView.LiveFilteringProperties.Add("EndDate");
			}
		}
	}

	protected override async void OnPropertyChanged(object sender, PropertyChangedEventArgsEx args)
	{
		base.OnPropertyChanged(sender, args);
		if (!(args.PropertyName == "SelectedItem"))
		{
			return;
		}
		try
		{
			if (base.SelectedItem is FeedSourceViewModel feedSourceViewModel)
			{
				await feedSourceViewModel.EnumerateAsync();
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnPropertyChanged", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\NewsFeedsViewModel.cs", 281);
		}
	}

	public void OnImportsSatisfied()
	{
		Client.LoginStateChange += OnUserLoginStateChange;
		if (Client.IsUserLoggedIn)
		{
			StartRotation();
		}
		SetItemSource(Locator.ListSource);
	}
}
