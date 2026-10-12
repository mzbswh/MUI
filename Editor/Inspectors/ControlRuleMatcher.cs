using System;
using System.Text.RegularExpressions;
using MUI.UGUI;

namespace MUI.Editor
{
    internal static class ControlRuleMatcher
    {
        internal static bool TryMatch(ControlNamingRule rule, string input, out string name)
        {
            name = input;
            if (string.IsNullOrEmpty(rule.Prefix)) return false;
            switch (rule.Match)
            {
                case ControlNameMatch.Prefix:
                    if (!input.StartsWith(rule.Prefix, StringComparison.Ordinal)) return false;
                    name = input.Substring(rule.Prefix.Length);
                    return true;
                case ControlNameMatch.Suffix:
                    if (!input.EndsWith(rule.Prefix, StringComparison.Ordinal)) return false;
                    name = input.Substring(0, input.Length - rule.Prefix.Length);
                    return true;
                case ControlNameMatch.Contains:
                    var index = input.IndexOf(rule.Prefix, StringComparison.Ordinal);
                    if (index < 0) return false;
                    name = input.Remove(index, rule.Prefix.Length);
                    return true;
                case ControlNameMatch.Regex:
                    var match = Regex.Match(input, rule.Prefix, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                    if (!match.Success) return false;
                    if (!match.Groups["name"].Success || string.IsNullOrEmpty(match.Groups["name"].Value))
                        throw new ArgumentException("正则必须用 (?<name>...) 捕获非空的绑定名称。");
                    name = match.Groups["name"].Value;
                    return true;
                default: throw new ArgumentException("未知的控件名称匹配方式。");
            }
        }
    }
}
