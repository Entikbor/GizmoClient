using System;
using System.Net.Http;
using System.Threading.Tasks;
using SharedLib;

namespace Client.ViewModels;

public class CountryCodeViewModel : PropertyChangedBase
{
	private NotifyTaskCompletion<byte[]> countryImageTask;

	private readonly string API_URL = "https://www.countryflags.io";

	private readonly int PIXEL_SIZE = 32;

	[IgnorePropertyModification]
	public string CountryName { get; set; }

	[IgnorePropertyModification]
	public string CountryCode { get; set; }

	[IgnorePropertyModification]
	public string CountryCallingCode { get; set; }

	[IgnorePropertyModification]
	public NotifyTaskCompletion<byte[]> CountryImageTask
	{
		get
		{
			if (countryImageTask == null)
			{
				countryImageTask = new NotifyTaskCompletion<byte[]>(GetCountryImageAsync());
			}
			return countryImageTask;
		}
		set
		{
			SetProperty(ref countryImageTask, value, "CountryImageTask");
		}
	}

	private async Task<byte[]> GetCountryImageAsync()
	{
		if (string.IsNullOrWhiteSpace(CountryCode))
		{
			return null;
		}
		HttpClient httpClient = new HttpClient();
		try
		{
			string text = $"{API_URL}/{CountryCode}/flat/{PIXEL_SIZE}.png";
			HttpResponseMessage obj = await httpClient.GetAsync(text);
			obj.EnsureSuccessStatusCode();
			return await obj.Content.ReadAsByteArrayAsync();
		}
		finally
		{
			((IDisposable)httpClient)?.Dispose();
		}
	}
}
