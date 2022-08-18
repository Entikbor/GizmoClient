using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using SharedLib;
using SharedLib.Configuration;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
public class LoginRotatorViewModel : ItemsListViewModelBaseOfType<RotatorItemViewModel, NullView>, IPartImportsSatisfiedNotification
{
	private static readonly string[] IMAGE_EXTENSIONS = new string[3] { ".JPG", ".PNG", ".BMP" };

	private static readonly string[] VIDEO_EXTENSIONS = new string[4] { ".M4V", ".MP4", ".WMV", ".AVI" };

	private static readonly string[] ALL_EXTENSIONS = IMAGE_EXTENSIONS.Union(VIDEO_EXTENSIONS).ToArray();

	private string wallpaper;

	private int currentIndex;

	private Timer ROTATE_TIMER;

	private object ROTATE_TIMER_LOCK = new object();

	private int ROTATE_SPAN = 5000;

	[Import]
	private GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public string Wallpaper
	{
		get
		{
			return wallpaper;
		}
		set
		{
			SetProperty(ref wallpaper, value, "Wallpaper");
		}
	}

	[IgnorePropertyModification]
	public int CurrentIndex
	{
		get
		{
			return currentIndex;
		}
		private set
		{
			SetProperty(ref currentIndex, value, "CurrentIndex");
		}
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
			if (ROTATE_TIMER != null)
			{
				int count = Items.Count;
				int num = CurrentIndex + 1;
				int index = ((num < count) ? num : 0);
				RotatorItemViewModel rotatorItemViewModel = Items[index];
				if (rotatorItemViewModel.IsVideo && !rotatorItemViewModel.IsPlaying)
				{
					StopRotation();
					rotatorItemViewModel.Restart();
				}
				CurrentIndex = index;
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnRotateCallBack", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\LoginRotatorViewModel.cs", 125);
		}
		finally
		{
			Monitor.Exit(ROTATE_TIMER_LOCK);
		}
	}

	private void DetermineRotation()
	{
		try
		{
			lock (ROTATE_TIMER_LOCK)
			{
				int count = Items.Count;
				if (count == 0 || Client.IsUserLoggedIn || Client.IsUserLoggingIn)
				{
					Items.Where((RotatorItemViewModel i) => i.IsVideo).ToList().ForEach(delegate(RotatorItemViewModel m)
					{
						m.IsPlaying = false;
					});
					StopRotation();
				}
				else
				{
					if (count <= 0)
					{
						return;
					}
					RotatorItemViewModel rotatorItemViewModel = Items[CurrentIndex];
					if (count == 1)
					{
						if (rotatorItemViewModel.IsVideo && !rotatorItemViewModel.IsPlaying)
						{
							rotatorItemViewModel.Restart();
						}
					}
					else
					{
						StartRotation();
					}
				}
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "DetermineRotation", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\LoginRotatorViewModel.cs", 170);
		}
	}

	public void Initialize(SkinConfig SKIN_CONFIG, string SKIN_BASE_DIR)
	{
		if (SKIN_CONFIG == null)
		{
			throw new ArgumentNullException("SKIN_CONFIG");
		}
		if (string.IsNullOrWhiteSpace(SKIN_BASE_DIR))
		{
			throw new ArgumentNullException("SKIN_BASE_DIR");
		}
		string text = SKIN_CONFIG.Rotator?.RotatorSource;
		int? num = SKIN_CONFIG.Rotator?.RotateEverySeconds;
		RotatorFileOrder? rotatorFileOrder = SKIN_CONFIG.Rotator?.FileOrder;
		ROTATE_SPAN = ((num > 0) ? (num.GetValueOrDefault() * 1000) : ROTATE_SPAN);
		if (!string.IsNullOrWhiteSpace(SKIN_CONFIG.Wallpaper))
		{
			string name = (Path.IsPathRooted(SKIN_CONFIG.Wallpaper) ? SKIN_CONFIG.Wallpaper : Path.Combine(SKIN_BASE_DIR, SKIN_CONFIG.Wallpaper));
			name = Environment.ExpandEnvironmentVariables(name);
			if (File.Exists(name))
			{
				Wallpaper = name;
			}
		}
		if (!string.IsNullOrWhiteSpace(text))
		{
			string name2 = (Path.IsPathRooted(text) ? text : Path.Combine(SKIN_BASE_DIR, text));
			name2 = Environment.ExpandEnvironmentVariables(name2);
			if (Directory.Exists(name2))
			{
				IEnumerable<string> source = from FILE in Directory.GetFiles(name2, "*.*", SearchOption.AllDirectories)
					where ALL_EXTENSIONS.Any((string EXTESNSION) => FILE.EndsWith(EXTESNSION, StringComparison.InvariantCultureIgnoreCase))
					select FILE;
				if (rotatorFileOrder.HasValue)
				{
					switch (rotatorFileOrder)
					{
					case RotatorFileOrder.Random:
						source = source.OrderBy((string FILE_NAME) => Guid.NewGuid());
						break;
					case RotatorFileOrder.FileName:
						source = source.OrderBy((string FILE_NAME) => FILE_NAME);
						break;
					}
				}
				IEnumerable<RotatorItemViewModel> list = source.Select((string FILE) => new RotatorItemViewModel
				{
					MediaPath = FILE,
					MediaEndedCommand = new SimpleCommand<object, object>((object p) => true, OnMediaEndedCommand),
					MediaFailedCommand = new SimpleCommand<object, object>((object p) => true, OnMediaEndedCommand),
					IsVideo = VIDEO_EXTENSIONS.Any((string EXTENSION) => FILE.EndsWith(EXTENSION, StringComparison.InvariantCultureIgnoreCase))
				});
				InitFrom(list);
			}
		}
		DetermineRotation();
	}

	private void OnMediaEndedCommand(object param)
	{
		try
		{
			if (param is RoutedEventArgs routedEventArgs && (routedEventArgs.OriginalSource as FrameworkElement)?.DataContext is RotatorItemViewModel rotatorItemViewModel && rotatorItemViewModel.IsVideo)
			{
				rotatorItemViewModel.IsPlaying = false;
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnMediaEndedCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\LoginRotatorViewModel.cs", 253);
		}
		DetermineRotation();
	}

	private void OnClientLoginStateChange(object sender, UserEventArgs e)
	{
		DetermineRotation();
	}

	public void OnImportsSatisfied()
	{
		Client.LoginStateChange += OnClientLoginStateChange;
	}
}
