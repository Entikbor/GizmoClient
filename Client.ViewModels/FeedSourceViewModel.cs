using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Threading.Tasks;
using SharedLib;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class FeedSourceViewModel : ViewModel, INewsViewModel, IFeedSourceViewModel
{
	private int maxResults;

	private string title;

	private string url;

	private bool hasEnumerated;

	private bool isEnumerating;

	private bool hasFailed;

	private IEnumerable<NewsViewModel> feedItems;

	private readonly SemaphoreSlim ENUM_LOCK = new SemaphoreSlim(1, 1);

	[Import]
	private GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public IEnumerable<NewsViewModel> FeedItems
	{
		get
		{
			return feedItems;
		}
		private set
		{
			SetProperty(ref feedItems, value, "FeedItems");
		}
	}

	[IgnorePropertyModification]
	public bool IsEnumerating
	{
		get
		{
			return isEnumerating;
		}
		private set
		{
			SetProperty(ref isEnumerating, value, "IsEnumerating");
		}
	}

	[IgnorePropertyModification]
	public bool HasEnumerated
	{
		get
		{
			return hasEnumerated;
		}
		internal set
		{
			SetProperty(ref hasEnumerated, value, "HasEnumerated");
		}
	}

	[IgnorePropertyModification]
	public bool HasFailed
	{
		get
		{
			return hasFailed;
		}
		set
		{
			SetProperty(ref hasFailed, value, "HasFailed");
		}
	}

	[IgnorePropertyModification]
	public int MaxResults
	{
		get
		{
			return maxResults;
		}
		set
		{
			SetProperty(ref maxResults, value, "MaxResults");
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

	public async Task EnumerateAsync()
	{
		await ENUM_LOCK.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (HasEnumerated)
			{
				return;
			}
			IsEnumerating = true;
			HasFailed = false;
			WebRequest webRequest = WebRequest.Create(Environment.ExpandEnvironmentVariables(Url));
			if (webRequest is HttpWebRequest httpWebRequest)
			{
				httpWebRequest.Method = "GET";
				httpWebRequest.UserAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64";
			}
			using (WebResponse webResponse = await webRequest.GetResponseAsync())
			{
				using Stream stream = webResponse.GetResponseStream();
				using RSSReader reader = new RSSReader(stream);
				SyndicationFeed syndicationFeed = SyndicationFeed.Load(reader);
				List<NewsViewModel> list = (from MODEL in ((MaxResults <= 0) ? syndicationFeed.Items : syndicationFeed.Items.Take(MaxResults)).Select(delegate(SyndicationItem FEED)
					{
						NewsViewModel exportedValue = Client.GetExportedValue<NewsViewModel>();
						exportedValue.Title = FEED.Title?.Text?.Trim();
						exportedValue.Data = FEED.Summary?.Text?.Trim();
						exportedValue.Url = FEED.Links?.FirstOrDefault()?.Uri?.AbsoluteUri;
						exportedValue.CreatedTime = ((FEED.PublishDate.Date == DateTime.MinValue) ? null : new DateTime?(FEED.PublishDate.Date));
						return exportedValue;
					})
					orderby MODEL.CreatedTime descending
					select MODEL).ToList();
				FeedItems = list;
			}
			HasEnumerated = true;
		}
		catch (Exception ex)
		{
			HasFailed = true;
			Client.LogAddError("Could not fetch rss feed " + (Title ?? "Unknown") + " from " + (Url ?? "Unknown") + ".", ex, LogCategories.Generic);
		}
		finally
		{
			ENUM_LOCK.Release();
			IsEnumerating = false;
		}
	}
}
