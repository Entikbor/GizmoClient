using System;
using callback.CBFSConnect;

namespace Client.Drivers;

public class CBConnectHelper : CBHelperBase<Cbfs>
{
	public Version BundledPNPBusVersion { get; private set; }

	public CBConnectHelper()
		: base(new Cbfs(), "cbfs.cab", new Version(20, 0, 8132, 0))
	{
		BundledPNPBusVersion = new Version(1, 0, 0, 4);
		base.Component.RuntimeLicense = "43434E4641444E585246323032323034313136474E393233343000000000000000000000000000004D555A394341575400004655314E5256504E3748464D0000";
	}

	public override bool Install(string appGuid)
	{
		if (string.IsNullOrWhiteSpace(appGuid))
		{
			throw new ArgumentNullException("appGuid");
		}
		if (!Install(appGuid))
		{
			return false;
		}
		if (!Install(appGuid, CBFS_MODULE.MODULE_PNP_BUS))
		{
			return false;
		}
		return true;
	}

	public override bool Uninstall(string appGuid)
	{
		if (string.IsNullOrWhiteSpace(appGuid))
		{
			throw new ArgumentNullException("appGuid");
		}
		return base.Component.Uninstall(base.DriverFilePath, appGuid, string.Empty, 3) == 0;
	}

	public override bool IsInstalled(string appGuid, out Version version)
	{
		if (string.IsNullOrWhiteSpace(appGuid))
		{
			throw new ArgumentNullException("appGuid");
		}
		if (!IsInstalled(appGuid, out version))
		{
			return false;
		}
		if (!IsInstalled(appGuid, out var _, CBFS_MODULE.MODULE_PNP_BUS))
		{
			return false;
		}
		return true;
	}

	public override bool IsInstalledAndLatest(string appGuid)
	{
		if (string.IsNullOrWhiteSpace(appGuid))
		{
			throw new ArgumentNullException("appGuid");
		}
		if (IsInstalled(appGuid, out var version))
		{
			return version >= base.BundledVersion;
		}
		return false;
	}

	public override void Initialize(string appGuid)
	{
		if (string.IsNullOrWhiteSpace(appGuid))
		{
			throw new ArgumentNullException("appGuid");
		}
		if (!IsInstalledAndLatest(appGuid))
		{
			if (!DriverFileExist())
			{
				return;
			}
			Install(appGuid);
		}
		if (!IsPNPInstalledAndLatest(appGuid))
		{
			if (!DriverFileExist())
			{
				return;
			}
			Install(appGuid, CBFS_MODULE.MODULE_PNP_BUS);
		}
		InitializeComponent(appGuid);
		CBHelperBase<Cbfs>.IsInitialized = true;
	}

	protected override void InitializeComponent(string appGuid)
	{
		base.Component.Initialize(appGuid);
	}

	public bool IsInstalled(string appGuid, out Version version, CBFS_MODULE module = CBFS_MODULE.MODULE_DRIVER)
	{
		if (string.IsNullOrWhiteSpace(appGuid))
		{
			throw new ArgumentNullException("appGuid");
		}
		version = null;
		CBDRIVER_STATUS driverStatus = (CBDRIVER_STATUS)base.Component.GetDriverStatus(appGuid, (int)module);
		if (driverStatus == CBDRIVER_STATUS.MODULE_STATUS_NOT_PRESENT || driverStatus == CBDRIVER_STATUS.MODULE_STATUS_STOPPED)
		{
			return false;
		}
		long moduleVersion = base.Component.GetModuleVersion(appGuid, (int)module);
		version = CBVersionHelper.Parse(moduleVersion);
		return true;
	}

	public bool IsPNPInstalledAndLatest(string appGuid)
	{
		if (string.IsNullOrWhiteSpace(appGuid))
		{
			throw new ArgumentNullException("appGuid");
		}
		if (IsInstalled(appGuid, out var version, CBFS_MODULE.MODULE_PNP_BUS))
		{
			return version >= BundledPNPBusVersion;
		}
		return false;
	}

	public bool Install(string appGuid, CBFS_MODULE modules = CBFS_MODULE.MODULE_DRIVER, CB_INSTALL_FLAGS flags = CB_INSTALL_FLAGS.INSTALL_REMOVE_OLD_VERSIONS)
	{
		if (string.IsNullOrWhiteSpace(appGuid))
		{
			throw new ArgumentNullException("appGuid");
		}
		return base.Component.Install(base.DriverFilePath, appGuid, null, (int)modules, (int)flags) == 0;
	}
}
