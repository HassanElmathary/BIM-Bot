using Autodesk.Revit.DB;
namespace BIMBotPlugin.Core
{
    public class FederatedElement
    {
        public Element Element { get; set; }
        public string SourceModel { get; set; }       // "Host" or link filename
        public string NamespacedId { get; set; }      // "Host:12345" or "LinkName:12345"
        public Transform WorldTransform { get; set; } // Identity for host, link transform for linked
        public bool IsFromLink { get; set; }
        public Document SourceDocument { get; set; }
    }
}
