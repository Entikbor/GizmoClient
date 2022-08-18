using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using ServerService;
using SharedLib;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
public class ProductOrderViewModel : ItemsListViewModelBaseOfType<ProductOrderLineViewModel, NullView>, IProductOrderViewModel
{
	private decimal total;

	private int pointsTotal;

	private int award;

	private int? paymentMethodId;

	private ICollectionView paymentMethodsView;

	private readonly SemaphoreSlim MODIFY_LOCK = new SemaphoreSlim(1, 1);

	private string userNote;

	private bool addUserNote = true;

	[Import]
	private GizmoClient Client { get; set; }

	[Import]
	private IViewModelLocatorList<IProductViewModel> ProductLocator { get; set; }

	[Import]
	private IViewModelLocatorList<IPaymentMethodViewModel> PaymentMethodLocator { get; set; }

	[Import(AllowDefault = true)]
	private IDialogService DialogService { get; set; }

	public decimal Total
	{
		get
		{
			return total;
		}
		set
		{
			SetProperty(ref total, value, "Total");
			RaisePropertyChanged("RequiresCash");
		}
	}

	public int PointsTotal
	{
		get
		{
			return pointsTotal;
		}
		set
		{
			SetProperty(ref pointsTotal, value, "PointsTotal");
			RaisePropertyChanged("RequiresPoints");
		}
	}

	public int Award
	{
		get
		{
			return award;
		}
		protected set
		{
			SetProperty(ref award, value, "Award");
			RaisePropertyChanged("HasAward");
		}
	}

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

	public ICollectionView PaymentMethods
	{
		get
		{
			if (paymentMethodsView == null)
			{
				paymentMethodsView = new ListCollectionView(PaymentMethodLocator.ListSource as IList)
				{
					Filter = PaymentMethodFilter
				};
				paymentMethodsView.SortDescriptions.Add(new SortDescription("DisplayOrder", ListSortDirection.Ascending));
			}
			return paymentMethodsView;
		}
	}

	[MaxLength(255)]
	public string UserNote
	{
		get
		{
			return userNote;
		}
		set
		{
			SetPropertyAndValidate(ref userNote, value, "UserNote");
		}
	}

	public bool AddUserNote
	{
		get
		{
			return addUserNote;
		}
		set
		{
			SetProperty(ref addUserNote, value, "AddUserNote");
		}
	}

	public bool RequiresCash => Total > 0m;

	public bool RequiresPoints => PointsTotal > 0;

	public bool HasAward => Award > 0;

	[ImportingConstructor]
	public ProductOrderViewModel()
	{
		base.IsItemsPropertyChangeTracking = true;
	}

	protected override bool OnCanAcceptCommand(object parameter)
	{
		return Items.Count > 0;
	}

	protected override async void OnAcceptCommand(object parameter)
	{
		try
		{
			UserBalance userBalance = await Client.UserBalanceGetAsync();
			decimal currentBalance = userBalance.Balance;
			int points = userBalance.Points;
			if (PointsTotal > 0 && PointsTotal > points)
			{
				string localizedString = Client.GetLocalizedString("PRODUCT_ORDER_INSUFFICIENT_POINTS_MESSAGE");
				await (DialogService?.ShowAcceptDialogAsync(localizedString));
				return;
			}
			int? num = null;
			if (RequiresCash)
			{
				PaymentMethodSelectorViewModel view = Client.GetExportedValue<PaymentMethodSelectorViewModel>();
				if (!(await view.ShowOverlayAsync()))
				{
					return;
				}
				num = view.PreferedPaymentMethodId;
				if (num == -3 && Total > currentBalance)
				{
					string localizedString2 = Client.GetLocalizedString("PRODUCT_ORDER_INSUFFICIENT_DEPOSITS_MESSAGE");
					await (DialogService?.ShowAcceptDialogAsync(localizedString2));
					return;
				}
			}
			List<ClientProductOrderEntry> entries = Items.Select((ProductOrderLineViewModel item) => new ClientProductOrderEntry
			{
				ProductId = item.ProductId,
				Quantity = item.Quantity,
				PayType = item.PayType
			}).ToList();
			ClientProductOrder order = new ClientProductOrder
			{
				Entries = entries,
				UserNote = ((AddUserNote && !string.IsNullOrEmpty(UserNote)) ? UserNote : null)
			};
			ProductOrderResult result = await Client.ProductOrderCreateAsync(order, num);
			if (result.Result != OrderResult.Failed)
			{
				await ResetAsync();
				if (result.Result == OrderResult.Completed)
				{
					await Client.NotifyOrderStatusAsync(OrderStatus.Completed);
				}
			}
			else
			{
				await Client.NotifyOrderFailedAsync(result.FailReason);
			}
		}
		catch
		{
			try
			{
				string localizedString3 = Client.GetLocalizedString("MESSAGE_ORDER_ERROR");
				await DialogService.ShowAcceptDialogAsync(localizedString3);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex2)
			{
				Client.TraceWrite(ex2, "OnAcceptCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Ordering\\ProductOrderViewModel.cs", 266);
			}
		}
	}

