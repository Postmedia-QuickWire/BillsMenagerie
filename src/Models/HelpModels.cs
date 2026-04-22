
namespace Common.Models
{

    public class HelpSearchModel
    {
        public string Words { get; set; }
        public bool HighlightWords { get; set; }
    }

    public class HelpIndexModel
    {
        public HelpSearchModel SearchModel { get; set; }

        public HelpFile HelpFile { get; set; }
    }

    public class HelpSearchType
	{
		public string Words { get; set; }
	}

	public class HelpSearchHits
	{
		public List<HelpSearchHit> Hits { get; set; } = new List<HelpSearchHit>();
        public string err { get; set; }
	}

	public class HelpSearchHit
	{
		public string Name { get; set; }
		public string Desc { get; set; }
        public string TopicKey { get; set; }
        public string TopicParentKey { get; set; }
        public float Rank { get; set; }
		public List<string> Frags { get; set; } = new List<string>();
	}

	public class HelpIndexItem
	{
		public string Key { get; set; }
        public string ParentKey { get; set; }
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
