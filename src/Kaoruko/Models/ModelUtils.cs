using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WagahighChoices.Ashe;

namespace WagahighChoices.Kaoruko.Models
{
    public static class ModelUtils
    {
        public static ChoiceAction[] ParseChoices(string source)
        {
            return Array.ConvertAll(
                source.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                x => (ChoiceAction)int.Parse(x, CultureInfo.InvariantCulture)
            );
        }

        public static string ToChoicesString(IEnumerable<ChoiceAction> source)
        {
            return string.Join(",", source.Select(x => ((int)x).ToString(CultureInfo.InvariantCulture)));
        }

        public static int[] ParseSelections(string source)
        {
            return Array.ConvertAll(
               source.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
               x => int.Parse(x, CultureInfo.InvariantCulture)
           );
        }

        public static string ToSelectionsString(IEnumerable<int> source)
        {
            return string.Join(",", source.Select(x => x.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
