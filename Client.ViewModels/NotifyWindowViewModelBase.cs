using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using SharedLib.ViewModels;

namespace Client.ViewModels;

public class NotifyWindowViewModelBase : ViewModelBase, INotifyWindowViewModel
{
	private Window splashWindow;

	private IntPtr splashWindowHandle;

	public Window Window
	{
		get
		{
			return splashWindow;
		}
		protected set
		{
			splashWindow = value;
			RaisePropertyChanged("Window");
		}
	}

	public IntPtr WindowHandle
	{
		get
		{
			return splashWindowHandle;
		}
		protected set
		{
			splashWindowHandle = value;
			RaisePropertyChanged("WindowHandle");
		}
	}

	public bool AllowClosing { get; set; }

	public bool AllowDrag { get; set; }

	public bool WasClosed { get; protected set; }

	public bool WasShown { get; protected set; }

	public bool IsLoaded { get; protected set; }

	public event EventHandler<EventArgs> Closed;

	public event EventHandler<EventArgs> Shown;

	public NotifyWindowViewModelBase(Window splashWindow)
	{
		if (Application.Current == null)
		{
			throw new ArgumentNullException();
		}
		base.UIDispatcher = Application.Current.Dispatcher;
		if (splashWindow != null)
		{
			Window = splashWindow;
			OnInitializeWindow();
			OnAttachWindow();
			return;
		}
		throw new ArgumentNullException("SplashWindow", "Window may not be null.");
	}

	public void Show()
	{
		Show(IntPtr.Zero);
	}

	public async Task ShowAsync()
	{
		Action callback = delegate
		{
			OnShowWindow(IntPtr.Zero);
		};
		await base.UIDispatcher.InvokeAsync(callback);
	}

	public void Show(IntPtr owner)
	{
		Action<IntPtr> method = delegate(IntPtr handle)
		{
			OnShowWindow(handle);
		};
		base.UIDispatcher.Invoke(method, owner);
	}

	public void ShowDialog()
	{
		ShowDialog(IntPtr.Zero);
	}

	public void ShowDialog(IntPtr owner)
	{
		Action<IntPtr> method = delegate(IntPtr handle)
		{
			OnShowDialog(handle);
		};
		base.UIDispatcher.Invoke(method, owner);
	}

	public void ShowDialogAsync(IntPtr owner)
	{
		Action<IntPtr> method = ShowDialog;
		base.UIDispatcher.BeginInvoke(method, owner);
	}

	public void Hide()
	{
		Action callback = delegate
		{
			OnCloseWindow();
		};
		base.UIDispatcher.Invoke(callback);
	}

	public void HideAsync()
	{
		Action method = delegate
		{
			OnCloseWindow();
		};
		base.UIDispatcher.BeginInvoke(method);
	}

	protected virtual void OnShowWindow(IntPtr owner)
	{
		if (!IsLoaded)
		{
			WindowInteropHelper windowInteropHelper = new WindowInteropHelper(Window);
			windowInteropHelper.Owner = owner;
			Window.ShowActivated = true;
			Window.Opacity = 0.0;
			Window.Show();
			WindowHandle = windowInteropHelper.Handle;
			DoubleAnimation animation = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(500.0)));
			Window.BeginAnimation(UIElement.OpacityProperty, animation);
		}
	}

	protected virtual void OnShowDialog(IntPtr owner)
	{
		if (!IsLoaded)
		{
			WindowInteropHelper windowInteropHelper = new WindowInteropHelper(Window);
			windowInteropHelper.Owner = owner;
			WindowHandle = windowInteropHelper.Handle;
			Window.ShowActivated = true;
			Window.Opacity = 0.0;
			DoubleAnimation animation = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(500.0)));
			Window.BeginAnimation(UIElement.OpacityProperty, animation);
			Window.ShowDialog();
		}
	}

	protected virtual void OnCloseWindow()
	{
		if (!WasClosed)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, new Duration(TimeSpan.FromMilliseconds(500.0)));
			doubleAnimation.Completed += delegate
			{
				AllowClosing = true;
				Window.Close();
				WindowHandle = IntPtr.Zero;
			};
			Window.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		}
	}

	protected virtual void OnInitializeWindow()
	{
		Window.Width = 480.0;
		Window.Height = 200.0;
		Window.MaxWidth = 480.0;
		Window.MaxHeight = 200.0;
		Window.DataContext = this;
	}

	protected virtual void OnAttachWindow()
	{
		Window.Loaded += OnMainWindowLoadedInternal;
		Window.Closing += OnMainWindowClosingInternal;
		Window.Closed += OnMainWindowClosedInternal;
		Window.MouseDown += OnMainWindowMouseDown;
	}

	private void OnMainWindowLoadedInternal(object sender, RoutedEventArgs e)
	{
		WasShown = true;
		IsLoaded = true;
		OnMainWindowLoaded(sender, e);
		if (this.Shown != null)
		{
			this.Shown(this, new EventArgs());
		}
	}

	private void OnMainWindowClosedInternal(object sender, EventArgs e)
	{
		WasClosed = true;
		IsLoaded = false;
		OnMainWindowClosed(sender, e);
		if (this.Closed != null)
		{
			this.Closed(this, new EventArgs());
		}
	}

	private void OnMainWindowClosingInternal(object sender, CancelEventArgs e)
	{
		e.Cancel = !AllowClosing;
		OnMainWindowClosing(sender, e);
	}

	protected virtual void OnMainWindowMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (AllowDrag && e.ChangedButton == MouseButton.Left && sender is Window)
		{
			((Window)sender).DragMove();
		}
	}

	protected virtual void OnMainWindowClosing(object sender, CancelEventArgs e)
	{
	}

	protected virtual void OnMainWindowClosed(object sender, EventArgs e)
	{
	}

	protected virtual void OnMainWindowLoaded(object sender, RoutedEventArgs e)
	{
	}
}
