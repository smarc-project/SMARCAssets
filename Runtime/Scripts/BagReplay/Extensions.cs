using System.Collections.Generic;

namespace BagReplay
{
    public static class Extensions
    {
        public static T GetLatestMessage<T>(this SortedList<long, T> list, double timestamp)
        {
            if (list.Count == 0)
            {
                return default;
            }

            var keys = list.Keys;
            int lo = 0;
            int hi = keys.Count - 1;

            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                if (keys[mid] <= timestamp)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            if (hi >= 0)
            {
                return list.Values[hi];
            }

            return list.Values[0];
        }
    }
}
