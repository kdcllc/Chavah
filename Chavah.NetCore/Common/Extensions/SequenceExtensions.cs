﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

using Optional;

using Raven.Client.Documents;

namespace BitShuva.Chavah.Common
{
    public static class SequenceExtensions
    {
        private static readonly Random Random = new Random();

        public static async Task<Option<TSource>> FirstOrNoneAsync<TSource>(this IQueryable<TSource> source)
        {
            var result = await source.FirstOrDefaultAsync();
            return result.SomeNotNull();
        }

        public static Task<Option<TSource>> FirstOrNoneAsync<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate)
        {
            return source.Where(predicate).FirstOrNoneAsync();
        }

        /// <summary>
        /// Finds the item whose selector returns the highest value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static Option<T> FindMax<T>(this IEnumerable<T> items, Func<T, double> selector)
        {
            var maxItem = default(T);
            var maxVal = double.NegativeInfinity;
            foreach (var item in items)
            {
                var val = selector(item);
                if (val > maxVal)
                {
                    maxItem = item;
                    maxVal = val;
                }
            }

            return maxItem.SomeNotNull();
        }

        public static T RandomElement<T>(this IEnumerable<T> items)
        {
            var collection = items as ICollection<T> ?? new List<T>(items);

            if (collection.Count == 0)
            {
                return default;
            }

            var randomElementIndex = Random.Next(0, collection.Count);
            return items.ElementAtOrDefault(randomElementIndex);
        }
    }
}