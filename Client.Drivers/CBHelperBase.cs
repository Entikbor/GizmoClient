using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace Client.Drivers;

public abstract class CBHelperBase<TComponent> : IDisposable where TComponent : Component
{
	private static readonly string PROCESS_DIRECTORY = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

	protected bool IsDisposed { get; private set; }

	protected TComponent Component { get; set; }

	public Version BundledVersion { get; protected set; }

	public string DriverFilePath { get; private set; }

	public static bool IsInitialized { get; protected set; }

	public CBHelperBase(TComponent component, string driverFileName, Version bundledVersion)
	{
		Component = component ?? throw new ArgumentNullException("component");
		if (string.IsNullOrWhiteSpace(driverFileName))
		{
			throw new ArgumentNullException("driverFileName");
		}
		DriverFilePath = Path.Combine(PROCESS_DIRECTORY, "Drivers", driverFileName);
		BundledVersion = bundledVersion ?? throw new ArgumentNullException("bundledVersion");
	}

	~CBHelperBase()
	{
		OnDisposing(isDisposing: false);
	}

	protected virtual bool DriverFileExist()
	{
		if (string.IsNullOrWhiteSpace(DriverFilePath))
		{
			throw new ArgumentNullException("DriverFilePath");
		}
		return File.Exists(DriverFilePath);
	}

	public virtual void Initialize(string appGuid)
	{
		if (!IsInstalledAndLatest(appGuid))
		{
			if (!DriverFileExist())
			{
				return;
			}
			Install(appGuid);
		}
		InitializeComponent(appGuid);
		IsInitialized = true;
	}

	public abstract bool Install(string appGuid);

	public abstract bool Uninstall(string appGuid);

	public abstract bool IsInstalled(string appGuid, out Version version);

	public virtual bool IsInstalledAndLatest(string appGuid)
	{
		if (IsInstalled(appGuid, out var version))
		{
			return version >= BundledVersion;
		}
		return false;
	}

	protected abstract void InitializeComponent(string appGuid);

	public void Dispose()
	{
		OnDisposing(isDisposing: true);
	}

	protected virtual void OnDisposing(bool isDisposing)
	{
		if (isDisposing && !IsDisposed)
		{
			try
			{
				Component.Dispose();
			}
			catch (ObjectDisposedException)
			{
			}
			IsDisposed = true;
		}
	}
}
