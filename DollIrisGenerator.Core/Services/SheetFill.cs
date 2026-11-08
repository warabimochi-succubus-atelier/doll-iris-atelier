using System;
using System.Collections.Generic;

namespace DollIrisGenerator.Services
{
    /// <summary>
    /// Pure arithmetic behind "fill the A4 sheet": how many pairs the sheet should hold, and how to share
    /// the empty slots among the designs already on it so their quantity ratio is kept.
    /// </summary>
    public static class SheetFill
    {
        /// <summary>
        /// Smallest whole number of pages that holds <paramref name="totalPairs"/> (at least one page),
        /// expressed in pairs. A sheet whose last page is partly empty is rounded up to fill that page.
        /// </summary>
        public static int TargetPairs(int totalPairs, int pairsPerPage)
        {
            if (pairsPerPage < 1) throw new ArgumentOutOfRangeException(nameof(pairsPerPage));
            int pages = Math.Max(1, (int)Math.Ceiling(Math.Max(0, totalPairs) / (double)pairsPerPage));
            return pages * pairsPerPage;
        }

        /// <summary>
        /// Grows <paramref name="counts"/> until they add up to <paramref name="targetTotal"/>, keeping the
        /// ratio between them as close as possible. Counts never shrink, and no count is raised above
        /// <paramref name="maxPerItem"/> (so the total can stay short of the target when every item is capped).
        /// Each slot goes to the item whose count is furthest below its share; ties go to the earlier item.
        /// </summary>
        public static int[] Distribute(IReadOnlyList<int> counts, int targetTotal, int maxPerItem)
        {
            var result = new int[counts.Count];
            int total = 0;
            for (int i = 0; i < counts.Count; i++)
            {
                result[i] = counts[i];
                total += counts[i];
            }

            while (total < targetTotal)
            {
                int best = -1;
                for (int i = 0; i < result.Length; i++)
                {
                    if (result[i] >= maxPerItem) continue;
                    // Lowest result / weight wins; compared by cross-multiplication to stay in integers
                    if (best < 0 || (long)result[i] * Weight(counts[best]) < (long)result[best] * Weight(counts[i]))
                        best = i;
                }
                if (best < 0) break;
                result[best]++;
                total++;
            }
            return result;
        }

        // An item with a zero (or negative) quantity still gets a share, as if it had one pair
        private static int Weight(int count) => Math.Max(1, count);
    }
}
