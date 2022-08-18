using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows.Data;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class AppLinksViewModel : ItemsListViewModelBaseOfType<AppLinkViewModel, NullView>, IPartImportsSatisfiedNotification
{
	private ListCollectionView MEDIA_LINKS_CV;

	private ListCollectionView URL_LINKS_CV;

	public ListCollectionView MediaLinks
	{
		get
		{
			if (MEDIA_LINKS_CV == null)
			{
				MEDIA_LINKS_CV = new ListCollectionView(Items as IList)
				{
					Filter = (object m) => (m as AppLinkViewModel)?.IsMedia ?? false
				};
			}
			return MEDIA_LINKS_CV;
		}
	}

	public ListCollectionView UrlLinks
	{
		get
		{
			if (URL_LINKS_CV == null)
			{
				URL_LINKS_CV = new ListCollectionView(Items as IList)
				{
					Filter = delegate(object m)
					{
						AppLinkViewModel obj = m as AppLinkViewModel;
						return obj != null && !obj.IsMedia;
					}
				};
			}
			return URL_LINKS_CV;
		}
	}

	protected override void OnItemsCollectionViewChanged(ICollectionView previousView, ICollectionView newView)
	{
		base.OnItemsCollectionViewChanged(previousView, newView);
		if (newView != null)
		{
			using (ItemsView.DeferRefresh())
			{
				ItemsView.SortDescriptions.Add(new SortDescription("DisplayOrder", ListSortDirection.Ascending));
			}
			using (UrlLinks.DeferRefresh())
			{
				UrlLinks.SortDescriptions.Add(new SortDescription("DisplayOrder", ListSortDirection.Ascending));
			}
			using (MediaLinks.DeferRefresh())
			{
				MediaLinks.SortDescriptions.Add(new SortDescription("DisplayOrder", ListSortDirection.Ascending));
			}
		}
	}

	public void OnImportsSatisfied()
	{
	}
}
