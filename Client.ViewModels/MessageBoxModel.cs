using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

public class MessageBoxModel : ClientNotifyWindowViewModelBase, IMessageBoxModel
{
	private string title;

	private string message;

	private SimpleCommand<object, object> buttonCommand;

	private ImageSource icon;

	private MessageBoxResult result;

	public string Title
	{
		get
		{
			return title;
		}
		protected set
		{
			title = value;
			RaisePropertyChanged("Title");
		}
	}

	public string Message
	{
		get
		{
			return message;
		}
		protected set
		{
			message = value;
			RaisePropertyChanged("Message");
		}
	}

	public MessageBoxButton Buttons { get; protected set; }

	public NotificationButtons DefaultButton { get; set; }

	public MessageBoxImage Image { get; protected set; }

	public SimpleCommand<object, object> ButtonCommand
	{
		get
		{
			if (buttonCommand == null)
			{
				buttonCommand = new SimpleCommand<object, object>(OnCanButtonCommand, OnButtonCommand);
			}
			return buttonCommand;
		}
	}

	public bool HideButtons { get; set; }

	public ImageSource Icon
	{
		get
		{
			if (this.icon == null)
			{
				Icon icon = null;
				switch (Image)
				{
				case MessageBoxImage.Hand:
					icon = SystemIcons.Error;
					break;
				case MessageBoxImage.Asterisk:
					icon = SystemIcons.Information;
					break;
				case MessageBoxImage.Question:
					icon = SystemIcons.Question;
					break;
				case MessageBoxImage.Exclamation:
					icon = SystemIcons.Warning;
					break;
				}
				if (icon != null)
				{
					BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
					this.icon = bitmapSource;
				}
			}
			return this.icon;
		}
	}

	public MessageBoxResult Result
	{
		get
		{
			return result;
		}
		protected set
		{
			result = value;
		}
	}

	public Visibility IconVisible
	{
		get
		{
			if (Image == MessageBoxImage.None)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public MessageBoxModel(GizmoClient client, string message, string title, MessageBoxImage icon = MessageBoxImage.Asterisk, MessageBoxButton buttons = MessageBoxButton.OK)
		: base(client, new MessageBoxWindow())
	{
		Title = title;
		Message = message;
		Buttons = buttons;
		base.AllowDrag = true;
		Image = icon;
	}

	private bool OnCanButtonCommand(object param)
	{
		int num = 0;
		if (!HideButtons && param is string && int.TryParse(param as string, out num))
		{
			switch (num)
			{
			case 0:
				return (Buttons == MessageBoxButton.OK) | (Buttons == MessageBoxButton.OKCancel);
			case 1:
			case 2:
				return (Buttons == MessageBoxButton.YesNo) | (Buttons == MessageBoxButton.YesNoCancel);
			case 3:
				return Buttons == MessageBoxButton.OKCancel;
			default:
				return false;
			}
		}
		return false;
	}

	private void OnButtonCommand(object param)
	{
		if (!base.IsLoaded)
		{
			return;
		}
		int num = 0;
		if (!string.IsNullOrWhiteSpace(param as string) && int.TryParse(param as string, out num))
		{
			switch (num)
			{
			case 0:
				Result = MessageBoxResult.OK;
				break;
			case 1:
				Result = MessageBoxResult.Yes;
				break;
			case 2:
				Result = MessageBoxResult.No;
				break;
			case 3:
				Result = MessageBoxResult.Cancel;
				break;
			default:
				Result = MessageBoxResult.None;
				break;
			}
		}
		Hide();
	}
}
