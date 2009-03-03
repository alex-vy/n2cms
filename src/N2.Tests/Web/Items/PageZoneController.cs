using N2.Web;
using N2.Collections;

namespace N2.Tests.Web.Items
{
	[Controls(typeof(PageItem))]
	public class PageZoneController : ZoneAspectController
	{
		public override ItemList GetItemsInZone(ContentItem parentItem, string zoneName)
		{
			if(zoneName.EndsWith("None"))
				return new ItemList();
			if (zoneName.EndsWith("All"))
				return parentItem.GetChildren(new DelegateFilter(ci => ci.ZoneName != null));

			return parentItem.GetChildren(zoneName);
		}
	}
}