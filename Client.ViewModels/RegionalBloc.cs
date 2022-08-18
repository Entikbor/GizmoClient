using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Client.ViewModels;

[DataContract]
public class RegionalBloc
{
	[DataMember(Name = "acronym")]
	public string Acronym { get; set; }

	[DataMember(Name = "name")]
	public string Name { get; set; }

	[DataMember(Name = "otherAcronyms")]
	public IList<string> OtherAcronyms { get; set; }

	[DataMember(Name = "otherNames")]
	public IList<string> OtherNames { get; set; }
}
