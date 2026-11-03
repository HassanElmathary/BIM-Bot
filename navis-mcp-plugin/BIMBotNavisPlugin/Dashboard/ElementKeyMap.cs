using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace BIMBotNavisPlugin.Dashboard
{
    /// <summary>
    /// Stable integer keys for model elements. The BIM-Bot 3D Viewer binds on a whole number,
    /// and Revit Element IDs repeat across the appended models, so every element gets a project
    /// key that never changes between exports (filters, bookmarks and manual Power BI edits keep
    /// pointing at the same element). Identity = "&lt;model file&gt;|r:&lt;Revit id&gt;", or "|g:&lt;guid&gt;",
    /// or "|p:&lt;tree path&gt;" when an element has neither. New elements get the next free key;
    /// keys of deleted elements are never reused.
    /// </summary>
    public sealed class ElementKeyMap
    {
        private class MapFile
        {
            public int NextKey { get; set; } = 1;
            public Dictionary<string, int> Keys { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private readonly string _path;
        private readonly MapFile _map;
        private bool _dirty;

        public ElementKeyMap(string path)
        {
            _path = path;
            _map = File.Exists(path)
                ? JsonConvert.DeserializeObject<MapFile>(File.ReadAllText(path)) ?? new MapFile()
                : new MapFile();
        }

        public int Count => _map.Keys.Count;

        public static string Identity(string modelFile, string revitId, Guid instanceGuid, string treePath)
        {
            var m = (modelFile ?? "").ToLowerInvariant();
            if (!string.IsNullOrEmpty(revitId)) return m + "|r:" + revitId;
            if (instanceGuid != Guid.Empty) return m + "|g:" + instanceGuid.ToString("N");
            return m + "|p:" + treePath;
        }

        public int KeyFor(string identity)
        {
            if (_map.Keys.TryGetValue(identity, out var k)) return k;
            k = _map.NextKey++;
            _map.Keys[identity] = k;
            _dirty = true;
            return k;
        }

        public void SaveIfChanged()
        {
            if (!_dirty) return;
            AtomicFile.WriteAllText(_path, JsonConvert.SerializeObject(_map));
            _dirty = false;
        }
    }
}
