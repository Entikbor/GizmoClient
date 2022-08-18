using System.ComponentModel.Composition;
using SharedLib;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class AppEnterpriseDisplayViewModel : PropertyChangedBase, IAppEnterpriseDisplayViewModel
{
	private int id;

	private string name;

	[IgnorePropertyModification]
	public int Id
	{
		get
		{
			return id;
		}
		set
		{
			SetProperty(ref id, value, "Id");
		}
	}

	[IgnorePropertyModification]
	public string Name
	{
		get
		{
			return name;
		}
		set
		{
			SetProperty(ref name, value, "Name");
		}
	}

	[ImportingConstructor]
	public AppEnterpriseDisplayViewModel()
	{
	}
}
