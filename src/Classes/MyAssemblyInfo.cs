using System;
using System.Collections.Generic;
using System.Text;

namespace Bills.Common.Classes
{

	/// <summary>
	/// Add THIS to your project file to make this all automatic
	/// 
	/// <ItemGroup>
	///     <AssemblyAttribute Include="Bills.Common.Classes.BuildDateTime">
	///		    <_Parameter1>$([System.DateTime]::Now.ToString("s"))</_Parameter1>
	///	    </AssemblyAttribute>
	/// </ItemGroup>
	/// 
	/// Then access the build date by:  
	/// 
	/// Bills.Common.Classes.AssemblyInfo.BuildDateTime
	/// 
	/// watch for null, returns DateTime? 
	/// </summary>


	[AttributeUsage(AttributeTargets.Assembly)]
	public class BuildDateTimeAttribute : Attribute
	{
		public string Date { get; set; }
		public BuildDateTimeAttribute(string date)
		{
			Date = date;
		}
	}

	public static class MyAssemblyInfo
	{
		public static DateTime? BuildDateTime
		{
			get
			{
				var assembly = System.Reflection.Assembly.GetExecutingAssembly();
				var attr = Attribute.GetCustomAttribute(assembly, typeof(BuildDateTimeAttribute)) as BuildDateTimeAttribute;
				if (DateTime.TryParse(attr?.Date, out DateTime dt))
					return dt;
				else
					return null;
			}
		}
	}
}
