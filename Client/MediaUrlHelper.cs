using System;
using System.Text.RegularExpressions;
using Client.ViewModels;

namespace Client;

public static class MediaUrlHelper
{
	private static readonly Regex mediaRegEx = new Regex("youtu(?:\\.be|be\\.com)/(?:.*v(?:/|=)|(?:.*/)?)([a-zA-Z0-9-_]+)", RegexOptions.None);

	public static bool IsYoutubeURL(string url, out Match match)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			throw new ArgumentNullException("url");
		}
		match = mediaRegEx.Match(url);
		return match.Success;
	}

	public static MediaSourceType GetMediaType(string contentType)
	{
		if (string.IsNullOrWhiteSpace(contentType))
		{
			throw new ArgumentNullException("contentType");
		}
		switch (contentType)
		{
		case "image/bmp":
		case "image/jpeg":
		case "image/png":
			return MediaSourceType.Image;
		case "video/mp4":
		case "video/mpeg":
		case "video/ogg":
		case "video/x-msvideo":
			return MediaSourceType.Video;
		default:
			return MediaSourceType.Unknown;
		}
	}
}
