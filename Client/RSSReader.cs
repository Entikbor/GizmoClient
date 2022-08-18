using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Client;

public class RSSReader : XmlTextReader
{
	private bool readingDate;

	private const string CustomUtcDateTimeFormat = "ddd MMM dd HH:mm:ss Z yyyy";

	public RSSReader(Stream stream)
		: base(stream)
	{
	}

	public override void ReadStartElement()
	{
		if (string.Equals(base.NamespaceURI, string.Empty, StringComparison.InvariantCultureIgnoreCase) && (string.Equals(base.LocalName, "lastBuildDate", StringComparison.InvariantCultureIgnoreCase) || string.Equals(base.LocalName, "pubDate", StringComparison.InvariantCultureIgnoreCase)))
		{
			readingDate = true;
		}
		base.ReadStartElement();
	}

	public override void ReadEndElement()
	{
		if (readingDate)
		{
			readingDate = false;
		}
		string text = LocalName.ToLower();
		base.ReadEndElement();
		if (text == "channel")
		{
			while (base.IsStartElement())
			{
				Skip();
			}
		}
	}

	public override string ReadString()
	{
		if (readingDate)
		{
			string text = base.ReadString();
			try
			{
				if (!DateTime.TryParse(text, out var result))
				{
					result = DateTime.ParseExact(text, "ddd MMM dd HH:mm:ss Z yyyy", CultureInfo.InvariantCulture);
				}
				return result.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture);
			}
			catch (FormatException)
			{
				return text;
			}
		}
		return base.ReadString();
	}
}
