using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Markdig;
using Markdown.ColorCode;
using System.Text;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.Util;
using Lucene.Net.Store;
using Lucene.Net.Documents;
using Lucene.Net.Search;
using Lucene.Net.QueryParsers.Flexible.Standard;
using Lucene.Net.Analysis;
using Lucene.Net.Search.Highlight;

using StreetPerfect.Classes;
using StreetPerfect.Controllers;
using StreetPerfect.Models;
using Common.Classes;
using WebSite.Controllers;
using Common.Models;


namespace Common.Controllers
{

	[Authorize]
	[ApiExplorerSettings(IgnoreApi = true)]
	public class HelpController : BaseController
	{
		private const LuceneVersion lucene_ver = LuceneVersion.LUCENE_48;
		private readonly IWebHostEnvironment _environment;
		private readonly string HelpFolder;
		private static List<HelpIndexItem> _helpIndex;
		private static IndexWriter _indexWriter = null;
		private static IndexSearcher _Searcher = null;
		private JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
		{
			AllowTrailingCommas = true,
			PropertyNameCaseInsensitive = true
		};

		private readonly HelpFile _mdNotFound = new HelpFile()
		{
			Html = "<p>sorry, there was an error accessing that help topic</p>",
			Title = "error",
		};
		public HelpController(BaseServices bs
			, IWebHostEnvironment environment
			, ILogger<BatchRunController> logger)
			: base(bs, logger)
		{
			_environment = environment;
			HelpFolder = _settings?.HelpFolder ?? "Help";
		}


		public async Task<IActionResult> Index()
		{
			try
			{
				await InitViewBag();
				var model = await LoadHelpFileModel("index");

				var sb = new StringBuilder();
				sb.AppendLine("<div class='help-index'>");
				foreach (HelpIndexItem item in ViewBag.HelpIndex)
				{
					sb.AppendFormat("<p><a href='/Help/topic/{0}'>{1}</a><br/>{2}</p>", item.Key, item.Name, item.Desc);
				}
				sb.AppendLine("</div>");
				model.Html += sb.ToString();
				return View(model);
			}
			catch (Exception ex)
			{
				_logger.LogError("Help Index error {m}", ex.Message);
				return View(_mdNotFound);
			}

		}


		public async Task<IActionResult> Topic(string id, string sid=null)
		{
			try
			{
				ViewBag.HelpSubIndex = id;
				if (!String.IsNullOrEmpty(sid))
				{
					ViewBag.HelpSubSubIndex = sid;
					id = sid;
				}
				return await LoadHelpView(id);
			}
			catch (Exception ex)
			{
				_logger.LogError("Help Topic error {m}, accessing topic: {t}", ex.Message, id);
				return View(_mdNotFound);
			}
		}

		public IActionResult Images(string id)
		{
			try
			{
				string path = Path.Combine(HelpFolder, "Images", id);
				if (id.Contains("/") || id.Contains("\\"))
				{
					return NotFound();
				}
				var ext = "jpg";
				var ind = id.LastIndexOf(".");
				if (ind > -1)
				{
					ext = id.Substring(ind + 1);
				}

				var stream = System.IO.File.OpenRead(path);
				return new FileStreamResult(stream, $"image/{ext}");
			}
			catch (Exception ex)
			{
				_logger.LogError("Help Images error {m}, accessing image: {t}", ex.Message, id);
				return NotFound();
			}
		}

		[HttpGet]
		public async Task<IActionResult> Search(string q)
		{
			await CommonViewBag();

			var found = new HelpSearchHits();
			if (!String.IsNullOrEmpty(q))
			{
				var searcher = await GetSearcher();
				var analyzer = new StandardAnalyzer(lucene_ver);
				StandardQueryParser queryParserHelper = new StandardQueryParser(analyzer);
				Query query = queryParserHelper.Parse(q, "text");

				var hits = searcher.Search(query, 20).ScoreDocs;

				SimpleHTMLFormatter htmlFormatter = new SimpleHTMLFormatter("<mark>", "</mark>");
				Highlighter highlighter = new Highlighter(htmlFormatter, new QueryScorer(query));

				foreach (var hit in hits)
				{
					var doc = searcher.Doc(hit.Doc);

					string text = doc.Get("text");
					TokenStream tokenStream = TokenSources.GetAnyTokenStream(_indexWriter.GetReader(false), hit.Doc, "text", analyzer);
					TextFragment[] frags = highlighter.GetBestTextFragments(tokenStream, text, false, 4);

					var help_hit = new HelpSearchHit()
					{
						Name = doc.Get("name"),
						Desc = doc.Get("desc"),
						TopicKey = doc.Get("key"),
						Rank = hit.Score,
					};

					foreach (var frag in frags)
					{
						if (frag?.Score > 0)
						{
							help_hit.Frags.Add(frag.ToString());
						}
					}

					found.Hits.Add(help_hit);
				}
			}
			return  View(found);
		}


