using System;
using System.ComponentModel.Composition;
using System.ComponentModel.DataAnnotations;
using IntegrationLib;
using SharedLib;
using SharedLib.User;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
[Export(typeof(IUserProfileEditViewModel))]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class UserProfileEditViewModel : ValidateViewModelBase<NullView>, IUserProfileEditViewModel, ISourceConverter<IUserProfile>
{
	private string firstName;

	private string lastName;

	private string phone;

	private string mobilePhone;

	private string city;

	private string address;

	private string email;

	private string country;

	private string postCode;

	private DateTime? birthDate;

	private Sex sex;

	private UserInfoTypes requiredUserInfo;

	private IExecutionChangedAwareCommand acceptCommand;

	private IExecutionChangedAwareCommand cancelCommand;

	[Import]
	private GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand AcceptCommand
	{
		get
		{
			return acceptCommand;
		}
		set
		{
			SetProperty(ref acceptCommand, value, "AcceptCommand");
		}
	}

	[IgnorePropertyModification]
	public IExecutionChangedAwareCommand CancelCommand
	{
		get
		{
			return cancelCommand;
		}
		set
		{
			SetProperty(ref cancelCommand, value, "CancelCommand");
		}
	}

	[CharacterOnly]
	[MaxLength(45)]
	public string FirstName
	{
		get
		{
			return firstName;
		}
		set
		{
			SetPropertyAndValidate(ref firstName, value, "FirstName");
		}
	}

	[CharacterOnly]
	[MaxLength(45)]
	public string LastName
	{
		get
		{
			return lastName;
		}
		set
		{
			SetPropertyAndValidate(ref lastName, value, "LastName");
		}
	}

	[MaxLength(20)]
	public string Phone
	{
		get
		{
			return phone;
		}
		set
		{
			SetPropertyAndValidate(ref phone, value, "Phone");
		}
	}

	[MaxLength(20)]
	public string MobilePhone
	{
		get
		{
			return mobilePhone;
		}
		set
		{
			SetPropertyAndValidate(ref mobilePhone, value, "MobilePhone");
		}
	}

	[MaxLength(254)]
	[EmailNullEmpty]
	public string Email
	{
		get
		{
			return email;
		}
		set
		{
			SetPropertyAndValidate(ref email, value, "Email");
		}
	}

	[MaxLength(45)]
	public string Country
	{
		get
		{
			return country;
		}
		set
		{
			SetPropertyAndValidate(ref country, value, "Country");
		}
	}

	[MaxLength(45)]
	public string City
	{
		get
		{
			return city;
		}
		set
		{
			SetPropertyAndValidate(ref city, value, "City");
		}
	}

	[MaxLength(255)]
	public string Address
	{
		get
		{
			return address;
		}
		set
		{
			SetPropertyAndValidate(ref address, value, "Address");
		}
	}

	[MaxLength(20)]
	public string PostCode
	{
		get
		{
			return postCode;
		}
		set
		{
			SetPropertyAndValidate(ref postCode, value, "PostCode");
		}
	}

	[Required]
	public DateTime? BirthDate
	{
		get
		{
			return birthDate;
		}
		set
		{
			SetPropertyAndValidate(ref birthDate, value, "BirthDate");
		}
	}

	public Sex Sex
	{
		get
		{
			return sex;
		}
		set
		{
			SetPropertyAndValidate(ref sex, value, "Sex");
			RaisePropertyChanged("IsMale");
			RaisePropertyChanged("IsFemale");
		}
	}

	[IgnorePropertyModification]
	public bool IsMale
	{
		get
		{
			return Sex == Sex.Male;
		}
		set
		{
			Sex = (value ? Sex.Male : Sex.Unspecified);
		}
	}

	[IgnorePropertyModification]
	public bool IsFemale
	{
		get
		{
			return Sex == Sex.Female;
		}
		set
		{
			Sex = (value ? Sex.Female : Sex.Unspecified);
		}
	}

	[IgnorePropertyModification]
	public UserInfoTypes RequiredUserInfo
	{
		get
		{
			return requiredUserInfo;
		}
		set
		{
			SetProperty(ref requiredUserInfo, value, "RequiredUserInfo");
		}
	}

	public void FromSource(IUserProfile source)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		FirstName = source.FirstName;
		LastName = source.LastName;
		Phone = source.Phone;
		MobilePhone = source.MobilePhone;
		Email = source.Email;
		BirthDate = source.BirthDate;
		Address = source.Address;
		Country = source.Country;
		City = source.City;
		PostCode = source.PostCode;
		Sex = source.Sex;
	}

	public IUserProfile ToSource()
	{
		return new UserProfileBase
		{
			Address = Address,
			BirthDate = BirthDate.GetValueOrDefault(),
			City = City,
			Country = Country,
			Email = Email,
			FirstName = FirstName,
			LastName = LastName,
			MobilePhone = MobilePhone,
			PostCode = PostCode,
			Phone = Phone,
			Sex = Sex
		};
	}

	protected override void AfterValidatePropery(string propertyName, object value)
	{
		base.AfterValidatePropery(propertyName, value);
		if (RequiredUserInfo.HasFlag(UserInfoTypes.FirstName) && string.IsNullOrWhiteSpace(FirstName))
		{
			AddError("FirstName", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("FirstName");
		}
		else if (RequiredUserInfo.HasFlag(UserInfoTypes.LastName) && string.IsNullOrWhiteSpace(LastName))
		{
			AddError("LastName", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("LastName");
		}
		else if (RequiredUserInfo.HasFlag(UserInfoTypes.Phone) && string.IsNullOrWhiteSpace(Phone))
		{
			AddError("Phone", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("Phone");
		}
		else if (RequiredUserInfo.HasFlag(UserInfoTypes.Mobile) && string.IsNullOrWhiteSpace(MobilePhone))
		{
			AddError("MobilePhone", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("MobilePhone");
		}
		else if (RequiredUserInfo.HasFlag(UserInfoTypes.Email) && string.IsNullOrWhiteSpace(Email))
		{
			AddError("Email", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("Email");
		}
		else if (RequiredUserInfo.HasFlag(UserInfoTypes.Country) && string.IsNullOrWhiteSpace(Country))
		{
			AddError("Country", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("Country");
		}
		else if (RequiredUserInfo.HasFlag(UserInfoTypes.City) && string.IsNullOrWhiteSpace(City))
		{
			AddError("City", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("City");
		}
		else if (RequiredUserInfo.HasFlag(UserInfoTypes.Address) && string.IsNullOrWhiteSpace(Address))
		{
			AddError("Address", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("Address");
		}
		else if (RequiredUserInfo.HasFlag(UserInfoTypes.PostCode) && string.IsNullOrWhiteSpace(PostCode))
		{
			AddError("PostCode", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("PostCode");
		}
		if (!RequiredUserInfo.HasFlag(UserInfoTypes.BirthDate))
		{
			RemoveError("BirthDate");
			RaiseErrorsChanged("BirthDate");
		}
	}

	protected override void OnPropertyChanged(object sender, PropertyChangedEventArgsEx args)
	{
		base.OnPropertyChanged(sender, args);
		AcceptCommand?.RaiseCanExecuteChanged();
	}

	protected override void OnResetCommands()
	{
		base.OnResetCommands();
		AcceptCommand?.RaiseCanExecuteChanged();
		CancelCommand?.RaiseCanExecuteChanged();
	}
}
