using System;
using CyClone.Core;
using SharedLib.Applications;

namespace Client;

public class ExecutionContextSyncInfo : IExecutionContextSyncInfo
{
	private IDeploymentProfile profile;

	private IcySync syncer;

	public IcySync Syncer
	{
		get
		{
			return syncer;
		}
		protected set
		{
			syncer = value;
		}
	}

	public IDeploymentProfile Profile
	{
		get
		{
			return profile;
		}
		protected set
		{
			profile = value;
		}
	}

	public ExecutionContextSyncInfo(IcySync syncer, IDeploymentProfile profile)
	{
		Syncer = syncer ?? throw new ArgumentNullException("syncer");
		Profile = profile ?? throw new ArgumentNullException("profile");
	}
}
