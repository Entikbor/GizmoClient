using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using SharedLib;
using SkinInterfaces;
using Unosquare.FFME;
using Unosquare.FFME.Common;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class MediaVideoViewModel : MediaViewModelBase
{
	private MediaElement MEDIA_VIEW;

	private readonly double DEFAULT_AUDIO_LEVEL = 0.5;

	private static double? PREFERED_VOLUME;

	private double RESTORE_VOLUME = 0.5;

	private double duration;

	private double position;

	private double volume;

	private bool isBuffering;

	private bool isFullScreen;

	private bool isInitialiazing;

	private MediaPlaybackState mediaState;

	private IExecutionChangedAwareCommand mediaCommand;

	private IExecutionChangedAwareCommand volumeCommand;

	[IgnorePropertyModification]
	public double Volume
	{
		get
		{
			return volume;
		}
		set
		{
			SetProperty(ref volume, value, "Volume");
			RaisePropertyChanged("VolumeLevelKind");
			RaisePropertyChanged("IsMuted");
		}
	}

	[IgnorePropertyModification]
	public double Position
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
	public double Duration
	{
		get
		{
			return duration;
		}
		set
		{
			SetProperty(ref duration, value, "Duration");
		}
	}

	[IgnorePropertyModification]
	public bool IsBuffering
	{
		get
		{
			return isBuffering;
		}
		protected set
		{
			SetProperty(ref isBuffering, value, "IsBuffering");
		}
	}

	[IgnorePropertyModification]
	public bool IsPlaying => MediaState == MediaPlaybackState.Play;

	[IgnorePropertyModification]
	public bool IsPaused => MediaState == MediaPlaybackState.Pause;

	[IgnorePropertyModification]
	public bool IsMuted => Volume <= 0.0;

	[IgnorePropertyModification]
	public MediaPlaybackState MediaState
	{
		get
		{
			return mediaState;
		}
		set
		{
			SetProperty(ref mediaState, value, "MediaState");
			RaisePropertyChanged("IsPlaying");
			RaisePropertyChanged("IsPaused");
		}
	}

	[IgnorePropertyModification]
	public int VolumeLevelKind
	{
		get
		{
			if (Volume == 0.0)
			{
				return 0;
			}
			if ((Volume > 0.0) & (Volume <= 0.5))
			{
				return 1;
			}
			if ((Volume > 0.5) & (Volume < 0.75))
			{
				return 2;
			}
			return 3;
		}
	}

	[IgnorePropertyModification]
	public bool IsFullScreen
	{
		get
		{
			return isFullScreen;
		}
		set
		{
			SetProperty(ref isFullScreen, value, "IsFullScreen");
		}
	}

	[IgnorePropertyModification]
	public bool IsViewAttached => MEDIA_VIEW != null;

	[IgnorePropertyModification]
	public bool IsInitializing
	{
		get
		{
			return isInitialiazing;
		}
		set
		{
			SetProperty(ref isInitialiazing, value, "IsInitializing");
		}
	}

	[IgnorePropertyModification]
	public MediaElement View
	{
		get
		{
			return MEDIA_VIEW;
		}
		protected set
		{
			SetProperty(ref MEDIA_VIEW, value, "View");
			RaisePropertyChanged("IsViewAttached");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand MediaCommand
	{
		get
		{
			if (mediaCommand == null)
			{
				mediaCommand = new SimpleCommand<object, object>(OnCanMediaCommand, OnMediaCommand);
			}
			return mediaCommand;
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand VolumeCommand
	{
		get
		{
			if (volumeCommand == null)
			{
				volumeCommand = new SimpleCommand<object, object>(OnCanVolumeCommand, OnVolumeCommand);
			}
			return volumeCommand;
		}
	}

	[IgnorePropertyModification]
	private double PositionInternal
	{
		get
		{
			return position;
		}
		set
		{
			position = value;
			RaisePropertyChanged("Position");
		}
	}

	private bool OnCanMediaCommand(object param)
	{
		return IsViewAttached;
	}

	private async void OnMediaCommand(object param)
	{
		MediaElement mEDIA_VIEW = MEDIA_VIEW;
		if (mEDIA_VIEW == null)
		{
			return;
		}
		try
		{
			if (mEDIA_VIEW.MediaInfo != null)
			{
				if (mEDIA_VIEW.Position >= mEDIA_VIEW.MediaInfo.Duration)
				{
					mEDIA_VIEW.Position = TimeSpan.FromSeconds(0.0);
				}
				switch (mEDIA_VIEW.MediaState)
				{
				case MediaPlaybackState.Manual:
				case MediaPlaybackState.Play:
					await mEDIA_VIEW.Pause();
					break;
				case MediaPlaybackState.Pause:
				case MediaPlaybackState.Stop:
					await mEDIA_VIEW.Play();
					break;
				}
			}
		}
		catch (Exception ex)
		{
			base.Client.TraceWrite(ex, "OnMediaCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\MediaViewModel.cs", 406);
		}
	}

	private bool OnCanVolumeCommand(object param)
	{
		return IsViewAttached;
	}

	private void OnVolumeCommand(object param)
	{
		double num = 0.0;
		double rESTORE_VOLUME = Volume;
		if (IsMuted)
		{
			num = ((RESTORE_VOLUME <= 0.0) ? DEFAULT_AUDIO_LEVEL : RESTORE_VOLUME);
		}
		else
		{
			RESTORE_VOLUME = rESTORE_VOLUME;
		}
		Volume = num;
	}

	public async Task InitializeAsync(CancellationToken ct)
	{
		string MEDIA_SOURCE = base.MediaSource;
		if (string.IsNullOrWhiteSpace(MEDIA_SOURCE))
		{
			throw new ArgumentNullException("MediaSource");
		}
		try
		{
			IsInitializing = true;
			base.IsFailed = false;
			MEDIA_SOURCE = Environment.ExpandEnvironmentVariables(MEDIA_SOURCE);
			string STREAM_SOURCE = MEDIA_SOURCE;
			if (MediaUrlHelper.IsYoutubeURL(MEDIA_SOURCE, out var _))
			{
				for (int TRY = 0; TRY <= 5; TRY++)
				{
					try
					{
						STREAM_SOURCE = (await new YoutubeClient().Videos.Streams.GetManifestAsync(MEDIA_SOURCE)).GetMuxedStreams().GetWithHighestBitrate()?.Url;
					}
					catch (Exception)
					{
						if (TRY > 5)
						{
							throw;
						}
						await Task.Delay(1000, ct);
						continue;
					}
					break;
				}
			}
			if (string.IsNullOrWhiteSpace(STREAM_SOURCE))
			{
				throw new NotSupportedException("Could not obtain stram source from " + MEDIA_SOURCE + ".");
			}
			Dispatcher dispatcher = Application.Current.Dispatcher;
			if (!dispatcher.CheckAccess())
			{
				await dispatcher.InvokeAsync(HANDLER);
			}
			else
			{
				HANDLER();
			}
			await View.Open(new Uri(STREAM_SOURCE));
			base.Attach();
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			base.IsFailed = true;
			throw;
		}
		finally
		{
			IsInitializing = false;
			ResetCommands();
		}
		void HANDLER()
		{
			View = new MediaElement
			{
				LoadedBehavior = MediaPlaybackState.Play,
				UnloadedBehavior = MediaPlaybackState.Pause
			};
			Binding binding = new Binding("Volume")
			{
				Mode = BindingMode.TwoWay,
				Source = this
			};
			MEDIA_VIEW.SetBinding(MediaElement.VolumeProperty, binding);
			MEDIA_VIEW.MediaOpened += OnMediaOpened;
			MEDIA_VIEW.PositionChanged += OnMediaPositionChanged;
			MEDIA_VIEW.BufferingStarted += OnBufferingStarted;
			MEDIA_VIEW.BufferingEnded += OnMediaBufferingEnded;
			MEDIA_VIEW.MediaStateChanged += OnMediaStateChanged;
			MEDIA_VIEW.MediaFailed += OnMediaFailed;
		}
	}

	public override void Detach()
	{
		base.Detach();
		if (IsViewAttached)
		{
			if (MEDIA_VIEW.Dispatcher.CheckAccess())
			{
				HANDLER();
			}
			else
			{
				MEDIA_VIEW.Dispatcher.Invoke(HANDLER);
			}
			View = null;
		}
		Position = 0.0;
		IsBuffering = false;
		IsFullScreen = false;
		base.IsFailed = false;
		MediaState = MediaPlaybackState.Manual;
		Volume = DEFAULT_AUDIO_LEVEL;
		RaisePropertyChanged("View");
		ResetCommands();
		void HANDLER()
		{
			BindingOperations.ClearAllBindings(MEDIA_VIEW);
			MEDIA_VIEW.MediaOpened -= OnMediaOpened;
			MEDIA_VIEW.PositionChanged -= OnMediaPositionChanged;
			MEDIA_VIEW.BufferingStarted -= OnBufferingStarted;
			MEDIA_VIEW.BufferingEnded -= OnMediaBufferingEnded;
			MEDIA_VIEW.MediaStateChanged -= OnMediaStateChanged;
			MEDIA_VIEW.MediaFailed -= OnMediaFailed;
			MEDIA_VIEW.Stop();
		}
	}

	private void OnMediaFailed(object sender, MediaFailedEventArgs e)
	{
		base.IsFailed = true;
	}

	private void OnMediaStateChanged(object sender, MediaStateChangedEventArgs e)
	{
		MediaState = e.MediaState;
		MediaPlaybackState mediaPlaybackState = e.MediaState;
		if (mediaPlaybackState == MediaPlaybackState.Close || mediaPlaybackState == MediaPlaybackState.Stop)
		{
			IsBuffering = false;
		}
	}

	private void OnMediaBufferingEnded(object sender, EventArgs e)
	{
		IsBuffering = false;
	}

	private void OnBufferingStarted(object sender, EventArgs e)
	{
		IsBuffering = true;
	}

	private void OnMediaPositionChanged(object sender, PositionChangedEventArgs e)
	{
		PositionInternal = e.Position.TotalSeconds;
	}

	private void OnMediaOpened(object sender, MediaOpenedEventArgs e)
	{
		MEDIA_VIEW.Volume = PREFERED_VOLUME ?? DEFAULT_AUDIO_LEVEL;
		MEDIA_VIEW.Position = TimeSpan.FromSeconds(Position);
		Duration = MEDIA_VIEW.MediaInfo.Duration.TotalSeconds;
	}

	protected override void OnPropertyChanged(object sender, PropertyChangedEventArgsEx args)
	{
		base.OnPropertyChanged(sender, args);
		if (!IsViewAttached)
		{
			return;
		}
		if (args.PropertyName == "Volume")
		{
			PREFERED_VOLUME = Volume;
		}
		if (args.PropertyName == "Position")
		{
			MEDIA_VIEW?.Dispatcher.Invoke(() => MEDIA_VIEW.Position = TimeSpan.FromSeconds(Position));
		}
	}

	protected override void OnResetCommands()
	{
		base.OnResetCommands();
		MediaCommand?.RaiseCanExecuteChanged();
		VolumeCommand?.RaiseCanExecuteChanged();
	}
}
