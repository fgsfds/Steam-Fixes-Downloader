﻿using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Common.Helpers
{
    public static class Extensions
    {
        public static ObservableCollection<T> AddRange<T>(this ObservableCollection<T> collection, List<T> list)
        {
            foreach (var item in list)
            {
                collection.Add(item);
            }

            return collection;
        }

        public static ObservableCollection<T> AddRange<T>(this ObservableCollection<T> collection, ObservableCollection<T> list)
        {
            foreach (var item in list)
            {
                collection.Add(item);
            }

            return collection;
        }

        public static void Move<T>(this List<T> list, int oldIndex, int newIndex)
        {
            var element = list[oldIndex];

            list.RemoveAt(oldIndex);
            list.Insert(newIndex, element);
        }

        public static ImmutableList<T> Move<T>(this ImmutableList<T> list, int oldIndex, int newIndex)
        {
            var element = list[oldIndex];

            list = list.RemoveAt(oldIndex);
            list = list.Insert(newIndex, element);

            return list;
        }

        public static List<string>? SplitSemicolonSeparatedString(this string? str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return null;
            }

            return [.. str.Split(';').Select(static x => x.Trim())];
        }

        public static string ReplaceDirectorySeparatorChar(this string str) => str.Replace('\\', Path.DirectorySeparatorChar);

        public static string ToSizeString(this long? size)
        {
            if (size is null)
            {
                return string.Empty;
            }
            else if (size < 1024)
            {
                return $"{size}B";
            }
            else if (size < 1024 * 1024)
            {
                return $"{size / 1024}Kb";
            }
            else if (size < 1024 * 1024 * 1024)
            {
                return $"{size / 1024 / 1024}Mb";
            }

            return $"{size / 1024 / 1024 / 1024}Gb";
        }
    }
}
