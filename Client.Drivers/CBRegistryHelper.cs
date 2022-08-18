using System;
using callback.CBFSFilter;

namespace Client.Drivers;

public class CBRegistryHelper : CBHelperBase<Cbregistry>
{
	public CBRegistryHelper()
		: base(new Cbregistry(), "cbregistry.cab", new Version(20, 0, 8124, 0))
	{
		base.Component.RuntimeLicense = "43464E4641444E585246323032323034313136474E393234353500000000000000000000000000004D555A394341575400005641314758484A34474D56310000";
	}

	public override bool Install(string appGuid)
	{
		if (string.IsNullOrWhiteSpace(appGuid))
		{
			throw new ArgumentNullException("appGuid");
		}
		return base.Component.Install(base.DriverFilePath, appGuid, null, 1);
	}

	public override bool IsInstalled(string appGuid, out Version version)
	{
		if (string.IsNullOrWhiteSpace(appGuid))
		{
			throw new ArgumentNullException("appGuid");
		}
		version = null;
		if (base.Component.GetDriverStatus(appGuid) == 0)
		{
			return false;
		}
		long driverVersion = base.Component.GetDriverVersion(appGuid);
		version = CBVersionHelper.Parse(driverVersion);
		return true;
	}

	public override bool Uninstall(string appGuid)
	{
		if (string.IsNullOrWhiteSpace(appGuid))
		{
			throw new ArgumentNullException("appGuid");
		}
		return base.Component.Uninstall(base.DriverFilePath, appGuid, null, 3);
	}

	protected override void InitializeComponent(string appGuid)
	{
		base.Component.Initialize(appGuid);
	}
}
