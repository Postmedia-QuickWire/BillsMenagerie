using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebSite.Models
{

	public class DataTablesAjaxSearch
	{
		public string value { get; set; }
		public bool regex { get; set; }
	}

	public class DataTablesAjaxOrder
	{
		public int column { get; set; }
		public string dir { get; set; }
	}

	public class DataTablesAjaxColumn
	{
		public int data { get; set; }
		public string name { get; set; }
		public bool searchable { get; set; }
		public bool orderable { get; set; }
		public DataTablesAjaxSearch search { get; set; }
	}

	public class DataTablesAjaxModel
	{
		/// <summary>
		/// Draw counter. 
		/// </summary>       
		public int draw { get; set; }

		/// <summary>
		/// Paging first record indicator. This is the start point in the current data set  
		/// </summary>       
		public int start { get; set; }

		/// <summary>
		/// Number of records that the table can display in the current draw.  
		/// </summary>       
		public int length { get; set; }

		public List<DataTablesAjaxColumn> columns { get; set; }
		public List<DataTablesAjaxOrder> order { get; set; }

		public DataTablesAjaxSearch search { get; set; }
	}
}