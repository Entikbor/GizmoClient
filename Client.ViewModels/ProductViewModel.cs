using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class ProductViewModel : ViewModel, IProductViewModel
{
	private int productId;

	private int productGropupId;

	private string name;

	private string description;

	private decimal price;

	private int? pointsPrice;

	private PurchaseOptionType purchaseOptions;

	private int? award;

	private IExecutionChangedAwareCommand addWithCashCommand;

	private IExecutionChangedAwareCommand addWithPointsCommand;

	private bool hasImage;

	private NotifyTaskCompletion<byte[]> imageTask;

	private DateTime addDate;

	[Import]
	private ProductOrderViewModel OrderViewModel { get; set; }

	[Import]
	private IAppImageHandler ImageHandler { get; set; }

	[Import]
	private IViewModelLocatorList<IPaymentMethodViewModel> PaymentMethodLocator { get; set; }

	[Import]
	private GizmoClient Client { get; set; }

	public IExecutionChangedAwareCommand AddWithCashCommand
	{
		get
		{
			if (addWithCashCommand == null)
			{
				addWithCashCommand = new SimpleCommand<object, object>(OnCanAddWithCashCommand, OnAddWithCahsCommand);
			}
			return addWithCashCommand;
		}
	}

	public IExecutionChangedAwareCommand AddWithPointsCommand
	{
		get
		{
			if (addWithPointsCommand == null)
			{
				addWithPointsCommand = new SimpleCommand<object, object>(OnCanAddWithPointsCommand, OnAddWithPointsCommand);
			}
			return addWithPointsCommand;
		}
	}

	public int ProductId
	{
		get
		{
			return productId;
		}
		set
		{
			SetProperty(ref productId, value, "ProductId");
		}
	}

	public int ProductGroupId
	{
		get
		{
			return productGropupId;
		}
		set
		{
			SetProperty(ref productGropupId, value, "ProductGroupId");
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

	public decimal Price
	{
		get
		{
			return price;
		}
		set
		{
			SetProperty(ref price, value, "Price");
			RaisePropertyChanged("CostMoney");
		}
	}

	public int? PointsPrice
	{
		get
		{
			return pointsPrice;
		}
		set
		{
			SetProperty(ref pointsPrice, value, "PointsPrice");
			RaisePropertyChanged("CostsPoints");
		}
	}

	public PurchaseOptionType PurchaseOptions
	{
		get
		{
			return purchaseOptions;
		}
		set
		{
			SetProperty(ref purchaseOptions, value, "PurchaseOptions");
		}
	}

	public int? Award
	{
		get
		{
			return award;
		}
		set
		{
			SetProperty(ref award, value, "Award");
		}
	}

	public ProductType Type { get; set; }

	public bool CostsPoints => PointsPrice > 0;

	public bool CostMoney => Price > 0m;

	public bool CanPurchaseWithPoints
	{
		get
		{
			if (!PaymentMethodLocator.EnumerableSource.Where((IPaymentMethodViewModel method) => method.PaymentMethodId == -4).Any())
			{
				return false;
			}
			if (PointsPrice > 0)
			{
				return PurchaseOptions == PurchaseOptionType.Or;
			}
			return false;
		}
	}

	public bool CanPurchaseWithCash
	{
		get
		{
			if (Price > 0m && !PaymentMethodLocator.EnumerableSource.Where((IPaymentMethodViewModel method) => method.PaymentMethodId != -4).Any())
			{
				return false;
			}
			if (PointsPrice > 0 && PurchaseOptions == PurchaseOptionType.And && !PaymentMethodLocator.EnumerableSource.Where((IPaymentMethodViewModel method) => method.PaymentMethodId == -4).Any())
			{
				return false;
			}
			return true;
		}
	}

	public bool HasImage
	{
		get
		{
			return hasImage;
		}
		set
		{
			SetProperty(ref hasImage, value, "HasImage");
		}
	}

	[IgnorePropertyModification]
	public NotifyTaskCompletion<byte[]> ImageData
	{
		get
		{
			if (imageTask == null)
			{
				imageTask = new NotifyTaskCompletion<byte[]>(GetImageHandler());
			}
			return imageTask;
		}
		internal set
		{
			SetProperty(ref imageTask, value, "ImageData");
		}
	}

	[IgnorePropertyModification]
	public DateTime AddDate
	{
		get
		{
			return addDate;
		}
		set
		{
			SetProperty(ref addDate, value, "AddDate");
		}
	}

	private async Task<byte[]> GetImageHandler()
	{
		return await ImageHandler.TryGetImageDataAsync(ProductId, ImageType.ProductDefault);
	}

	private bool OnCanAddWithCashCommand(object param)
	{
		return CanPurchaseWithCash;
	}

	private async void OnAddWithCahsCommand(object param)
	{
		try
		{
			await OrderViewModel.AddProductAsync(ProductId, OrderLinePayType.Cash);
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnAddWithCahsCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Ordering\\ProductViewModel.cs", 284);
		}
	}

	private bool OnCanAddWithPointsCommand(object param)
	{
		return CanPurchaseWithPoints;
	}

	private async void OnAddWithPointsCommand(object param)
	{
		try
		{
			await OrderViewModel.AddProductAsync(ProductId, OrderLinePayType.Points);
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnAddWithPointsCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Ordering\\ProductViewModel.cs", 300);
		}
	}
}
