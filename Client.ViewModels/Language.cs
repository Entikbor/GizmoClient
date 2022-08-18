using System.Runtime.Serialization;

namespace Client.ViewModels;

[DataContract]
public class Language
{
	[DataMember(Name = "iso639_1")]
	public string Iso6391 { get; set; }

	[DataMember(Name = "iso639_2")]
	public string Iso6392 { get; set; }

	[DataMember(Name = "name")]
	public string Name { get; set; }

	[DataMember(Name = "nativeName")]
	public string NativeName { get; set; }
}
