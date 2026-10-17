using System;
using System.Collections.Generic;
using System.Text;
using Atria.Core.Data;

namespace Atria.Core.Search
{
    public struct RoomMatch
    {
        public RoomData Room;
        public int Score;
    }

    /// <summary>
    /// Offline room lookup by number, identifier, alias or name. Matching ignores case, spaces,
    /// hyphens, dots and underscores, so "prp 101", "PRP-101" and "prp101" are equivalent.
    /// </summary>
    public sealed class RoomSearch
    {
        private readonly List<(RoomData room, string[] keys, string name)> _index = new List<(RoomData, string[], string)>();

        public RoomSearch(IEnumerable<RoomData> rooms)
        {
            foreach (RoomData r in rooms)
            {
                var keys = new List<string> { Normalize(r.Number), Normalize(r.Id) };
                foreach (string a in r.Aliases) keys.Add(Normalize(a));
                _index.Add((r, keys.ToArray(), (r.Name ?? string.Empty).ToUpperInvariant()));
            }
        }

        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '.') continue;
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>Best matches first. An empty query returns every room in number order.</summary>
        public List<RoomMatch> Search(string query, int maxResults = 20)
        {
            var results = new List<RoomMatch>();
            string q = Normalize(query);
            string rawUpper = (query ?? string.Empty).Trim().ToUpperInvariant();

            foreach (var (room, keys, name) in _index)
            {
                int score = 0;
                if (q.Length == 0) score = 1;
                else
                {
                    foreach (string k in keys)
                    {
                        if (k.Length == 0) continue;
                        if (k == q) score = Math.Max(score, 100);
                        else if (k.StartsWith(q, StringComparison.Ordinal)) score = Math.Max(score, 60);
                        else if (k.IndexOf(q, StringComparison.Ordinal) >= 0) score = Math.Max(score, 30);
                    }
                    if (rawUpper.Length >= 2 && name.IndexOf(rawUpper, StringComparison.Ordinal) >= 0) score = Math.Max(score, 20);
                }
                if (score > 0) results.Add(new RoomMatch { Room = room, Score = score });
            }

            results.Sort((a, b) =>
            {
                int c = b.Score.CompareTo(a.Score);
                return c != 0 ? c : string.Compare(a.Room.Number, b.Room.Number, StringComparison.OrdinalIgnoreCase);
            });
            if (results.Count > maxResults) results.RemoveRange(maxResults, results.Count - maxResults);
            return results;
        }

        /// <summary>Exact match on number, id or alias; null when the room is unknown.</summary>
        public RoomData FindExact(string query)
        {
            string q = Normalize(query);
            if (q.Length == 0) return null;
            foreach (var (room, keys, _) in _index)
                foreach (string k in keys)
                    if (k == q) return room;
            return null;
        }
    }
}
