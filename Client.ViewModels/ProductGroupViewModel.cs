using System.ComponentModel.Composition;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class ProductGroupViewModel : ViewModel, IProductGroupViewModel
{
	private string name;

	private int productGroupId;

	public int ProductGroupId
	{
		get
		{
			return productGroupId;
		}
		set
		{
			SetProperty(ref productGroupId, value, "ProductGroupId");
		}
	}

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
}
