using System;
using System.Windows.Input;
using SharedLib;
using SharedLib.ViewModels;

namespace Client.ViewModels;

public class RotatorItemViewModel : ViewModel
{
	private ICommand mediaEndedCommand;

	private ICommand mediaFailedCommand;

	private bool isPlaying;

	private bool isVideo;

	private string mediaPath;

	private TimeSpan position;

	[IgnorePropertyModification]
	public TimeSpan Position
	{
		get
		{
			return position;
		}
		set
		{
			SetProperty(ref position, value, "Position");
		}
	}

	[IgnorePropertyModification]
	public ICommand MediaEndedCommand
	{
		get
		{
			return mediaEndedCommand;
		}
		set
		{
			SetProperty(ref mediaEndedCommand, value, "MediaEndedCommand");
		}
	}

	[IgnorePropertyModification]
	public ICommand MediaFailedCommand
	{
		get
		{
			return mediaFailedCommand;
		}
		set
		{
			SetProperty(ref mediaFailedCommand, value, "MediaFailedCommand");
		}
	}

	[IgnorePropertyModification]
	public bool IsPlaying
	{
		get
		{
			return isPlaying;
		}
		set
		{
			SetProperty(ref isPlaying, value, "IsPlaying");
		}
	}

	[IgnorePropertyModification]
	public string MediaPath
	{
		get
		{
			return mediaPath;
		}
		set
		{
			SetProperty(ref mediaPath, value, "MediaPath");
			RaisePropertyChanged("MediaUri");
		}
	}

	[IgnorePropertyModification]
	public Uri MediaUri => new Uri(MediaPath);

	[IgnorePropertyModification]
	public bool IsVideo
	{
		get
		{
			return isVideo;
		}
		set
		{
			SetProperty(ref isVideo, value, "IsVideo");
		}
	}

	public void Restart()
	{
		IsPlaying = false;
		Position = new TimeSpan(0L);
		IsPlaying = true;
	}
}
