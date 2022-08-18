using System.ComponentModel.Composition;
using SharedLib;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class ProductOrderLineViewModel : ViewModel
{
	private decimal total;

	private int totalPoints;

	private int quantity;

	private int productId;

	private OrderLinePayType payType;

	private IExecutionChangedAwareCommand addCommand;

	private IExecutionChangedAwareCommand removeCommand;

	private IExecutionChangedAwareCommand deleteCommand;

	[Import]
	private IViewModelLocatorList<IProductViewModel> ProductLocator { get; set; }

	[Import]
	private ProductOrderViewModel OrderViewModel { get; set; }

	public IExecutionChangedAwareCommand AddCommand
	{
		get
		{
			if (addCommand == null)
			{
				addCommand = new SimpleCommand<object, object>(OnCanAddCommand, OnAddCommand);
			}
			return addCommand;
		}
	}

	public IExecutionChangedAwareCommand RemoveCommand
	{
		get
		{
			if (removeCommand == null)
			{
				removeCommand = new SimpleCommand<object, object>(OnCanRemoveCommand, OnRemoveCommand);
			}
			return removeCommand;
		}
	}

	public IExecutionChangedAwareCommand DeleteCommand
	{
		get
		{
			if (deleteCommand == null)
			{
				deleteCommand = new SimpleCommand<object, object>(OnCanDeleteCommand, OnDeleteCommand);
			}
			return deleteCommand;
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

	public decimal Total
	{
		get
		{
			return total;
		}
		set
		{
			SetProperty(ref total, value, "Total");
		}
	}

	public int TotalPoints
	{
		get
		{
			return totalPoints;
		}
		set
		{
			SetProperty(ref totalPoints, value, "TotalPoints");
		}
	}

	public int Quantity
	{
		get
		{
			return quantity;
		}
		set
		{
			SetProperty(ref quantity, value, "Quantity");
		}
	}

	public OrderLinePayType PayType
	{
		get
		{
			return payType;
		}
		set
		{
			SetProperty(ref payType, value, "PayType");
		}
	}

	public IProductViewModel Product => ProductLocator.TryGetViewModel(productId);

	public ProductOrderViewModel Parent { get; set; }

	private bool OnCanAddCommand(object param)
	{
		return true;
	}

	private async void OnAddCommand(object parameter)
	{
		ProductViewModel productViewModel = Product as ProductViewModel;
		if (((!productViewModel.PointsPrice.HasValue || productViewModel.PointsPrice.Value <= 0 || productViewModel.PurchaseOptions != 0) && (productViewModel.PurchaseOptions != PurchaseOptionType.Or || PayType != OrderLinePayType.Points)) || await Parent.VerifyOrderPointsWithUserBalance(productViewModel.PointsPrice.Value))
		{
			Quantity++;
		}
	}

	private bool OnCanRemoveCommand(object param)
	{
		if (quantity - 1 <= 0)
		{
			return false;
		}
		return true;
	}

	private void OnRemoveCommand(object parameter)
	{
		Quantity--;
	}

	private bool OnCanDeleteCommand(object param)
	{
		return true;
	}

	private async void OnDeleteCommand(object parameter)
	{
		await OrderViewModel.RemoveProductAsync(this);
	}

	protected override void OnResetCommands()
	{
		base.OnResetCommands();
		AddCommand?.RaiseCanExecuteChanged();
		RemoveCommand?.RaiseCanExecuteChanged();
		DeleteCommand?.RaiseCanExecuteChanged();
	}

	protected override void OnPropertyChanged(object sender, PropertyChangedEventArgsEx args)
	{
		base.OnPropertyChanged(sender, args);
		if (args.PropertyName == "Quantity")
		{
			ResetCommands();
		}
	}
}
