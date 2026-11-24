using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TheLongingDecrypted.Cosmetics
{
    /// <summary>Where a hat sits on one animation frame, in the frame's own local units.</summary>
    internal struct HeadAnchor
    {
        public Vector2 Position;
        public float Angle;
        public float Scale;
        public bool Mirrored;
        public bool FromBehind;
        public float Alpha;
    }

    /// <summary>
    /// Head pose of every Shade animation frame (Data/head_anchors.csv, built by tools/build_head_anchors.py).
    /// Only the standing and walking frames use a separate head sprite. Everything else has the head drawn into the
    /// body, so a hat has to be placed per frame rather than attached to the head object.
    /// </summary>
    internal static class HeadAnchors
    {
        private static Dictionary<string, HeadAnchor> _byName;
        private static readonly Dictionary<int, HeadAnchor?> ById = new Dictionary<int, HeadAnchor?>();

        public static bool TryGet(Sprite sprite, out HeadAnchor anchor)
        {
            anchor = default;
            if (sprite == null)
            {
                return false;
            }
            int id = sprite.GetInstanceID();
            if (!ById.TryGetValue(id, out var cached))
            {
                if (_byName == null)
                {
                    Load();
                }
                cached = _byName.TryGetValue(sprite.name, out var found) ? found : (HeadAnchor?)null;
                ById[id] = cached;
            }
            anchor = cached.GetValueOrDefault();
            return cached.HasValue;
        }

        private static void Load()
        {
            _byName = new Dictionary<string, HeadAnchor>(4600);
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var resource in assembly.GetManifestResourceNames())
            {
                if (!resource.EndsWith("head_anchors.csv", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                using (var reader = new StreamReader(assembly.GetManifestResourceStream(resource)))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var cells = line.Split(',');
                        if (cells.Length != 7)
                        {
                            continue;
                        }
                        int flags = int.Parse(cells[5], CultureInfo.InvariantCulture);
                        _byName[cells[0]] = new HeadAnchor
                        {
                            Position = new Vector2(Parse(cells[1]), Parse(cells[2])),
                            Angle = Parse(cells[3]),
                            Scale = Parse(cells[4]),
                            Mirrored = (flags & 1) != 0,
                            FromBehind = (flags & 2) != 0,
                            Alpha = Parse(cells[6]) / 255f,
                        };
                    }
                }
            }
            Plugin.Log.LogInfo($"Loaded {_byName.Count} head poses.");
        }

        private static float Parse(string cell) => float.Parse(cell, CultureInfo.InvariantCulture);
    }
}
