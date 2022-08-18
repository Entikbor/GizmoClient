using System;
using System.Collections.Generic;
using Gizmo.Web.Api.Models;

namespace Client;

public class UserAgreementsLoadedEventArgs : EventArgs
{
	public int? UserId { get; set; }

	public bool HasPendingUserAgreements { get; set; }

	public List<UserAgreement> UserAgreements { get; set; }

	public List<UserAgreementState> UserAgreementStates { get; set; }
}