		// used for ajax post while typing in search ctrl
		[HttpPost]
		public async Task<List<HelpSearchHit>> TypeSearch([FromBody] HelpSearch model)
		{
			var found = new List<HelpSearchHit>();
			if (ModelState.IsValid)
			{
				if (!String.IsNullOrEmpty(model?.Words))
				{
					var searcher = await GetSearcher();
					var analyzer = new StandardAnalyzer(lucene_ver);
					StandardQueryParser queryParserHelper = new StandardQueryParser(analyzer);
					Query query = queryParserHelper.Parse(model.Words, "text");

					var hits = searcher.Search(query, 20).ScoreDocs;

					foreach (var hit in hits)
					{
						var doc = searcher.Doc(hit.Doc);

						found.Add(new HelpSearchHit()
						{
							Name = doc.Get("name"),
							TopicKey = doc.Get("key"),
							Rank = hit.Score,
						});
					}
				}
			}
			return found;
		}

		private async Task<List<HelpIndexItem>> LoadHelpIndex()
		{
			string path = "";
			try
			{
				if (_helpIndex == null)
				{
					path = Path.Combine(HelpFolder, "index.json");
					using FileStream openStream = System.IO.File.OpenRead(path);
					_helpIndex = await JsonSerializer.DeserializeAsync<List<HelpIndexItem>>(openStream, _jsonOptions);
				}
				return _helpIndex;
			}
			catch (Exception ex)
			{
				_logger.LogError("Unable to load help index, {e}, {path}", ex, path);
				throw;
			}
		}

		private async Task<IActionResult> LoadHelpView(string md_title)
		{
			await InitViewBag();
			return View("index", await LoadHelpFileModel(md_title));
		}

		private async Task InitViewBag()
		{
			await CommonViewBag();

			ViewBag.TopMenu = "menu_support";
			ViewBag.SidebarMenu = "menu_help";
			ViewBag.HelpIndex = await LoadHelpIndex();
		}

		private async Task<HelpFile> LoadHelpFileModel(string md_title)
		{
			string filename = md_title;
			try
			{
				filename = Path.Combine(HelpFolder, $"{md_title}.md");
				var raw_md = await System.IO.File.ReadAllTextAsync(filename, System.Text.Encoding.UTF8);

				var pipeline = new MarkdownPipelineBuilder()
					.UseAdvancedExtensions()
					.UseBootstrap()
					.UseColorCode(HtmlFormatterType.Style
						, isDarkTheme ? ColorCode.Styling.StyleDictionary.DefaultDark : ColorCode.Styling.StyleDictionary.DefaultLight)
					.Build();
				var result = Markdig.Markdown.ToHtml(raw_md, pipeline);

				return new HelpFile()
				{
					Html = result,
					Md = raw_md,
					Title = md_title,
					Filename = filename
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error accessing/processing help md file '{md}', {e}", filename, ex);
				// rethrow with user exception
			}
			return new HelpFile()
			{
				Html = "<p>Error accessing this help topic</p>",
				Title = md_title,
				Filename = filename
			}; ;
		}

		private async Task<IndexSearcher> GetSearcher()
		{
			if (_Searcher == null)
			{
				await LoadHelpSearchIndex();
			}
			return _Searcher;
		}

		private async Task LoadHelpSearchIndex()
		{
			if (_indexWriter == null)
			{
				var index = await LoadHelpIndex();

				var analyzer = new StandardAnalyzer(lucene_ver);

				// Create an index writer
				var indexConfig = new IndexWriterConfig(lucene_ver, analyzer);
				_indexWriter = new IndexWriter(new RAMDirectory(), indexConfig);

				foreach (var item in index)
				{
					var filename = Path.Combine(HelpFolder, $"{item.Key}.md");
					var raw_md = await System.IO.File.ReadAllTextAsync(filename, System.Text.Encoding.UTF8);

					raw_md = raw_md.Replace("*", "").Replace("#", "").Replace(" \\", "");

					var doc = new Document
					{
						// StringField indexes but doesn't tokenize
						new StringField("name",item.Name, Field.Store.YES),
						new StringField("desc",item.Desc, Field.Store.YES),
						new StringField("key",item.Key, Field.Store.YES),
						new TextField("text", raw_md, Field.Store.YES)
					};
					_indexWriter.AddDocument(doc);
				}
				_indexWriter.Flush(triggerMerge: false, applyAllDeletes: false);

				// not sure we need to keep an _indexWriter instance after calling GetReader
				_Searcher = new IndexSearcher(_indexWriter.GetReader(false));
			}
		}
	}
}


