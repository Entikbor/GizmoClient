using System.ComponentModel.Composition;
using SharedLib;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class SectionModuleViewModel<T> : ViewModel
{
	private string title;

	private string description;

	private string iconResource;

	private string template;

	private bool isSelected;

	private T module;

	private int displayOrder;

	[IgnorePropertyModification]
	public string Title
	{
		get
		{
			return title;
		}
		set
		{
			SetProperty(ref title, value, "Title");
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
	public string IconResource
	{
		get
		{
			return iconResource;
		}
		set
		{
			SetProperty(ref iconResource, value, "IconResource");
		}
	}

	[IgnorePropertyModification]
	public bool IsSelected
	{
		get
		{
			return isSelected;
		}
		set
		{
			SetProperty(ref isSelected, value, "IsSelected");
		}
	}

	[IgnorePropertyModification]
	public T Module
	{
		get
		{
			return module;
		}
		set
		{
			SetProperty(ref module, value, "Module");
		}
	}

	[IgnorePropertyModification]
	public string Template
	{
		get
		{
			return template;
		}
		set
		{
			SetProperty(ref template, value, "Template");
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
	public IClientSkinModuleMetadata MetaData { get; set; }
}
