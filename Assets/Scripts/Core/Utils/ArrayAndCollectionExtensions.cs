using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;

namespace CoxlinCore
{
    public static class ArrayAndCollectionExtensions
    {
        public static T Find<T>(this T[] array, Predicate<T> predicate)
        {
            return Array.Find(array, predicate);
        }

        public static bool IndexIsAtOrGreaterThanLastIndex<T>(this T[] array, int index)
        {
            return index >= array.Length;
        }

        public static void ResetIndexIfItIsAtOrGreaterThanLastIndex<T>(this T[] array, ref int index)
        {
            if (IndexIsAtOrGreaterThanLastIndex(array, index))
            {
                index = 0;
            }
        }

        public static T First<T>(this T[] array)
        {
            return array[0];
        }

        public static T Last<T>(this T[] array)
        {
            return array[array.Length - 1];
        }

        public static T First<T>(this IList<T> list)
        {
            return list[0];
        }

        public static T Last<T>(this IList<T> list)
        {
            return list[list.Count - 1];
        }

        public static bool IsNullOrEmpty<T>(this IList<T> list)
        {
            return list == null || list.Count == 0;
        }

        public static bool IsNotNullOrEmpty<T>(this IList<T> list)
        {
            return list != null || list.Count != 0;
        }

        public static T[] SetAllValues<T>(this T[] array, T value)
        {
            for(int i = 0; i < array.Length; ++i)
            {
                array[i] = value;
            }
            return array;
        }

        public static IList<T> SetAllValues<T>(this IList<T> list, T value)
        {
            for(int i = 0; i < list.Count; ++i)
            {
                list[i] = value;
            }
            return list;
        }

        public static bool HasXElements<T>(this IEnumerable<T> source, int elementAmount)
        {
            return source.Count() == elementAmount;
        }

        public static bool IndexIsInRange<T>(this T[] array, int index)
        {
            return index >= 0 && index < array.Length && array != null;
        }

        public static bool IndexIsInRange<T>(this List<T> list, int index)
        {
            return index >= 0 && index < list.Count && list != null;
        }

        public static ReadOnlyCollection<T> ToReadOnly<T>(this T[] array)
        {
            return new ReadOnlyCollection<T>(array);
        }

        public static ReadOnlyCollection<T> ToReadOnly<T>(this IList<T> list)
        {
            return new ReadOnlyCollection<T>(list);
        }

        public static T[] Flatten<T>(this T[,] array)
        {
            List<T> list = array.Cast<T>().ToList();
            return list.ToArray();
        }

        public static T[,] To2D<T>(this T[] flatArray, int width)
        {
            int height = (int)Math.Ceiling(flatArray.Length / (double)width);
            T[,] result = new T[height, width];
            int rowIndex, colIndex;

            for (int index = 0; index < flatArray.Length; index++)
            {
                rowIndex = index / width;
                colIndex = index % width;
                result[rowIndex, colIndex] = flatArray[index];
            }
            return result;
        }

        public static void Initialise<T>(this List<T> currentList, List<T> list)
        {
            currentList.Clear();
            currentList.AddRange(list);
        }

        public static void EnqueueAll<T>(this Queue<T> q, ICollection<T> collection)
        {
            foreach (var c in collection)
            {
                q.Enqueue(c);
            }
        }

        public static bool ContainsAll<T>(this HashSet<T> hashset, ICollection<T> collection)
        {
            foreach (var item in collection)
            {
                if (!hashset.Contains(item))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
