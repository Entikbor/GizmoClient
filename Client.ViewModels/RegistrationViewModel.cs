using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Client.Views;
using Gizmo;
using Gizmo.Web.Api.Models;
using GizmoDALV2;
using GizmoDALV2.Entities;
using Newtonsoft.Json;
using ServerService;
using SharedLib;
using SharedLib.Configuration;
using SharedLib.Dispatcher.Exceptions;
using SharedLib.ViewModels;
using SkinInterfaces;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class RegistrationViewModel : SelectViewModelBaseOfType<CountryCodeViewModel, IRegisterView>, IPartImportsSatisfiedNotification
{
	private bool? confirmCodeValid;

	private string confirmCode;

	private string confrimMobilePhone;

	private string confirmEmailAddress;

	private string expectedCode;

	private List<UserAgreement> userAgreements;

	private int currentUserAgreementIndex;

	private string currentUserAgreementText;

	private bool currentUserAgreementIgnoreState;

	private bool currentUserAgreementAccepted;

	private Dictionary<int, UserAgreementAcceptState> userAgreementStates = new Dictionary<int, UserAgreementAcceptState>();

	private bool canSetMobilePhone;

	private bool canSetEmailAddress;

	private IExecutionChangedAwareCommand confirmCodeCommand;

	private IExecutionChangedAwareCommand sendConfirmationCommand;

	private IExecutionChangedAwareCommand acceptAgreementCommand;

	private IExecutionChangedAwareCommand rejectAgreementCommand;

	private IExecutionChangedAwareCommand registerCommand;

	private IExecutionChangedAwareCommand finishCommand;

	private readonly SemaphoreSlim usernameConfirmLock = new SemaphoreSlim(1, 1);

	private readonly SemaphoreSlim mobileConfirmLock = new SemaphoreSlim(1, 1);

	private RegisterStep currentStep;

	private Timer usernameConfirmTimer;

	private Timer verifyAvailabilityTimer;

	private Timer mobileConfirmTimer;

	private UserInfoTypes requiredUserInfo = UserInfoTypes.UserInformation | UserInfoTypes.BirthDate | UserInfoTypes.Country;

	private RegistrationVerificationMethod verificationMethod = RegistrationVerificationMethod.Email;

	private string firstName;

	private string lastName;

	private string emailAddress;

	private string country;

	private string username;

	private string password;

	private string repeatPassword;

	private string mobilePhone;

	private string phone;

	private string city;

	private string address;

	private string postCode;

	private DateTime? birthDate;

	private SharedLib.Sex sex;

	[IgnorePropertyModification]
	[Import]
	private GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	[Import(AllowDefault = true)]
	private IShellWindow Shell { get; set; }

	public IExecutionChangedAwareCommand RejectAgreementCommand
	{
		get
		{
			if (rejectAgreementCommand == null)
			{
				rejectAgreementCommand = new SimpleCommand<object, object>(OnCanRejectAgreementCommand, OnRejectAgreementCommand);
			}
			return rejectAgreementCommand;
		}
	}

	public IExecutionChangedAwareCommand AcceptAgreementCommand
	{
		get
		{
			if (acceptAgreementCommand == null)
			{
				acceptAgreementCommand = new SimpleCommand<object, object>(OnCanAcceptAgreementCommand, OnAcceptAgreementCommand);
			}
			return acceptAgreementCommand;
		}
	}

	public IExecutionChangedAwareCommand SendConfirmationCommand
	{
		get
		{
			if (sendConfirmationCommand == null)
			{
				sendConfirmationCommand = new SimpleCommand<object, object>(OnCanSendConfirmationCommand, OnSendConfirmationCommand);
			}
			return sendConfirmationCommand;
		}
	}

	public IExecutionChangedAwareCommand ConfirmCodeCommand
	{
		get
		{
			if (confirmCodeCommand == null)
			{
				confirmCodeCommand = new SimpleCommand<object, object>(OnCanConfirmCodeCommand, OnConfirmCodeCommand);
			}
			return confirmCodeCommand;
		}
	}

	public IExecutionChangedAwareCommand RegisterCommand
	{
		get
		{
			if (registerCommand == null)
			{
				registerCommand = new SimpleCommand<object, object>(OnCanRegisterCommand, OnRegisterCommand);
			}
			return registerCommand;
		}
	}

	public IExecutionChangedAwareCommand FinishCommand
	{
		get
		{
			if (finishCommand == null)
			{
				finishCommand = new SimpleCommand<object, object>(OnCanFinishCommand, OnFinishCommand);
			}
			return finishCommand;
		}
	}

	public RegisterStep CurrentStep
	{
		get
		{
			return currentStep;
		}
		set
		{
			SetProperty(ref currentStep, value, "CurrentStep");
			RaisePropertyChanged("CurrentStepIndex");
		}
	}

	public int CurrentStepIndex => (int)CurrentStep;

	[IgnorePropertyModification]
	public RegistrationVerificationMethod VerificationMethod
	{
		get
		{
			return verificationMethod;
		}
		set
		{
			SetProperty(ref verificationMethod, value, "VerificationMethod");
		}
	}

	[IgnorePropertyModification]
	public string ConfirmCode
	{
		get
		{
			return confirmCode;
		}
		set
		{
			SetProperty(ref confirmCode, value, "ConfirmCode");
		}
	}

	[IgnorePropertyModification]
	public string Token { get; set; }

	[IgnorePropertyModification]
	public string ExpectedCode
	{
		get
		{
			return expectedCode;
		}
		set
		{
			SetProperty(ref expectedCode, value, "ExpectedCode");
			RaisePropertyChanged("HasConfirmCode");
		}
	}

	[IgnorePropertyModification]
	public bool HasConfirmCode => !string.IsNullOrWhiteSpace(ExpectedCode);

	[StringLength(20)]
	[Phone]
	public string ConfirmMobilePhone
	{
		get
		{
			return confrimMobilePhone;
		}
		set
		{
			SetPropertyAndValidate(ref confrimMobilePhone, value, "ConfirmMobilePhone");
		}
	}

	[StringLength(254)]
	[EmailAddress]
	public string ConfirmEmailAddress
	{
		get
		{
			return confirmEmailAddress;
		}
		set
		{
			SetPropertyAndValidate(ref confirmEmailAddress, value, "ConfirmEmailAddress");
		}
	}

	[IgnorePropertyModification]
	public bool? IsConfirmCodeValid
	{
		get
		{
			return confirmCodeValid;
		}
		set
		{
			SetProperty(ref confirmCodeValid, value, "IsConfirmCodeValid");
		}
	}

	[IgnorePropertyModification]
	private bool? IsUsernameAvailable { get; set; }

	[IgnorePropertyModification]
	private bool? IsMobilePhoneAvailable { get; set; }

	[IgnorePropertyModification]
	private bool? IsEmailAvailable { get; set; }

	[IgnorePropertyModification]
	public bool CanSetMobilePhone
	{
		get
		{
			return canSetMobilePhone;
		}
		private set
		{
			SetProperty(ref canSetMobilePhone, value, "CanSetMobilePhone");
		}
	}

	[IgnorePropertyModification]
	public bool CanSetEmailAddress
	{
		get
		{
			return canSetEmailAddress;
		}
		private set
		{
			SetProperty(ref canSetEmailAddress, value, "CanSetEmailAddress");
		}
	}

	[Required]
	[StringLength(30)]
	[FileInvalidCharactersValidation]
	public string Username
	{
		get
		{
			return username;
		}
		set
		{
			SetProperty(ref username, value, "Username");
		}
	}

	[Required]
	[StringLength(25)]
	public string Password
	{
		get
		{
			return password;
		}
		set
		{
			SetPropertyAndValidate(ref password, value, "Password");
		}
	}

	[Required]
	[StringLength(25)]
	public string RepeatPassword
	{
		get
		{
			return repeatPassword;
		}
		set
		{
			SetPropertyAndValidate(ref repeatPassword, value, "RepeatPassword");
		}
	}

	[IgnorePropertyModification]
	public UserInfoTypes RequiredPersonalInfo
	{
		get
		{
			return requiredUserInfo;
		}
		set
		{
			SetProperty(ref requiredUserInfo, value, "RequiredPersonalInfo");
		}
	}

	[IgnorePropertyModification]
	public bool IsPersonalInfoRequired => RequiredPersonalInfo != UserInfoTypes.None;

	[IgnorePropertyModification]
	public List<UserAgreement> UserAgreements
	{
		get
		{
			return userAgreements;
		}
		protected set
		{
			SetProperty(ref userAgreements, value, "UserAgreements");
		}
	}

	[IgnorePropertyModification]
	public string CurrentUserAgreementText
	{
		get
		{
			return currentUserAgreementText;
		}
		protected set
		{
			SetProperty(ref currentUserAgreementText, value, "CurrentUserAgreementText");
		}
	}

	[IgnorePropertyModification]
	public bool CurrentUserAgreementIgnoreState
	{
		get
		{
			return currentUserAgreementIgnoreState;
		}
		protected set
		{
			SetProperty(ref currentUserAgreementIgnoreState, value, "CurrentUserAgreementIgnoreState");
		}
	}

	[IgnorePropertyModification]
	public bool CurrentUserAgreementAccepted
	{
		get
		{
			return currentUserAgreementAccepted;
		}
		set
		{
			SetProperty(ref currentUserAgreementAccepted, value, "CurrentUserAgreementAccepted");
		}
	}

	[IgnorePropertyModification]
	public bool HasUserAgreement
	{
		get
		{
			if (UserAgreements != null)
			{
				return UserAgreements.Count > 0;
			}
			return false;
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

	[StringLength(254)]
	[System.ComponentModel.DataAnnotations.EmailNullEmpty]
	public string EmailAddress
	{
		get
		{
			return emailAddress;
		}
		set
		{
			SetPropertyAndValidate(ref emailAddress, value, "EmailAddress");
		}
	}

	[StringLength(20)]
	[PhoneNullEmpty]
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

	[StringLength(20)]
	[PhoneNullEmpty]
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

	[StringLength(20)]
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

	[SexRequired]
	public SharedLib.Sex Sex
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
			return Sex == SharedLib.Sex.Male;
		}
		set
		{
			Sex = (value ? SharedLib.Sex.Male : SharedLib.Sex.Female);
		}
	}

	[IgnorePropertyModification]
	public bool IsFemale
	{
		get
		{
			return Sex == SharedLib.Sex.Female;
		}
		set
		{
			Sex = ((!value) ? SharedLib.Sex.Male : SharedLib.Sex.Female);
		}
	}

	[ImportingConstructor]
	public RegistrationViewModel(IRegisterView view)
		: base(view)
	{
	}

	private bool OnCanAcceptAgreementCommand(object param)
	{
		if (UserAgreements == null || currentUserAgreementIndex + 1 > UserAgreements.Count)
		{
			return false;
		}
		if (!UserAgreements[currentUserAgreementIndex].IgnoreState && !UserAgreements[currentUserAgreementIndex].IsRejectable)
		{
			return CurrentUserAgreementAccepted;
		}
		return true;
	}

	private void OnAcceptAgreementCommand(object param)
	{
		if (!UserAgreements[currentUserAgreementIndex].IgnoreState)
		{
			if (CurrentUserAgreementAccepted)
			{
				userAgreementStates[UserAgreements[currentUserAgreementIndex].Id] = UserAgreementAcceptState.Accepted;
			}
			else
			{
				userAgreementStates[UserAgreements[currentUserAgreementIndex].Id] = UserAgreementAcceptState.Rejected;
			}
		}
		if (UserAgreements.Count > currentUserAgreementIndex + 1)
		{
			currentUserAgreementIndex++;
			CurrentUserAgreementText = UserAgreements[currentUserAgreementIndex].Agreement;
			CurrentUserAgreementIgnoreState = UserAgreements[currentUserAgreementIndex].IgnoreState;
			CurrentUserAgreementAccepted = false;
		}
		else
		{
			ClientSettings settings = Client.Settings;
			if (settings != null && settings.RegistrationVerificationMethod == RegistrationVerificationMethod.None)
			{
				CurrentStep = RegisterStep.Registration;
			}
			else
			{
				CurrentStep = RegisterStep.Confirmation;
			}
		}
	}

	private bool OnCanRejectAgreementCommand(object param)
	{
		return true;
	}

	private void OnRejectAgreementCommand(object param)
	{
		Shell.HideCurrentOverlay(cancel: false);
	}

	private bool OnCanSendConfirmationCommand(object param)
	{
		switch (VerificationMethod)
		{
		case RegistrationVerificationMethod.MobilePhone:
			if (PropertyHasErrors("ConfirmMobilePhone"))
			{
				return false;
			}
			return IsMobilePhoneAvailable == true;
		case RegistrationVerificationMethod.Email:
			if (PropertyHasErrors("ConfirmEmailAddress"))
			{
				return false;
			}
			return IsEmailAvailable == true;
		default:
			return false;
		}
	}

	private async void OnSendConfirmationCommand(object param)
	{
		_ = 2;
		try
		{
			if (VerificationMethod == RegistrationVerificationMethod.None)
			{
				return;
			}
			base.IsEnumerating = true;
			ExpectedCode = null;
			ConfirmCode = null;
			await Task.Delay(new Random().Next(3000, 10000));
			if (VerificationMethod == RegistrationVerificationMethod.MobilePhone)
			{
				RemoveError("ConfirmMobilePhone");
				RaiseErrorsChanged("ConfirmMobilePhone");
				string mobilePhoneNumber = base.SelectedItem.CountryCallingCode + ConfirmMobilePhone;
				AccountCreationByMobilePhoneResult accountCreationByMobilePhoneResult = await Client.AccountCreationByMobilePhoneStartAsync(mobilePhoneNumber);
				if (accountCreationByMobilePhoneResult.Result == VerificationStartResultCode.Success)
				{
					ExpectedCode = accountCreationByMobilePhoneResult.ConfirmationCode;
					Token = accountCreationByMobilePhoneResult.Token;
				}
				else
				{
					AddError("ConfirmMobilePhone", accountCreationByMobilePhoneResult.ResultString);
					RaiseErrorsChanged("ConfirmMobilePhone");
				}
			}
			else if (VerificationMethod == RegistrationVerificationMethod.Email)
			{
				RemoveError("ConfirmEmailAddress");
				RaiseErrorsChanged("ConfirmEmailAddress");
				string text = ConfirmEmailAddress;
				AccountCreationByEmailResult accountCreationByEmailResult = await Client.AccountCreationByEmailStartAsync(text);
				if (accountCreationByEmailResult.Result == VerificationStartResultCode.Success)
				{
					ExpectedCode = accountCreationByEmailResult.ConfirmationCode;
					Token = accountCreationByEmailResult.Token;
				}
				else
				{
					AddError("ConfirmEmailAddress", accountCreationByEmailResult.ResultString);
					RaiseErrorsChanged("ConfirmEmailAddress");
				}
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnSendConfirmationCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Registration\\RegistrerViewModel.cs", 584);
		}
		finally
		{
			ResetCommands();
			base.IsEnumerating = false;
		}
	}

	private bool OnCanConfirmCodeCommand(object param)
	{
		return HasConfirmCode;
	}

	private void OnConfirmCodeCommand(object param)
	{
		switch (VerificationMethod)
		{
		default:
			return;
		case RegistrationVerificationMethod.Email:
			EmailAddress = ConfirmEmailAddress;
			CanSetEmailAddress = false;
			break;
		case RegistrationVerificationMethod.MobilePhone:
		{
			string text = base.SelectedItem?.CountryCallingCode + ConfirmMobilePhone;
			MobilePhone = text;
			CanSetMobilePhone = false;
			break;
		}
		}
		CurrentStep = RegisterStep.Registration;
	}

	private bool OnCanRegisterCommand(object param)
	{
		return IsValid;
	}

	private async void OnRegisterCommand(object param)
	{
		_ = 2;
		try
		{
			Validate();
			if (!IsValid)
			{
				return;
			}
			UserMember userMember = new UserMember
			{
				Username = Username,
				FirstName = FirstName,
				LastName = LastName,
				Address = Address,
				PostCode = PostCode,
				Country = Country,
				City = City,
				MobilePhone = MobilePhone,
				Email = EmailAddress,
				Sex = Sex,
				BirthDate = BirthDate
			};
			string pwd = Password;
			byte[] newSalt = PasswordHasher.GetNewSalt();
			byte[] hashedPassword = PasswordHasher.GetHashedPassword(pwd, newSalt);
			userMember.UserCredential = new UserCredential
			{
				Password = hashedPassword,
				Salt = newSalt
			};
			int result;
			int? userId;
			if (VerificationMethod == RegistrationVerificationMethod.None)
			{
				AccountCreationCompleteResult accountCreationCompleteResult = await Client.AccountCreationCompleteAsync(userMember, null);
				result = (int)accountCreationCompleteResult.Result;
				userId = accountCreationCompleteResult.UserId;
			}
			else
			{
				AccountCreationByTokenCompleteResult accountCreationByTokenCompleteResult = await Client.AccountCreationByTokenCompleteAsync(Token, userMember);
				result = (int)accountCreationByTokenCompleteResult.Result;
				userId = accountCreationByTokenCompleteResult.CreatedUserId;
			}
			if (result == 0)
			{
				foreach (KeyValuePair<int, UserAgreementAcceptState> userAgreementState in userAgreementStates)
				{
					await Client.UserAgreementSetStateAsync(userAgreementState.Key, userId.Value, userAgreementState.Value);
				}
				CurrentStep = RegisterStep.Complete;
			}
			else
			{
				CurrentStep = RegisterStep.Failed;
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnRegisterCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Registration\\RegistrerViewModel.cs", 687);
			CurrentStep = RegisterStep.Failed;
		}
	}

	private bool OnCanFinishCommand(object param)
	{
		return true;
	}

	private void OnFinishCommand(object param)
	{
		Shell.HideCurrentOverlay(cancel: false);
	}

	public async void OnImportsSatisfied()
	{
		_ = 4;
		try
		{
			base.IsEnumerating = true;
			VerificationMethod = Client.Settings?.RegistrationVerificationMethod ?? RegistrationVerificationMethod.None;
			CanSetEmailAddress = VerificationMethod != RegistrationVerificationMethod.Email;
			CanSetMobilePhone = VerificationMethod != RegistrationVerificationMethod.MobilePhone;
			try
			{
				UserAgreements = (await Client.UserAgreementGetAsync(new UserAgreementsFilter
				{
					Limit = 1000000,
					IsEnabled = true
				})).Data.OrderBy((UserAgreement a) => a.DisplayOrder).ToList();
			}
			catch (Exception ex)
			{
				Client.TraceWrite(ex, "OnImportsSatisfied", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Registration\\RegistrerViewModel.cs", 731);
			}
			if (HasUserAgreement)
			{
				currentUserAgreementIndex = 0;
				CurrentUserAgreementText = UserAgreements[currentUserAgreementIndex].Agreement;
				CurrentUserAgreementIgnoreState = UserAgreements[currentUserAgreementIndex].IgnoreState;
				CurrentUserAgreementAccepted = false;
				CurrentStep = RegisterStep.AcceptAgreement;
			}
			else
			{
				CurrentStep = ((VerificationMethod != 0) ? RegisterStep.Confirmation : RegisterStep.Registration);
			}
			try
			{
				UserInfoTypes? userInfoTypes = await Client.UserGroupDefaultRequiredInfoGetAsync();
				if (userInfoTypes.HasValue)
				{
					RequiredPersonalInfo = userInfoTypes.Value;
				}
			}
			catch (OperationNotSupportedException)
			{
			}
			catch (Exception ex3)
			{
				Client.TraceWrite(ex3, "OnImportsSatisfied", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Registration\\RegistrerViewModel.cs", 765);
			}
			IEnumerable<CountryInfo> countries = null;
			try
			{
				HttpClient client = new HttpClient();
				try
				{
					string text = "https://restcountries.com/rest/v2/all";
					HttpResponseMessage val = await client.GetAsync(text);
					if (val.IsSuccessStatusCode)
					{
						countries = JsonConvert.DeserializeObject<IEnumerable<CountryInfo>>(await val.Content.ReadAsStringAsync());
					}
				}
				finally
				{
					((IDisposable)client)?.Dispose();
				}
			}
			catch (Exception ex4)
			{
				Client.TraceWrite(ex4, "OnImportsSatisfied", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Registration\\RegistrerViewModel.cs", 789);
			}
			try
			{
				if (countries == null || countries.Count() == 0)
				{
					Assembly executingAssembly = Assembly.GetExecutingAssembly();
					string name = "Client.Resources.Data.countries.json";
					using Stream stream = executingAssembly.GetManifestResourceStream(name);
					using StreamReader reader = new StreamReader(stream);
					using JsonTextReader reader2 = new JsonTextReader(reader);
					JsonSerializer jsonSerializer = new JsonSerializer();
					countries = jsonSerializer.Deserialize<IEnumerable<CountryInfo>>(reader2);
				}
				Items = (from c in countries
					orderby c.Name
					select new CountryCodeViewModel
					{
						CountryCallingCode = GetCallingCode(c.CallingCodes),
						CountryCode = c.Alpha2Code,
						CountryName = c.Name
					}).ToList();
			}
			catch (Exception ex5)
			{
				Client.TraceWrite(ex5, "OnImportsSatisfied", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Registration\\RegistrerViewModel.cs", 815);
			}
			string defaultCode = await CountryCodeTryGetAsync();
			if (defaultCode == null)
			{
				RegionInfo regionInfo = new RegionInfo(CultureInfo.CurrentCulture.Name);
				defaultCode = regionInfo.TwoLetterISORegionName;
			}
			base.SelectedItem = Items.Where((CountryCodeViewModel c) => c.CountryCode == defaultCode).FirstOrDefault();
		}
		catch (Exception ex6)
		{
			CurrentStep = RegisterStep.Failed;
			Client.TraceWrite(ex6, "OnImportsSatisfied", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Registration\\RegistrerViewModel.cs", 835);
		}
		finally
		{
			base.IsEnumerating = false;
		}
	}

	protected override async void AfterValidatePropery(string propertyName, object value)
	{
		base.AfterValidatePropery(propertyName, value);
		if ((propertyName == "RepeatPassword" || propertyName == "Password") && !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(RepeatPassword))
		{
			RemoveError("RepeatPassword");
			RaiseErrorsChanged("RepeatPassword");
			if (Password != RepeatPassword)
			{
				string localizedString = Client.GetLocalizedString("VE_REPEAT_PASSWORD_MISSMATCH");
				AddError("RepeatPassword", localizedString);
				RaiseErrorsChanged("RepeatPassword");
			}
		}
		if (propertyName == "EmailAddress")
		{
			if (IsEmailAvailable == true)
			{
				return;
			}
			if (!PropertyHasErrors(propertyName))
			{
				string value2 = EmailAddress;
				if (!string.IsNullOrWhiteSpace(value2) && await Client.UserEmailExistAsync(value2))
				{
					string localizedString2 = Client.GetLocalizedString("VE_EMAIL_ADDRESS_USED");
					AddError("EmailAddress", localizedString2);
					RaiseErrorsChanged("EmailAddress");
				}
			}
		}
		if (propertyName == "FirstName" && RequiredPersonalInfo.HasFlag(UserInfoTypes.FirstName) && string.IsNullOrWhiteSpace(FirstName))
		{
			AddError("FirstName", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("FirstName");
		}
		if (propertyName == "LastName" && RequiredPersonalInfo.HasFlag(UserInfoTypes.LastName) && string.IsNullOrWhiteSpace(LastName))
		{
			AddError("LastName", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("LastName");
		}
		if (propertyName == "Phone" && RequiredPersonalInfo.HasFlag(UserInfoTypes.Phone) && string.IsNullOrWhiteSpace(Phone))
		{
			AddError("Phone", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("Phone");
		}
		if (propertyName == "MobilePhone" && RequiredPersonalInfo.HasFlag(UserInfoTypes.Mobile) && string.IsNullOrWhiteSpace(MobilePhone))
		{
			AddError("MobilePhone", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("MobilePhone");
		}
		if (propertyName == "EmailAddress" && RequiredPersonalInfo.HasFlag(UserInfoTypes.Email) && string.IsNullOrWhiteSpace(EmailAddress))
		{
			AddError("EmailAddress", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("EmailAddress");
		}
		if (propertyName == "Country" && RequiredPersonalInfo.HasFlag(UserInfoTypes.Country) && string.IsNullOrWhiteSpace(Country))
		{
			AddError("Country", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("Country");
		}
		if (propertyName == "City" && RequiredPersonalInfo.HasFlag(UserInfoTypes.City) && string.IsNullOrWhiteSpace(City))
		{
			AddError("City", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("City");
		}
		if (propertyName == "Address" && RequiredPersonalInfo.HasFlag(UserInfoTypes.Address) && string.IsNullOrWhiteSpace(Address))
		{
			AddError("Address", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("Address");
		}
		if (propertyName == "PostCode" && RequiredPersonalInfo.HasFlag(UserInfoTypes.PostCode) && string.IsNullOrWhiteSpace(PostCode))
		{
			AddError("PostCode", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("PostCode");
		}
		if (propertyName == "BirthDate" && RequiredPersonalInfo.HasFlag(UserInfoTypes.BirthDate) && !BirthDate.HasValue)
		{
			AddError("BirthDate", Client.GetLocalizedString("VE_FIELD_REQUIRED"));
			RaiseErrorsChanged("BirthDate");
		}
		if (propertyName == "BirthDate" && !RequiredPersonalInfo.HasFlag(UserInfoTypes.BirthDate))
		{
			RemoveError("BirthDate");
			RaiseErrorsChanged("BirthDate");
		}
		if (propertyName == "Sex" && !RequiredPersonalInfo.HasFlag(UserInfoTypes.Sex))
		{
			RemoveError("Sex");
			RaiseErrorsChanged("Sex");
		}
	}

	protected override void OnPropertyChanged(object sender, PropertyChangedEventArgsEx args)
	{
		base.OnPropertyChanged(sender, args);
		if (args.PropertyName == "CurrentUserAgreementText" || args.PropertyName == "CurrentUserAgreementAccepted")
		{
			AcceptAgreementCommand.RaiseCanExecuteChanged();
		}
		if (args.PropertyName == "ConfirmCode")
		{
			if (string.IsNullOrWhiteSpace(ConfirmCode))
			{
				IsConfirmCodeValid = null;
				return;
			}
			string text = ExpectedCode;
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}
			IsConfirmCodeValid = string.Compare(confirmCode, text, StringComparison.OrdinalIgnoreCase) == 0;
		}
		if (args.PropertyName == "Username")
		{
			IsUsernameAvailable = false;
			usernameConfirmTimer = usernameConfirmTimer ?? new Timer(OnUsernameConfirmCallback);
			usernameConfirmTimer.Change(1000, -1);
		}
		bool flag = args.PropertyName == "ConfirmMobilePhone";
		bool flag2 = args.PropertyName == "ConfirmEmailAddress";
		if (flag2 || flag)
		{
			IsMobilePhoneAvailable = (flag ? new bool?(false) : null);
			IsEmailAvailable = (flag2 ? new bool?(false) : null);
			verifyAvailabilityTimer = verifyAvailabilityTimer ?? new Timer(OnVerifyAvailabilityCallBack);
			verifyAvailabilityTimer.Change(1000, -1);
		}
		else if (args.PropertyName == "MobilePhone")
		{
			IsMobilePhoneAvailable = false;
			mobileConfirmTimer = mobileConfirmTimer ?? new Timer(OnMobileConfirmCallback);
			mobileConfirmTimer.Change(1000, -1);
		}
	}

	protected override void OnResetCommands()
	{
		base.OnResetCommands();
		ConfirmCodeCommand?.RaiseCanExecuteChanged();
		SendConfirmationCommand?.RaiseCanExecuteChanged();
		RegisterCommand?.RaiseCanExecuteChanged();
	}

	private async void OnUsernameConfirmCallback(object state)
	{
		await usernameConfirmLock.WaitAsync();
		try
		{
			RemoveError("Username");
			RaiseErrorsChanged("Username");
			ValidateProperty(Username, "Username");
			string text = Username;
			if (!string.IsNullOrWhiteSpace(username))
			{
				if (username.IndexOf(" ") != -1)
				{
					AddError("Username", "Spaces not allowed");
				}
				if (await Client.UsernameExistAsync(text))
				{
					AddError("Username", "User already exists");
				}
				IsUsernameAvailable = true;
			}
			RaiseErrorsChanged("Username");
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnUsernameConfirmCallback", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Registration\\RegistrerViewModel.cs", 1067);
		}
		finally
		{
			usernameConfirmLock.Release();
		}
	}

	private async void OnMobileConfirmCallback(object state)
	{
		await mobileConfirmLock.WaitAsync();
		try
		{
			RemoveError("MobilePhone");
			RaiseErrorsChanged("MobilePhone");
			ValidateProperty(MobilePhone, "MobilePhone");
			if (!string.IsNullOrWhiteSpace(MobilePhone))
			{
				if (MobilePhone.IndexOf(" ") != -1)
				{
					AddError("MobilePhone", "Spaces not allowed");
				}
				if (await Client.MobilePhoneExistAsync(MobilePhone))
				{
					AddError("MobilePhone", "Mobile phone already exists");
				}
				IsMobilePhoneAvailable = true;
			}
			RaiseErrorsChanged("MobilePhone");
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnMobileConfirmCallback", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Registration\\RegistrerViewModel.cs", 1109);
		}
		finally
		{
			mobileConfirmLock.Release();
		}
	}

	private async void OnVerifyAvailabilityCallBack(object state)
	{
		if (VerificationMethod == RegistrationVerificationMethod.None)
		{
			return;
		}
		try
		{
			string text = null;
			string fieldName = null;
			switch (VerificationMethod)
			{
			case RegistrationVerificationMethod.Email:
				text = ConfirmEmailAddress;
				fieldName = "ConfirmEmailAddress";
				break;
			case RegistrationVerificationMethod.MobilePhone:
				text = ConfirmMobilePhone;
				fieldName = "ConfirmMobilePhone";
				break;
			}
			if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(fieldName))
			{
				return;
			}
			if (VerificationMethod == RegistrationVerificationMethod.MobilePhone)
			{
				if ((await Client.MobilePhoneVerificationStateInfoGetAsync(text)).IsVerified)
				{
					string localizedString = Client.GetLocalizedString("VE_MOBILE_PHONE_USED");
					AddError(fieldName, localizedString);
					RaiseErrorsChanged(fieldName);
				}
				else
				{
					IsMobilePhoneAvailable = true;
				}
			}
			else if (VerificationMethod == RegistrationVerificationMethod.Email)
			{
				if ((await Client.EmailVerificationStateInfoGetAsync(text)).IsAssigned)
				{
					string localizedString2 = Client.GetLocalizedString("VE_EMAIL_ADDRESS_USED");
					AddError(fieldName, localizedString2);
					RaiseErrorsChanged(fieldName);
				}
				else
				{
					IsEmailAvailable = true;
				}
			}
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnVerifyAvailabilityCallBack", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\Registration\\RegistrerViewModel.cs", 1188);
		}
		finally
		{
			ResetCommands();
		}
	}

	public async Task<string> CountryCodeTryGetAsync()
	{
		_ = 1;
		try
		{
			HttpClient client = new HttpClient();
			try
			{
				client.BaseAddress = new Uri("http://www.geoplugin.net");
				HttpResponseMessage val = await client.GetAsync("json.gp").ConfigureAwait(continueOnCapturedContext: false);
				if (val.IsSuccessStatusCode)
				{
					GeoPluginResponse geoPluginResponse = JsonConvert.DeserializeObject<GeoPluginResponse>(await val.Content.ReadAsStringAsync());
					if (geoPluginResponse.Status == 200 || geoPluginResponse.Status == 206)
					{
						return geoPluginResponse.CountryCode;
					}
				}
			}
			finally
			{
				((IDisposable)client)?.Dispose();
			}
		}
		catch
		{
		}
		return null;
	}

	private string GetCallingCode(IEnumerable<string> callingCodes)
	{
		string empty = string.Empty;
		if (callingCodes.Count() < 2)
		{
			return callingCodes.DefaultIfEmpty("00").FirstOrDefault();
		}
		int min = callingCodes.Min((string a) => a.Length);
		string text = callingCodes.Where((string a) => a.Length == min).FirstOrDefault();
		string result = text;
		bool flag = false;
		for (int i = 0; i < text.Length; i++)
		{
			foreach (string callingCode in callingCodes)
			{
				if (callingCode[i] != text[i])
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				result = text.Substring(0, i);
				break;
			}
		}
		return result;
	}
}
