
namespace Common.Models
{

	public class HelpSearch
	{
		public string Words { get; set; }
	}

	public class HelpSearchHits
	{
		public List<HelpSearchHit> Hits { get; set; } = new List<HelpSearchHit>();
	}
	public class HelpSearchHit
	{
		public string Name { get; set; }
		public string Desc { get; set; }
		public string TopicKey { get; set; }
		public float Rank { get; set; }
		public List<string> Frags { get; set; } = new List<string>();
	}

	public class HelpIndexItem
	{
		public string Key { get; set; }
		public string Name { get; set; }
		public string Desc { get; set; }
		public List <HelpIndexItem> Items { get; set; }
	}

	public class HelpFile
	{
		public string Html { get; set; }
		public string Md { get; set; }
		public string Filename { get; set; }
		public string Title { get; set; }
	}

}
