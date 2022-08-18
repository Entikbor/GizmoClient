using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Net;
using System.Net.Cache;
using System.Threading;
using System.Threading.Tasks;
using SharedLib;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class NewsViewModel : ExecuteViewModelBase, INewsViewModel
{
	private int id;

	private string data;

	private string url;

	private string mediaUrl;

	private string title;

	private DateTime? startDate;

	private DateTime? endDate;

	private DateTime? createdTime;

	private NotifyTaskCompletion<byte[]> imageTask;

	private NotifyTaskCompletion<MediaSourceType> mediaTypeTask;

	private bool hasMedia;

	private MediaSourceType mediaType;

	private IExecutionChangedAwareCommand executeMediaCommand;

	private bool inMediaMode;

	private MediaVideoViewModel mediaViewModel;

	[Import]
	private GizmoClient Client { get; set; }

	[Import]
	public IShellWindow Shell { get; set; }

	[IgnorePropertyModification]
	public int Id
	{
		get
		{
			return id;
		}
		set
		{
			SetProperty(ref id, value, "Id");
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
	public string Data
	{
		get
		{
			return data;
		}
		set
		{
			SetProperty(ref data, value, "Data");
			RaisePropertyChanged("IsDataHtml");
		}
	}

	[IgnorePropertyModification]
	public bool IsDataHtml => Data?.StartsWith("<html>", StringComparison.InvariantCultureIgnoreCase) ?? false;

	[IgnorePropertyModification]
	public DateTime? StartDate
	{
		get
		{
			return startDate;
		}
		set
		{
			SetProperty(ref startDate, value, "StartDate");
		}
	}

	[IgnorePropertyModification]
	public DateTime? EndDate
	{
		get
		{
			return endDate;
		}
		set
		{
			SetProperty(ref endDate, value, "EndDate");
		}
	}

	[IgnorePropertyModification]
	public DateTime? CreatedTime
	{
		get
		{
			return createdTime;
		}
		set
		{
			SetProperty(ref createdTime, value, "CreatedTime");
		}
	}

	[IgnorePropertyModification]
	public string Url
	{
		get
		{
			return url;
		}
		set
		{
			SetProperty(ref url, value, "Url");
		}
	}

	[IgnorePropertyModification]
	public string ExpandedUrl
	{
		get
		{
			if (string.IsNullOrWhiteSpace(Url))
			{
				return Url;
			}
			return Environment.ExpandEnvironmentVariables(Url);
		}
	}

	[IgnorePropertyModification]
	public string MediaUrl
	{
		get
		{
			return mediaUrl ?? url;
		}
		set
		{
			SetProperty(ref mediaUrl, value, "MediaUrl");
		}
	}

	[IgnorePropertyModification]
	public string ExpandedMediaUrl
	{
		get
		{
			if (string.IsNullOrWhiteSpace(MediaUrl))
			{
				return MediaUrl;
			}
			return Environment.ExpandEnvironmentVariables(MediaUrl);
		}
	}

	[IgnorePropertyModification]
	public bool HasMedia
	{
		get
		{
			return hasMedia;
		}
		private set
		{
			SetProperty(ref hasMedia, value, "HasMedia");
		}
	}

	[IgnorePropertyModification]
	public MediaSourceType MediaType
	{
		get
		{
			return mediaType;
		}
		private set
		{
			SetProperty(ref mediaType, value, "MediaType");
		}
	}

	[IgnorePropertyModification]
	public bool InMediaMode
	{
		get
		{
			return inMediaMode;
		}
		set
		{
			SetProperty(ref inMediaMode, value, "InMediaMode");
		}
	}

	[IgnorePropertyModification]
	public MediaVideoViewModel MediaModel
	{
		get
		{
			return mediaViewModel;
		}
		protected set
		{
			SetProperty(ref mediaViewModel, value, "MediaModel");
		}
	}

	[IgnorePropertyModification]
	public NotifyTaskCompletion<byte[]> ImageData
	{
		get
		{
			if (imageTask == null)
			{
				imageTask = new NotifyTaskCompletion<byte[]>(OnGetThumbNail());
			}
			return imageTask;
		}
		internal set
		{
			SetProperty(ref imageTask, value, "ImageData");
		}
	}

	[IgnorePropertyModification]
	public NotifyTaskCompletion<MediaSourceType> MediaTypeTask
	{
		get
		{
			if (mediaTypeTask == null)
			{
				mediaTypeTask = new NotifyTaskCompletion<MediaSourceType>(OnGetMediaType());
			}
			return mediaTypeTask;
		}
		internal set
		{
			SetProperty(ref mediaTypeTask, value, "MediaTypeTask");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand ExecuteMediaCommand
	{
		get
		{
			if (executeMediaCommand == null)
			{
				executeMediaCommand = new SimpleCommand<object, object>(OnCanExecuteMediaCommand, OnExecuteMediaCommand);
			}
			return executeMediaCommand;
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand NavigateCommand => base.ExecuteCommand;

	private async Task<byte[]> OnGetThumbNail()
	{
		_ = 1;
		try
		{
			string FULL_URL = ExpandedMediaUrl;
			if (!string.IsNullOrWhiteSpace(FULL_URL))
			{
				if (MediaUrlHelper.IsYoutubeURL(FULL_URL, out var match))
				{
					FULL_URL = string.Format("{0}/{1}/{2}", "http://img.youtube.com/vi", match.Groups[1], "maxresdefault.jpg");
				}
				using WebClient client = new WebClient();
				client.CachePolicy = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable);
				MediaSourceType mediaSourceType = MediaSourceType.Unknown;
				using (await client.OpenReadTaskAsync(FULL_URL))
				{
					mediaSourceType = MediaUrlHelper.GetMediaType(client.ResponseHeaders["content-type"]);
				}
				if (mediaSourceType == MediaSourceType.Image)
				{
					return await client.DownloadDataTaskAsync(FULL_URL);
				}
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnGetThumbNail", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\NewsFeedsViewModel.cs", 556);
		}
		return null;
	}

	private async Task<MediaSourceType> OnGetMediaType()
	{
		string expandedMediaUrl = ExpandedMediaUrl;
		if (string.IsNullOrWhiteSpace(expandedMediaUrl))
		{
			return MediaSourceType.Unknown;
		}
		if (MediaUrlHelper.IsYoutubeURL(expandedMediaUrl, out var _))
		{
			HasMedia = true;
			MediaType = MediaSourceType.Video;
			return MediaSourceType.Video;
		}
		using WebClient client = new WebClient();
		client.CachePolicy = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable);
		using (await client.OpenReadTaskAsync(expandedMediaUrl))
		{
			MediaSourceType mediaSourceType2 = (MediaType = MediaUrlHelper.GetMediaType(client.ResponseHeaders["content-type"]));
			HasMedia = mediaSourceType2 != MediaSourceType.Unknown;
			return mediaSourceType2;
		}
	}

	internal void StopMedia()
	{
		InMediaMode = false;
		MediaModel?.Detach();
	}

	protected override bool OnCanExecuteCommand(object param)
	{
		return !string.IsNullOrWhiteSpace(Url);
	}

	protected override async void OnExecuteCommand(object param)
	{
		try
		{
			await Task.Run(() => Process.Start(ExpandedUrl));
		}
		catch
		{
		}
	}

	private bool OnCanExecuteMediaCommand(object param)
	{
		return !string.IsNullOrWhiteSpace(MediaUrl);
	}

	private async void OnExecuteMediaCommand(object param)
	{
		try
		{
			if (MediaType == MediaSourceType.Video)
			{
				string text = MediaUrl;
				if (string.IsNullOrWhiteSpace(text))
				{
					return;
				}
				InMediaMode = true;
				if (MediaModel == null)
				{
					MediaModel = Client.GetExportedValue<MediaVideoViewModel>();
				}
				if (MediaModel.IsViewAttached)
				{
					return;
				}
				CancellationTokenSource CTS;
				MediaModel.FullScreenCommand = new SimpleCommand<object, object>((object o) => !MediaModel.IsFullScreen, async delegate
				{
					MediaModel.IsFullScreen = true;
					try
					{
						CTS = new CancellationTokenSource();
						CancellationToken token = CTS.Token;
						MediaModel.ExitFullScreenCommand = new SimpleCommand<object, object>((object e) => MediaModel.IsFullScreen, delegate
						{
							CTS.Cancel();
						});
						await Shell.ShowOverlayAsync(MediaModel, token);
					}
					catch (OperationCanceledException)
					{
					}
					catch
					{
						throw;
					}
					finally
					{
						MediaModel.IsFullScreen = false;
						MediaModel.ResetCommands();
					}
				});
				MediaModel.SourceType = MediaSourceType.Video;
				MediaModel.MediaSource = text;
				try
				{
					await MediaModel.InitializeAsync(CancellationToken.None);
					if (!InMediaMode)
					{
						MediaModel.Detach();
					}
					return;
				}
				catch (NotSupportedException)
				{
					return;
				}
				catch
				{
					throw;
				}
			}
			if (MediaType == MediaSourceType.Image && !string.IsNullOrWhiteSpace(ExpandedUrl))
			{
				OnExecuteCommand(param);
			}
		}
		catch (Exception ex2)
		{
			Client.TraceWrite(ex2, "OnExecuteMediaCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\NewsFeedsViewModel.cs", 697);
		}
	}

	protected override void OnResetCommands()
	{
		base.OnResetCommands();
		ExecuteMediaCommand?.RaiseCanExecuteChanged();
	}
}
