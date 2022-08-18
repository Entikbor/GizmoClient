using System.ComponentModel.Composition;
using SharedLib;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class PaymentMethodViewModel : PropertyChangedBase, IPaymentMethodViewModel
{
	private int? paymentMethodId;

	private string name;

	private string description;

	private int displayOrder;

	private string localizedName;

	[IgnorePropertyModification]
	public int? PaymentMethodId
	{
		get
		{
			return paymentMethodId;
		}
		set
		{
			SetProperty(ref paymentMethodId, value, "PaymentMethodId");
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

	[IgnorePropertyModification]
	public string LocalizedName
	{
		get
		{
			return localizedName;
		}
		set
		{
			SetProperty(ref localizedName, value, "LocalizedName");
		}
	}

	[IgnorePropertyModification]
	public string Description
	{
		get
		{
			return description;
		}
		set
		{
			SetProperty(ref description, value, "Description");
		}
	}

	[IgnorePropertyModification]
	public int DisplayOrder
	{
		get
		{
			return displayOrder;
		}
		set
		{
			SetProperty(ref displayOrder, value, "DisplayOrder");
		}
	}

	[IgnorePropertyModification]
	public bool IsBuiltInMethod => PaymentMethodId < 0;
}
