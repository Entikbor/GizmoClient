using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Gizmo;
using Gizmo.Web.Api.Models;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
public class ClientUserAgreementsViewModel : ViewModel, IPartImportsSatisfiedNotification
{
	private int? userId;

	private List<UserAgreement> userAgreements;

	private List<UserAgreementState> userAgreementInitialStates;

	private int currentUserAgreementIndex;

	private string currentUserAgreementText;

	private bool currentUserAgreementIgnoreState;

	private bool currentUserAgreementAccepted;

	private IExecutionChangedAwareCommand acceptAgreementCommand;

	private IExecutionChangedAwareCommand rejectAgreementCommand;

	[Import]
	public GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public List<UserAgreement> UserAgreements
	{
		get
		{
			return userAgreements;
		}
		protected set
		{
			SetProperty(ref userAgreements, value, "UserAgreements");
		}
	}

	[IgnorePropertyModification]
	public string CurrentUserAgreementText
	{
		get
		{
			return currentUserAgreementText;
		}
		protected set
		{
			SetProperty(ref currentUserAgreementText, value, "CurrentUserAgreementText");
		}
	}

	[IgnorePropertyModification]
	public bool CurrentUserAgreementIgnoreState
	{
		get
		{
			return currentUserAgreementIgnoreState;
		}
		protected set
		{
			SetProperty(ref currentUserAgreementIgnoreState, value, "CurrentUserAgreementIgnoreState");
		}
	}

	[IgnorePropertyModification]
	public bool CurrentUserAgreementAccepted
	{
		get
		{
			return currentUserAgreementAccepted;
		}
		set
		{
			SetProperty(ref currentUserAgreementAccepted, value, "CurrentUserAgreementAccepted");
		}
	}

	public IExecutionChangedAwareCommand RejectAgreementCommand
	{
		get
		{
			if (rejectAgreementCommand == null)
			{
				rejectAgreementCommand = new SimpleCommand<object, object>(OnCanRejectAgreementCommand, OnRejectAgreementCommand);
			}
			return rejectAgreementCommand;
		}
	}

	public IExecutionChangedAwareCommand AcceptAgreementCommand
	{
		get
		{
			if (acceptAgreementCommand == null)
			{
				acceptAgreementCommand = new SimpleCommand<object, object>(OnCanAcceptAgreementCommand, OnAcceptAgreementCommand);
			}
			return acceptAgreementCommand;
		}
	}

	private bool OnCanAcceptAgreementCommand(object param)
	{
		if (UserAgreements == null || currentUserAgreementIndex + 1 > UserAgreements.Count)
		{
			return false;
		}
		if (!UserAgreements[currentUserAgreementIndex].IgnoreState && !UserAgreements[currentUserAgreementIndex].IsRejectable)
		{
			return CurrentUserAgreementAccepted;
		}
		return true;
	}

	private async void OnAcceptAgreementCommand(object param)
	{
		_ = 1;
		try
		{
			if (!UserAgreements[currentUserAgreementIndex].IgnoreState)
			{
				if (!CurrentUserAgreementAccepted)
				{
					await Client.UserAgreementSetStateAsync(UserAgreements[currentUserAgreementIndex].Id, userId.Value, UserAgreementAcceptState.Rejected);
				}
				else
				{
					await Client.UserAgreementSetStateAsync(UserAgreements[currentUserAgreementIndex].Id, userId.Value, UserAgreementAcceptState.Accepted);
				}
			}
			do
			{
				currentUserAgreementIndex++;
			}
			while (currentUserAgreementIndex < UserAgreements.Count && userAgreementInitialStates.Where((UserAgreementState a) => a.UserAgreementId == UserAgreements[currentUserAgreementIndex].Id).Any());
			if (UserAgreements.Count > currentUserAgreementIndex)
			{
				CurrentUserAgreementText = UserAgreements[currentUserAgreementIndex].Agreement;
				CurrentUserAgreementIgnoreState = UserAgreements[currentUserAgreementIndex].IgnoreState;
				CurrentUserAgreementAccepted = false;
			}
			else
			{
				Client.RaiseUserAgreementsLoaded(null, hasPendingUserAgreements: false, null, null);
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnAcceptAgreementCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\UserAgreementsViewModel.cs", 136);
		}
	}

	private bool OnCanRejectAgreementCommand(object param)
	{
		return true;
	}

	private async void OnRejectAgreementCommand(object param)
	{
		await Client.LogoutAsync();
	}

	private void Client_UserAgreementsLoaded(object sender, UserAgreementsLoadedEventArgs e)
	{
		userId = e.UserId;
		UserAgreements = e.UserAgreements;
		userAgreementInitialStates = e.UserAgreementStates;
		if (UserAgreements == null || userAgreementInitialStates == null)
		{
			return;
		}
		int i;
		for (i = 0; i < UserAgreements.Count; i++)
		{
			if (!userAgreementInitialStates.Where((UserAgreementState a) => a.UserAgreementId == UserAgreements[i].Id).Any())
			{
				currentUserAgreementIndex = i;
				CurrentUserAgreementText = UserAgreements[currentUserAgreementIndex].Agreement;
				CurrentUserAgreementIgnoreState = UserAgreements[currentUserAgreementIndex].IgnoreState;
				CurrentUserAgreementAccepted = false;
				break;
			}
		}
	}

	protected override void OnPropertyChanged(object sender, PropertyChangedEventArgsEx args)
	{
		base.OnPropertyChanged(sender, args);
		if (args.PropertyName == "CurrentUserAgreementText" || args.PropertyName == "CurrentUserAgreementAccepted")
		{
			AcceptAgreementCommand.RaiseCanExecuteChanged();
		}
	}

	public void OnImportsSatisfied()
	{
		Client.UserAgreementsLoaded += Client_UserAgreementsLoaded;
	}
}
