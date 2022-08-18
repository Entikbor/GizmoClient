using System;
using System.ComponentModel.DataAnnotations;
using SharedLib;

namespace Client;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class SexRequiredAttribute : ValidationAttribute
{
	public override bool IsValid(object value)
	{
		if (value is Sex sex)
		{
			return sex != Sex.Unspecified;
		}
		return false;
	}
}
