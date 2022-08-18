using System;

namespace Client.Drivers;

public static class CBVersionHelper
{
	public static Version Parse(long version)
	{
		int major = (int)(version >> 48);
		int minor = (int)((version >> 32) & 0xFFFF);
		int build = (int)((version >> 16) & 0xFFFF);
		int revision = (int)(version & 0xFFFF);
		return new Version(major, minor, build, revision);
	}
}
