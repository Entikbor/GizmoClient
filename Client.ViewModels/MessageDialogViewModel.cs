using System.ComponentModel.Composition;
using System.Windows.Input;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
[Export(typeof(IMessageDialogViewModel))]
public class MessageDialogViewModel : ViewModel, IMessageDialogViewModel
{
	private string message;

	private MessageDialogButtons buttons = MessageDialogButtons.Accept;

	public ICommand AcceptCommand { get; set; }

	public ICommand CancelCommand { get; set; }

	public string Message
	{
		get
		{
			return message;
		}
		set
		{
			SetProperty(ref message, value, "Message");
		}
	}

	public MessageDialogButtons Buttons
	{
		get
		{
			return buttons;
		}
		set
		{
			SetProperty(ref buttons, value, "Buttons");
			RaisePropertyChanged("ShowAccept");
			RaisePropertyChanged("ShowCancel");
		}
	}

	public bool ShowAccept => Buttons.HasFlag(MessageDialogButtons.Accept);

	public bool ShowCancel => Buttons.HasFlag(MessageDialogButtons.Cancel);
}
