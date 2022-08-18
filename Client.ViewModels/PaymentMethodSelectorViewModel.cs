using System;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using Client.Views;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class PaymentMethodSelectorViewModel : ExecuteViewModelBase
{
	private bool result;

	private int? preferedPaymentMethodId;

	private CancellationTokenSource cts;

	private ICollectionView paymentMethodsView;

	[Import]
	private GizmoClient Client { get; set; }

	[Import]
	private IShellWindow Shell { get; set; }

	[Import]
	private IViewModelLocatorList<IPaymentMethodViewModel> PaymentMethodLocator { get; set; }

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

	public int? PreferedPaymentMethodId => preferedPaymentMethodId;

	protected override bool OnCanExecuteCommand(object param)
	{
		return true;
	}

	protected override void OnExecuteCommand(object param)
	{
		preferedPaymentMethodId = (int?)param;
		result = true;
		cts.Cancel();
	}

	public async Task<bool> ShowOverlayAsync()
	{
		IPaymentMethodSelectorView exportedValue = Client.GetExportedValue<IPaymentMethodSelectorView>();
		exportedValue.DataContext = this;
		cts = new CancellationTokenSource();
		try
		{
			await Shell.ShowOverlayAsync(exportedValue, cts.Token);
		}
		catch (OperationCanceledException)
		{
		}
		return result;
	}

	private bool PaymentMethodFilter(object obj)
	{
		if (obj is PaymentMethodViewModel paymentMethodViewModel)
		{
			return paymentMethodViewModel.PaymentMethodId != -4;
		}
		return false;
	}
}
