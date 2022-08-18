using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using Client.Views;
using CoreLib;
using GizmoDALV2.Entities;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class AppViewModel : ExecuteViewModelBase, IAppViewModel, IPartImportsSatisfiedNotification
{
	private int rating;

	private int userRating;

	private int totalRates;

	private int totalExecutions;

	private int id;

	private int cateoryId;

	private double totalExecutionTime;

	private IExecutionChangedAwareCommand detailsCommand;

	private IExecutionChangedAwareCommand rateCommand;

	private DateTime? releaseDate;

	private int? publisherId;

	private int? developerId;

	private int? defaultExecutableId;

	private NotifyTaskCompletion<byte[]> imageTask;

	private NotifyTaskCompletion<AppLinksViewModel> appLinksTask;

	private string title;

	private string description;

	private DateTime addDate;

	private ListCollectionView APP_EXE_CV;

	private ListCollectionView APP_EXE_ACTIVE_CV;

	private int ageRating;

	private AgeRatingType ageRatingType;

	private AppExeViewModel defaultExecutable;

	[Import]
	private IViewModelLocatorList<IAppExeViewModel> AppExeLocator { get; set; }

	[Import]
	private IViewModelLocatorList<IAppEnterpriseDisplayViewModel> EnterpriseLocator { get; set; }

	[Import]
	private IViewModelLocatorList<ICategoryDisplayViewModel> CategoryLocator { get; set; }

	[Import]
	private IShellWindow Shell { get; set; }

	[Import]
	private GizmoClient Client { get; set; }

	public IExecutionChangedAwareCommand DetailsCommand
	{
		get
		{
			if (detailsCommand == null)
			{
				detailsCommand = new SimpleCommand<object, object>(OnCanDetailsCommand, OnDetailsCommand);
			}
			return detailsCommand;
		}
	}

	public IExecutionChangedAwareCommand RateCommand
	{
		get
		{
			if (rateCommand == null)
			{
				rateCommand = new SimpleCommand<object, object>(OnCanRateCommand, OnRateCommand);
			}
			return rateCommand;
		}
	}

	public ListCollectionView Executables
	{
		get
		{
			if (Client.DataInitialized && APP_EXE_CV == null)
			{
				IList list = AppExeLocator.ListSource as IList;
				APP_EXE_CV = new ListCollectionView(list)
				{
					Filter = ExecutableFilter
				};
				APP_EXE_CV.SortDescriptions.Add(new SortDescription("DisplayOrder", ListSortDirection.Ascending));
			}
			return APP_EXE_CV;
		}
	}

	public ListCollectionView ActiveExecutables
	{
		get
		{
			if (Client.DataInitialized && APP_EXE_ACTIVE_CV == null)
			{
				IList list = AppExeLocator.ListSource as IList;
				APP_EXE_ACTIVE_CV = new ListCollectionView(list)
				{
					IsLiveFiltering = true,
					Filter = ActiveExecutablesFilter
				};
				APP_EXE_ACTIVE_CV.LiveFilteringProperties.Add("IsActive");
			}
			return APP_EXE_ACTIVE_CV;
		}
	}

	[IgnorePropertyModification]
	public int AppId
	{
		get
		{
			return id;
		}
		set
		{
			SetProperty(ref id, value, "AppId");
		}
	}

	[IgnorePropertyModification]
	public string Title
	{
		get
		{
			return title;
		}
		set
		{
			SetProperty(ref title, value, "Title");
		}
	}

	[IgnorePropertyModification]
	public string Description
	{
		get
		{
			return description;
		}
		set
		{
			SetProperty(ref description, value, "Description");
		}
	}

	[IgnorePropertyModification]
	public int Rating
	{
		get
		{
			return rating;
		}
		set
		{
			SetProperty(ref rating, value, "Rating");
		}
	}

	[IgnorePropertyModification]
	public int UserRating
	{
		get
		{
			return userRating;
		}
		set
		{
			SetProperty(ref userRating, value, "UserRating");
			OnSetUserRating(value);
		}
	}

	[IgnorePropertyModification]
	internal int UserRatingInternal
	{
		get
		{
			return userRating;
		}
		set
		{
			userRating = value;
			RaisePropertyChanged("UserRating");
		}
	}

	[IgnorePropertyModification]
	public int TotalRates
	{
		get
		{
			return totalRates;
		}
		set
		{
			SetProperty(ref totalRates, value, "TotalRates");
		}
	}

	[IgnorePropertyModification]
	public DateTime? ReleaseDate
	{
		get
		{
			return releaseDate;
		}
		set
		{
			SetProperty(ref releaseDate, value, "ReleaseDate");
		}
	}

	[IgnorePropertyModification]
	public DateTime AddDate
	{
		get
		{
			return addDate;
		}
		set
		{
			SetProperty(ref addDate, value, "AddDate");
		}
	}

	[IgnorePropertyModification]
	public int CategoryId
	{
		get
		{
			return cateoryId;
		}
		set
		{
			SetProperty(ref cateoryId, value, "CategoryId");
			RaisePropertyChanged("Category");
		}
	}

	[IgnorePropertyModification]
	public int? DeveloperId
	{
		get
		{
			return developerId;
		}
		set
		{
			SetProperty(ref developerId, value, "DeveloperId");
			RaisePropertyChanged("Developer");
		}
	}

	[IgnorePropertyModification]
	public int? PublisherId
	{
		get
		{
			return publisherId;
		}
		set
		{
			SetProperty(ref publisherId, value, "PublisherId");
			RaisePropertyChanged("Publisher");
		}
	}

	[IgnorePropertyModification]
	public ICategoryDisplayViewModel Category => CategoryLocator.TryGetViewModel(CategoryId);

	[IgnorePropertyModification]
	public IAppEnterpriseDisplayViewModel Publisher
	{
		get
		{
			if (PublisherId.HasValue)
			{
				return EnterpriseLocator.TryGetViewModel(PublisherId.Value);
			}
			return null;
		}
	}

	[IgnorePropertyModification]
	public IAppEnterpriseDisplayViewModel Developer
	{
		get
		{
			if (DeveloperId.HasValue)
			{
				return EnterpriseLocator.TryGetViewModel(DeveloperId.Value);
			}
			return null;
		}
	}

	[IgnorePropertyModification]
	public int TotalExecutions
	{
		get
		{
			return totalExecutions;
		}
		set
		{
			SetProperty(ref totalExecutions, value, "TotalExecutions");
		}
	}

	[IgnorePropertyModification]
	public double TotalExecutionTime
	{
		get
		{
			return totalExecutionTime;
		}
		set
		{
			SetProperty(ref totalExecutionTime, value, "TotalExecutionTime");
		}
	}

	[IgnorePropertyModification]
	public NotifyTaskCompletion<byte[]> ImageData
	{
		get
		{
			if (imageTask == null)
			{
				imageTask = new NotifyTaskCompletion<byte[]>(GetImageHandler());
			}
			return imageTask;
		}
		internal set
		{
			SetProperty(ref imageTask, value, "ImageData");
		}
	}

	[IgnorePropertyModification]
	public IEnumerable<string> ExecutableTitles => (from exe in Executables?.OfType<AppExeViewModel>()
		select exe.Caption).ToList() ?? new List<string>();

	[IgnorePropertyModification]
	public IEnumerable<ApplicationModes> ExeModes => (from exe in Executables?.OfType<AppExeViewModel>()
		where exe.IsAccessible
		select exe.Modes).SelectMany((ApplicationModes modes) => modes.GetIndividualFlags()).Distinct().Cast<ApplicationModes>() ?? new List<ApplicationModes>();

	[IgnorePropertyModification]
	public int? DefaultExecutableId
	{
		get
		{
			return defaultExecutableId;
		}
		set
		{
			SetProperty(ref defaultExecutableId, value, "DefaultExecutableId");
			RaisePropertyChanged("DefaultExecutable");
		}
	}

	[IgnorePropertyModification]
	public AppExeViewModel DefaultExecutable
	{
		get
		{
			if (defaultExecutable == null)
			{
				IEnumerable<AppExeViewModel> enumerable = Executables?.OfType<AppExeViewModel>();
				if (enumerable != null)
				{
					if (DefaultExecutableId.HasValue)
					{
						defaultExecutable = enumerable.Where((AppExeViewModel exe) => exe.ExeId == DefaultExecutableId).FirstOrDefault();
					}
					if (defaultExecutable == null)
					{
						defaultExecutable = enumerable.OrderBy((AppExeViewModel APP_EXE) => APP_EXE.DisplayOrder).FirstOrDefault();
					}
				}
			}
			return defaultExecutable;
		}
	}

	[IgnorePropertyModification]
	public int AgeRating
	{
		get
		{
			return ageRating;
		}
		set
		{
			SetProperty(ref ageRating, value, "AgeRating");
		}
	}

	[IgnorePropertyModification]
	public AgeRatingType AgeRatingType
	{
		get
		{
			return ageRatingType;
		}
		set
		{
			SetProperty(ref ageRatingType, value, "AgeRatingType");
		}
	}

	[IgnorePropertyModification]
	public NotifyTaskCompletion<AppLinksViewModel> AppLinks
	{
		get
		{
			if (appLinksTask == null)
			{
				appLinksTask = new NotifyTaskCompletion<AppLinksViewModel>(GetAppLinksHandler());
			}
			return appLinksTask;
		}
		internal set
		{
			SetProperty(ref appLinksTask, value, "AppLinks");
		}
	}

	[IgnorePropertyModification]
	IEnumerable IAppViewModel.Executables => Executables;

	[ImportingConstructor]
	public AppViewModel()
	{
	}

	protected override bool OnCanExecuteCommand(object param)
	{
		return true;
	}

	protected override async void OnExecuteCommand(object param)
	{
		await Execute(param);
	}

	private bool OnCanDetailsCommand(object param)
	{
		return true;
	}

	private async void OnDetailsCommand(object param)
	{
		if (Client.HideAppInfo)
		{
			await Execute(param);
			return;
		}
		try
		{
			IAppDetailView exportedValue = Client.GetExportedValue<IAppDetailView>();
			exportedValue.DataContext = this;
			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
			try
			{
				await Shell.ShowOverlayAsync(exportedValue, cancellationTokenSource.Token);
			}
			catch (OperationCanceledException)
			{
			}
		}
		catch (Exception ex2)
		{
			Client.TraceWrite(ex2, "OnDetailsCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\AppViewModel.cs", 463);
		}
	}

	private bool OnCanRateCommand(object param)
	{
		return true;
	}

	private void OnRateCommand(object param)
	{
	}

	private async Task Execute(object param)
	{
		_ = 2;
		try
		{
			var executables = (from appExe in Executables?.OfType<AppExeViewModel>()
				where appExe.IsAccessible
				orderby appExe.DisplayOrder
				select new
				{
					AppExe = appExe,
					CheckTask = appExe.ValidateExecutableFileExist()
				}).ToList();
			await Task.WhenAll(executables.Select(appExe => appExe.CheckTask).ToList());
			if (executables.Where(a => a.CheckTask.Result).Count() > 1)
			{
				IExeSelectorView exportedValue = Client.GetExportedValue<IExeSelectorView>();
				exportedValue.DataContext = this;
				CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
				try
				{
					await Shell.ShowOverlayAsync(exportedValue, cancellationTokenSource.Token);
				}
				catch (OperationCanceledException)
				{
				}
				return;
			}
			AppExeViewModel defaultExe = DefaultExecutable;
			bool flag = defaultExe == null || !defaultExe.IsAccessible;
			if (!flag)
			{
				flag = !(await defaultExe.ValidateExecutableFileExist());
			}
			if (flag)
			{
				defaultExe = (from appExe in executables
					where appExe.CheckTask.Result
					select appExe.AppExe).FirstOrDefault();
			}
			if (defaultExe != null && defaultExe.ExecuteCommand.CanExecute(param))
			{
				defaultExe.ExecuteCommand.Execute(param);
			}
		}
		catch (Exception ex2)
		{
			Client.TraceWrite(ex2, "Execute", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\AppViewModel.cs", 526);
		}
	}

	private async Task<byte[]> GetImageHandler()
	{
		return await Client.TryGetImageDataAsync(AppId, ImageType.Application).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task<AppLinksViewModel> GetAppLinksHandler()
	{
		IEnumerable<AppLink> source = await Client.AppLinkGetAsync(AppId);
		AppLinksViewModel exportedValue = Client.GetExportedValue<AppLinksViewModel>();
		IEnumerable<AppLinkViewModel> list = source.Select(delegate(AppLink appLink)
		{
			AppLinkViewModel exportedValue2 = Client.GetExportedValue<AppLinkViewModel>();
			exportedValue2.Caption = appLink.Caption;
			exportedValue2.Description = appLink.Description;
			exportedValue2.DisplayOrder = appLink.DisplayOrder;
			exportedValue2.Url = appLink.Url;
			return exportedValue2;
		});
		exportedValue.InitFrom(list);
		return exportedValue;
	}

	private void OnSetUserRating(int value)
	{
		Client.AppRatingSetAsync(AppId, value).ContinueWith(delegate(Task t)
		{
			if (t.IsFaulted)
			{
				Client.TraceWrite(t.Exception, "OnSetUserRating", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\AppViewModel.cs", 561);
			}
		}, TaskContinuationOptions.OnlyOnFaulted);
	}

	private bool ExecutableFilter(object obj)
	{
		if (obj is AppExeViewModel appExeViewModel)
		{
			if (!appExeViewModel.IsAccessible)
			{
				return false;
			}
			return appExeViewModel.AppId == AppId;
		}
		return false;
	}

	private bool ActiveExecutablesFilter(object obj)
	{
		if (!ExecutableFilter(obj))
		{
			return false;
		}
		if (obj is AppExeViewModel appExeViewModel)
		{
			return appExeViewModel.IsActive;
		}
		return false;
	}

	public void OnImportsSatisfied()
	{
	}
}
