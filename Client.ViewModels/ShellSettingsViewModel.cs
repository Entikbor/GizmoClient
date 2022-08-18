using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using SharedLib;
using SharedLib.ViewModels;

namespace Client.ViewModels;

[Export]
public class ShellSettingsViewModel : ViewModel, IPartImportsSatisfiedNotification
{
	private NotifyTaskCompletion<IEnumerable<string>> languageTask;

	private IEnumerable<CultureInfo> installedCultures;

	private CultureInfo selectedInputCulture;

	[Import]
	public GizmoClient Client { get; protected set; }

	public NotifyTaskCompletion<IEnumerable<string>> Languages
	{
		get
		{
			if (languageTask == null)
			{
				languageTask = new NotifyTaskCompletion<IEnumerable<string>>(GetLanguagesAsync());
			}
			return languageTask;
		}
	}

	[IgnorePropertyModification]
	public IEnumerable InstalledCultures
	{
		get
		{
			if (installedCultures == null)
			{
				installedCultures = InputLanguageManager.Current.AvailableInputLanguages.OfType<CultureInfo>();
			}
			return installedCultures;
		}
	}

	[IgnorePropertyModification]
	public CultureInfo SelectedInputCulture
	{
		get
		{
			if (selectedInputCulture == null)
			{
				selectedInputCulture = InputLanguageManager.Current.CurrentInputLanguage;
			}
			return selectedInputCulture;
		}
		set
		{
			SetProperty(ref selectedInputCulture, value, "SelectedInputCulture");
			if (value != null)
			{
				InputLanguageManager.Current.CurrentInputLanguage = value;
			}
		}
	}

	[IgnorePropertyModification]
	private CultureInfo SelectedInputCultureInternal
	{
		get
		{
			return SelectedInputCulture;
		}
		set
		{
			selectedInputCulture = value;
			RaisePropertyChanged("SelectedInputCulture");
		}
	}

	private async Task<IEnumerable<string>> GetLanguagesAsync()
	{
		await Task.Yield();
		string languagePath = Client.Settings.LanguagePath;
		if (Directory.Exists(languagePath))
		{
			return from fileName in Directory.GetFiles(languagePath, "*.resx")
				select Path.GetFileNameWithoutExtension(fileName);
		}
		return Enumerable.Empty<string>();
	}

	private void OnInputLanguageChanged(object sender, InputLanguageEventArgs e)
	{
		if (SelectedInputCulture?.LCID != e.NewLanguage?.LCID)
		{
			SelectedInputCultureInternal = e.NewLanguage;
		}
	}

	public void OnImportsSatisfied()
	{
		InputLanguageManager.Current.InputLanguageChanged += OnInputLanguageChanged;
	}
}