	protected override async void OnClearCommand(object param)
	{
		_ = 1;
		try
		{
			string localizedString = Client.GetLocalizedString("PRODUCT_ORDER_CONFIRM_CLEAR_MESSAGE");
			if (await DialogService.ShowAcceptDialogAsync(localizedString, MessageDialogButtons.AcceptCancel))
			{
				await ResetAsync();
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnClearCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Ordering\\ProductOrderViewModel.cs", 283);
		}
	}

	public bool SetPaymentMethod(int? paymentMethodId)
	{
		if (paymentMethodId.HasValue)
		{
			if ((from method in PaymentMethodLocator.EnumerableSource.ToList()
				where method.PaymentMethodId == paymentMethodId
				select method).Any())
			{
				PaymentMethodId = paymentMethodId;
				return true;
			}
			return false;
		}
		PaymentMethodId = null;
		return true;
	}

	public async Task AddProductAsync(int productId, OrderLinePayType payType)
	{
		_ = 3;
		try
		{
			int? paymentMethodId = ((payType == OrderLinePayType.Points) ? null : PaymentMethodId);
			if (!(await MODIFY_LOCK.WaitAsync(TimeSpan.Zero).ConfigureAwait(continueOnCapturedContext: false)))
			{
				return;
			}
			try
			{
				string localizedString;
				switch (await Client.ProductOrderPassAsync(productId, paymentMethodId))
				{
				case ProductOrderPassResult.ClientOrderDisallowed:
					localizedString = Client.GetLocalizedString("PRODUCT_ORDER_PASS_RESULT_CLIENT_ORDER_DISALLOWED_MESSAGE");
					goto IL_01fa;
				case ProductOrderPassResult.UserGroupDisallowed:
					localizedString = Client.GetLocalizedString("PRODUCT_ORDER_PASS_RESULT_DISALLOWED_USER_GROUP_MESSAGE");
					goto IL_01fa;
				case ProductOrderPassResult.SaleDisallowed:
					localizedString = Client.GetLocalizedString("PRODUCT_ORDER_PASS_RESULT_SALE_DISALLOWED_MESSAGE");
					goto IL_01fa;
				case ProductOrderPassResult.GuestSaleDisallowed:
					localizedString = Client.GetLocalizedString("PRODUCT_ORDER_PASS_RESULT_GUEST_SALE_DISALLOWED_MESSAGE");
					goto IL_01fa;
				case ProductOrderPassResult.OutOfStock:
					localizedString = Client.GetLocalizedString("PRODUCT_ORDER_PASS_RESULT_OUT_OF_STOCK_MESSAGE");
					goto IL_01fa;
				case ProductOrderPassResult.PeriodDisallowed:
					localizedString = Client.GetLocalizedString("PRODUCT_ORDER_PASS_RESULT_PURCHASE_PERIOD_DISALLOWED_MESSAGE");
					goto IL_01fa;
				default:
					localizedString = Client.GetLocalizedString("PRODUCT_ORDER_PASS_RESULT_ERROR_MESSAGE");
					goto IL_01fa;
				case ProductOrderPassResult.Success:
					{
						IProductViewModel productViewModel2 = ProductLocator.TryGetViewModel(productId);
						if (productViewModel2 is ProductViewModel productViewModel)
						{
							if (productViewModel == null || (((productViewModel.PointsPrice.HasValue && productViewModel.PointsPrice.Value > 0 && productViewModel.PurchaseOptions == PurchaseOptionType.And) || (productViewModel.PurchaseOptions == PurchaseOptionType.Or && payType == OrderLinePayType.Points)) && !(await VerifyOrderPointsWithUserBalance(productViewModel.PointsPrice.Value))))
							{
								return;
							}
							ProductOrderLineViewModel exportedValue = Client.GetExportedValue<ProductOrderLineViewModel>();
							exportedValue.ProductId = productId;
							exportedValue.PayType = ((productViewModel.PurchaseOptions == PurchaseOptionType.Or) ? payType : OrderLinePayType.Mixed);
							exportedValue.Quantity = 1;
							exportedValue.Parent = this;
							Add(exportedValue);
						}
						_ = PaymentMethodId.HasValue;
						break;
					}
					IL_01fa:
					await DialogService.ShowAcceptDialogAsync(localizedString, MessageDialogButtons.Accept, default(CancellationToken));
					return;
				}
			}
			catch
			{
				throw;
			}
			finally
			{
				MODIFY_LOCK.Release();
				ResetCommands();
			}
		}
		catch (Exception ex)
		{
			Client.LogAddError("AddProductAsync", ex, LogCategories.Operation);
		}
	}

	public async Task RemoveProductAsync(ProductOrderLineViewModel orderLine)
	{
		try
		{
			if (orderLine == null || !(await MODIFY_LOCK.WaitAsync(TimeSpan.Zero).ConfigureAwait(continueOnCapturedContext: false)))
			{
				return;
			}
			try
			{
				Remove(orderLine);
			}
			catch
			{
				throw;
			}
			finally
			{
				MODIFY_LOCK.Release();
				ResetCommands();
			}
		}
		catch (Exception ex)
		{
			Client.LogAddError("RemoveProductAsync", ex, LogCategories.Operation);
		}
	}

	private void ResetPaymentMethod()
	{
		List<IPaymentMethodViewModel> source = PaymentMethodLocator.EnumerableSource.ToList();
		if (source.Any((IPaymentMethodViewModel method) => method.PaymentMethodId == -3))
		{
			PaymentMethodId = -3;
			return;
		}
		if (source.Any((IPaymentMethodViewModel method) => method.PaymentMethodId == -1))
		{
			PaymentMethodId = -1;
		}
		PaymentMethodId = (from method in source
			orderby method.DisplayOrder
			select method.PaymentMethodId).FirstOrDefault();
	}

	public async Task<bool> VerifyOrderPointsWithUserBalance(int points)
	{
		int points2 = (await Client.UserBalanceGetAsync()).Points;
		int num = Items.Sum((ProductOrderLineViewModel a) => a.TotalPoints);
		if (num + points > 0 && num + points > points2)
		{
			string localizedString = Client.GetLocalizedString("PRODUCT_ORDER_INSUFFICIENT_POINTS_MESSAGE");
			await (DialogService?.ShowAcceptDialogAsync(localizedString));
			return false;
		}
		return true;
	}

	private void RecalculateOrder()
	{
		foreach (ProductOrderLineViewModel item in Items)
		{
			if (item.Product is ProductViewModel productViewModel)
			{
				int? pointsPrice = productViewModel.PointsPrice;
				decimal price = productViewModel.Price;
				_ = item.PayType;
				if (item.PayType == OrderLinePayType.Cash || item.PayType == OrderLinePayType.Mixed)
				{
					item.Total = price * (decimal)item.Quantity;
				}
				else
				{
					item.Total = 0m;
				}
				if (item.PayType == OrderLinePayType.Points || item.PayType == OrderLinePayType.Mixed)
				{
					item.TotalPoints = pointsPrice.GetValueOrDefault() * item.Quantity;
				}
				else
				{
					item.TotalPoints = 0;
				}
			}
		}
		Total = Items.Select((ProductOrderLineViewModel ol) => ol.Total).DefaultIfEmpty().Sum();
		PointsTotal = Items.Select((ProductOrderLineViewModel ol) => ol.TotalPoints).DefaultIfEmpty().Sum();
		Award = (from ol in Items
			where ol.PayType != OrderLinePayType.Points
			select (ol.Product as ProductViewModel).Award.GetValueOrDefault() * ol.Quantity).DefaultIfEmpty().Sum();
	}

	private bool PaymentMethodFilter(object obj)
	{
		if (obj is PaymentMethodViewModel paymentMethodViewModel)
		{
			return paymentMethodViewModel.PaymentMethodId != -4;
		}
		return false;
	}

	protected override void OnReset()
	{
		base.OnReset();
		UserNote = null;
		ResetPaymentMethod();
	}

	protected override void OnAdded(ProductOrderLineViewModel item)
	{
		base.OnAdded(item);
		RecalculateOrder();
	}

	protected override void OnRemoved(ProductOrderLineViewModel item)
	{
		base.OnRemoved(item);
		RecalculateOrder();
	}

	protected override void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		base.OnItemPropertyChanged(sender, e);
		if (e.PropertyName == "Quantity")
		{
			RecalculateOrder();
		}
	}
}
