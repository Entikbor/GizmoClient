using System.Runtime.Serialization;

namespace Client.ViewModels;

[DataContract]
public class Translations
{
	[DataMember(Name = "de")]
	public string De { get; set; }

	[DataMember(Name = "es")]
	public string Es { get; set; }

	[DataMember(Name = "fr")]
	public string Fr { get; set; }

	[DataMember(Name = "ja")]
	public string Ja { get; set; }

	[DataMember(Name = "it")]
	public string It { get; set; }

	[DataMember(Name = "br")]
	public string Br { get; set; }

	[DataMember(Name = "pt")]
	public string Pt { get; set; }
}
