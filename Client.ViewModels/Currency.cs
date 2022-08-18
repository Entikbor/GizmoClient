using System.Runtime.Serialization;

namespace Client.ViewModels;

[DataContract]
public class Currency
{
	[DataMember(Name = "code")]
	public string Code { get; set; }

	[DataMember(Name = "name")]
	public string Name { get; set; }

	[DataMember(Name = "symbol")]
	public string Symbol { get; set; }
}
