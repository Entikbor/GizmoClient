using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using SharedLib;
using SkinInterfaces;
using Win32API.Modules;

namespace Client.ViewModels;

public abstract class ViewModelBase : PropertyChangedNotificator
{
	private delegate MessageBoxResult MessageBoxDelegate(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options);

	private Dispatcher dispatcher;

	private ICommand loadedCommand;

	private ICommand dataContextChangedCommand;

	private bool isAsyncActionActive;

	private bool isEnumerating;

	public ICommand DataContextChangedCommand
	{
		get
		{
			if (dataContextChangedCommand == null)
			{
				dataContextChangedCommand = new SimpleCommand
				{
					CanExecuteDelegate = (object x) => true,
					ExecuteDelegate = delegate(object x)
					{
						OnDataContextChanged(x);
					}
				};
			}
			return dataContextChangedCommand;
		}
	}

	public ICommand LoadedCommand
	{
		get
		{
			if (loadedCommand == null)
			{
				loadedCommand = new SimpleCommand
				{
					CanExecuteDelegate = (object x) => true,
					ExecuteDelegate = delegate(object x)
					{
						OnLoaded(this, x as RoutedEventArgs);
					}
				};
			}
			return loadedCommand;
		}
	}

	public Dispatcher UIDispatcher
	{
		get
		{
			if (dispatcher == null)
			{
				return Application.Current.Dispatcher;
			}
			return dispatcher;
		}
		protected set
		{
			dispatcher = value;
			RaisePropertyChanged("UIDispatcher");
		}
	}

	public bool IsAsyncActionActive
	{
		get
		{
			return isAsyncActionActive;
		}
		protected set
		{
			isAsyncActionActive = value;
			Dispatcher uIDispatcher = UIDispatcher;
			if (uIDispatcher != null && !uIDispatcher.CheckAccess())
			{
				uIDispatcher.Invoke(delegate
				{
					RaisePropertyChanged("IsAsyncActionActive");
				});
			}
			else
			{
				RaisePropertyChanged("IsAsyncActionActive");
			}
		}
	}

	public bool IsEnumerating
	{
		get
		{
			return isEnumerating;
		}
		protected set
		{
			isEnumerating = value;
			Dispatcher uIDispatcher = UIDispatcher;
			if (uIDispatcher != null && !uIDispatcher.CheckAccess())
			{
				uIDispatcher.Invoke(delegate
				{
					RaisePropertyChanged("IsEnumerating");
				});
			}
			else
			{
				RaisePropertyChanged("IsEnumerating");
			}
			ResetCommands();
		}
	}

	public ViewModelBase()
	{
		UIDispatcher = Application.Current.Dispatcher;
	}

	protected void AddOnDispatcher(IList list, object obj, DispatcherPriority priority = DispatcherPriority.Normal)
	{
		UIDispatcher.Invoke(delegate
		{
			list.Add(obj);
		}, priority);
	}

	protected void RemoveOnDispatcher(IList list, object obj, DispatcherPriority priority = DispatcherPriority.Normal)
	{
		UIDispatcher.Invoke(delegate
		{
			list.Remove(obj);
		}, priority);
	}

	protected void ClearOnDispatcher(IList list, DispatcherPriority priority = DispatcherPriority.Normal)
	{
		UIDispatcher.Invoke(delegate
		{
			list.Clear();
		}, priority);
	}

	protected bool ContainsOnDispatcher(IList list, object obj, DispatcherPriority priority = DispatcherPriority.Normal)
	{
		bool result = false;
		UIDispatcher.Invoke(delegate
		{
			result = list.Contains(obj);
		}, priority);
		return result;
	}

	protected void FillOnDispatcher(IList list, IEnumerable source, DispatcherPriority priority = DispatcherPriority.Normal)
	{
		UIDispatcher.Invoke(delegate
		{
			foreach (object item in source)
			{
				list.Add(item);
			}
		}, priority);
	}

	protected void MergeOnDispatcher(IList list, IEnumerable source, DispatcherPriority priority = DispatcherPriority.Normal)
	{
		List<object> removedItems = (from e in list.OfType<object>()
			where !source.OfType<object>().Contains(e)
			select e).ToList();
		List<object> addedItems = (from e in source.OfType<object>()
			where !list.OfType<object>().Contains(e)
			select e).ToList();
		UIDispatcher.Invoke(delegate
		{
			foreach (object item in removedItems)
			{
				list.Remove(item);
			}
			foreach (object item2 in addedItems)
			{
				list.Add(item2);
			}
		}, priority);
	}

	protected void RefreshDispatcher(IList list, DispatcherPriority priority = DispatcherPriority.Normal)
	{
		UIDispatcher.Invoke(delegate
		{
			CollectionViewSource.GetDefaultView(list)?.Refresh();
		}, priority);
	}

	public virtual void OnLoaded(object sender, RoutedEventArgs e)
	{
	}

	public virtual void OnUnloaded(object sender, RoutedEventArgs e)
	{
	}

	protected virtual void OnRefresh()
	{
	}

	protected virtual void OnDataContextChanged(object context)
	{
	}

	protected virtual void OnReset()
	{
	}

	protected virtual void OnResetCommands()
	{
		CommandManager.InvalidateRequerySuggested();
	}

	public void Refresh()
	{
		Refresh(DispatcherPriority.Normal);
	}

	public void Refresh(DispatcherPriority priority)
	{
		if (UIDispatcher != null)
		{
			UIDispatcher.BeginInvoke(new Action(OnRefresh), priority);
		}
	}

	public void Reset()
	{
		UIDispatcher.Invoke(delegate
		{
			OnReset();
		});
	}

	public void ResetCommands()
	{
		OnResetCommands();
	}

	public MessageBoxResult ShowMessage(string messageBoxText, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Asterisk, MessageBoxResult defaultResult = MessageBoxResult.No, MessageBoxOptions options = MessageBoxOptions.None)
	{
		MessageBoxDelegate messageBoxDelegate = delegate(string _messageBoxText, string _caption, MessageBoxButton _button, MessageBoxImage _icon, MessageBoxResult _defaultResult, MessageBoxOptions _options)
		{
			if (Application.Current.MainWindow != null)
			{
				MessageBoxResult result = MessageBox.Show(Application.Current.MainWindow, _messageBoxText, _caption, _button, _icon, _defaultResult, _options);
				User32.InvalidateRect(new WindowInteropHelper(Application.Current.MainWindow).Handle, IntPtr.Zero, bErase: true);
				return result;
			}
			return MessageBox.Show(_messageBoxText, _caption, _button, _icon, _defaultResult, _options);
		};
		if (Application.Current.Dispatcher.CheckAccess())
		{
			return messageBoxDelegate(messageBoxText, caption, button, icon, defaultResult, options);
		}
		return (MessageBoxResult)Application.Current.Dispatcher.Invoke(messageBoxDelegate, messageBoxText, caption, button, icon, defaultResult, options);
	}

	public MessageBoxResult ShowError(string messageBoxText, string caption)
	{
		return ShowMessage(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.Hand);
	}
}
