using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Net;
using System.Net.Cache;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SharedLib;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class AppLinkViewModel : ExecuteViewModelBase
{
	private static Regex mediaRegEx = new Regex("youtu(?:\\.be|be\\.com)/(?:.*v(?:/|=)|(?:.*/)?)([a-zA-Z0-9-_]+)", RegexOptions.None);

	private string caption;

	private string description;

	private string url;

	private int displayOrder;

	private NotifyTaskCompletion<byte[]> imageTask;

	[Import]
	private IShellWindow Shell { get; set; }

	[Import]
	private GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public string Caption
	{
		get
		{
			return caption;
		}
		set
		{
			SetProperty(ref caption, value, "Caption");
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
	public int DisplayOrder
	{
		get
		{
			return displayOrder;
		}
		set
		{
			SetProperty(ref displayOrder, value, "DisplayOrder");
		}
	}

	[IgnorePropertyModification]
	public virtual string ExpandedUrl
	{
		get
		{
			if (string.IsNullOrWhiteSpace(Url))
			{
				return null;
			}
			return Environment.ExpandEnvironmentVariables(Url);
		}
	}

	[IgnorePropertyModification]
	public bool IsMedia
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(ExpandedUrl))
			{
				return mediaRegEx.IsMatch(ExpandedUrl);
			}
			return false;
		}
	}

	[IgnorePropertyModification]
	public Uri Uri
	{
		get
		{
			Uri.TryCreate(Url, UriKind.RelativeOrAbsolute, out var result);
			return result;
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
	}

	private Task<byte[]> OnGetThumbNail()
	{
		Match match = mediaRegEx.Match(ExpandedUrl);
		if (match.Success)
		{
			string address = string.Format("{0}/{1}/{2}", "http://img.youtube.com/vi", match.Groups[1], "0.jpg");
			using WebClient webClient = new WebClient();
			webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable);
			return webClient.DownloadDataTaskAsync(address);
		}
		return null;
	}

	protected override bool OnCanExecuteCommand(object param)
	{
		return !string.IsNullOrWhiteSpace(ExpandedUrl);
	}

	protected override async void OnExecuteCommand(object param)
	{
		_ = 2;
		try
		{
			string LINK_URL = ExpandedUrl;
			if (string.IsNullOrWhiteSpace(LINK_URL))
			{
				return;
			}
			if (IsMedia)
			{
				MediaVideoViewModel videoModel = Client.GetExportedValue<MediaVideoViewModel>();
				videoModel.SourceType = MediaSourceType.Video;
				videoModel.MediaSource = LINK_URL;
				videoModel.IsFullScreen = true;
				CancellationTokenSource CTS = new CancellationTokenSource();
				CancellationTokenSource INIT_CTS = new CancellationTokenSource();
				videoModel.ExitFullScreenCommand = new SimpleCommand<object, object>((object o) => !CTS.IsCancellationRequested, delegate
				{
					CTS.Cancel();
					videoModel.ExitFullScreenCommand?.RaiseCanExecuteChanged();
				});
				Task task = videoModel.InitializeAsync(INIT_CTS.Token);
				Task showTask = Shell.ShowOverlayAsync(videoModel, CTS.Token).ContinueWith(delegate
				{
					INIT_CTS.Cancel();
				});
				try
				{
					await task;
				}
				catch (Exception ex)
				{
					Client.TraceWrite(ex, "OnExecuteCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\AppLinksViewModel.cs", 249);
				}
				try
				{
					await showTask;
				}
				catch
				{
					throw;
				}
				finally
				{
					videoModel.Detach();
				}
			}
			else
			{
				await Task.Run(() => Process.Start(LINK_URL));
			}
		}
		catch (Exception ex2)
		{
			Client.TraceWrite(ex2, "OnExecuteCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\AppLinksViewModel.cs", 272);
		}
	}
}
