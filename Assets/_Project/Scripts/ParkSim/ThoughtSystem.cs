using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>RCT1 thought types. Surfaced as one-liners on guests and the global feed.</summary>
    public enum ThoughtType
    {
        Hungry, Thirsty, TooExpensive, QueuingTooLong, DisgustingPaths,
        GreatRide, CleanPark, Tired, NeedBathroom, UmbrellaNeeded
    }

    /// <summary>
    /// Per-guest recent-thoughts ring buffer (last 5) + a global feed (last 60)
    /// for the wrist UI. Static and SDK-free.
    /// VR hook: thought bubbles read guest.lastThought each frame.
    /// </summary>
    public static class ThoughtSystem
    {
        private const int RingSize = 5;
        private const int FeedSize = 60;

        private static readonly Dictionary<int, Queue<string>> _rings = new Dictionary<int, Queue<string>>();
        private static readonly Queue<string> _feed = new Queue<string>();

        /// <summary>Records a thought for a guest, updates its ring buffer and the global feed.</summary>
        public static void Think(GuestAgent guest, ThoughtType type, string subject = null)
        {
            if (guest == null) return;
            string text = Format(type, subject);
            guest.lastThought = text;

            if (!_rings.TryGetValue(guest.guestId, out Queue<string> ring))
            {
                ring = new Queue<string>();
                _rings[guest.guestId] = ring;
            }
            ring.Enqueue(text);
            while (ring.Count > RingSize) ring.Dequeue();

            _feed.Enqueue($"{guest.GuestName}: {text}");
            while (_feed.Count > FeedSize) _feed.Dequeue();
        }

        public static IReadOnlyCollection<string> RecentThoughts(GuestAgent guest)
        {
            if (guest != null && _rings.TryGetValue(guest.guestId, out Queue<string> ring))
                return ring;
            return System.Array.Empty<string>();
        }

        public static IReadOnlyCollection<string> GlobalFeed() => _feed;

        /// <summary>Called by GuestSpawner when a guest is pooled.</summary>
        public static void ForgetGuest(GuestAgent guest)
        {
            if (guest != null) _rings.Remove(guest.guestId);
        }

        private static string Format(ThoughtType type, string subject)
        {
            string s = string.IsNullOrEmpty(subject) ? "this" : subject;
            switch (type)
            {
                case ThoughtType.Hungry: return "I'm hungry.";
                case ThoughtType.Thirsty: return "I'm thirsty.";
                case ThoughtType.TooExpensive: return $"I'm not paying that much for {s}.";
                case ThoughtType.QueuingTooLong: return $"I've been queuing for {s} for ages.";
                case ThoughtType.DisgustingPaths: return "The paths here are disgusting.";
                case ThoughtType.GreatRide: return $"{s} was amazing!";
                case ThoughtType.CleanPark: return "This is a really clean park.";
                case ThoughtType.Tired: return "I need to sit down for a bit.";
                case ThoughtType.NeedBathroom: return "I need to find a bathroom!";
                case ThoughtType.UmbrellaNeeded: return "It's raining and I don't have an umbrella.";
                default: return "...";
            }
        }
    }
}
