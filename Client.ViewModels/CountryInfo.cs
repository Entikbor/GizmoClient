using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Client.ViewModels;

[DataContract]
public class CountryInfo
{
	[DataMember(Name = "name")]
	public string Name { get; set; }

	[DataMember(Name = "topLevelDomain")]
	public IList<string> TopLevelDomain { get; set; }

	[DataMember(Name = "alpha2Code")]
	public string Alpha2Code { get; set; }

	[DataMember(Name = "alpha3Code")]
	public string Alpha3Code { get; set; }

	[DataMember(Name = "callingCodes")]
	public IList<string> CallingCodes { get; set; }

	[DataMember(Name = "capital")]
	public string Capital { get; set; }

	[DataMember(Name = "altSpellings")]
	public IList<string> AltSpellings { get; set; }

	[DataMember(Name = "region")]
	public string Region { get; set; }

	[DataMember(Name = "subregion")]
	public string Subregion { get; set; }

	[DataMember(Name = "population")]
	public int Population { get; set; }

	[DataMember(Name = "latlng")]
	public IList<double> Latlng { get; set; }

	[DataMember(Name = "demonym")]
	public string Demonym { get; set; }

	[DataMember(Name = "area")]
	public double? Area { get; set; }

	[DataMember(Name = "gini")]
	public double? Gini { get; set; }

	[DataMember(Name = "timezones")]
	public IList<string> Timezones { get; set; }

	[DataMember(Name = "borders")]
	public IList<string> Borders { get; set; }

	[DataMember(Name = "nativeName")]
	public string NativeName { get; set; }

	[DataMember(Name = "numericCode")]
	public string NumericCode { get; set; }

	[DataMember(Name = "currencies")]
	public IList<Currency> Currencies { get; set; }

	[DataMember(Name = "languages")]
	public IList<Language> Languages { get; set; }

	[DataMember(Name = "translations")]
	public Translations Translations { get; set; }

	[DataMember(Name = "flag")]
	public string Flag { get; set; }

	[DataMember(Name = "regionalBlocs")]
	public IList<RegionalBloc> RegionalBlocs { get; set; }

	[DataMember(Name = "cioc")]
	public string Cioc { get; set; }
}
