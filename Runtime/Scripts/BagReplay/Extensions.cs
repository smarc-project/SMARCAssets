using System.Collections.Generic;

namespace BagReplay
{
    public static class Extensions
    {
        public static T GetLatestMessage<T>(this SortedList<long, T> list, double timestamp)
        {
            if (list.Count == 0) return default;

            var keys = list.Keys;
            int lo = 0, hi = keys.Count - 1;

            // Binary search for first key > timestamp
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                if (keys[mid] <= timestamp)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            // lo now points to the first key > timestamp
            if (lo < keys.Count)
                return list.Values[lo];
            return list.Values[^1]; // No key > timestamp ⇒ return latest (max) value
        }
    }
}