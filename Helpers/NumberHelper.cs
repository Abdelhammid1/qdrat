using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace QdratNew.Helpers
{
    public static class NumberHelper
    {


        /// <summary>
        /// يحوّل الأرقام العربية-الهندية (٠-٩) والفارسية (۰-۹) إلى لاتينية ويحذف المسافات — للتحقق من الجوال.
        /// </summary>
        public static string NormalizeDigits(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (c >= '٠' && c <= '٩') sb.Append((char)('0' + (c - '٠')));
                else if (c >= '۰' && c <= '۹') sb.Append((char)('0' + (c - '۰')));
                else if (!char.IsWhiteSpace(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        public static string ToIndicNumbersSafe(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            var englishToIndic = new Dictionary<char, char>
            {
                ['0'] = '٠',
                ['1'] = '١',
                ['2'] = '٢',
                ['3'] = '٣',
                ['4'] = '٤',
                ['5'] = '٥',
                ['6'] = '٦',
                ['7'] = '٧',
                ['8'] = '٨',
                ['9'] = '٩'
            };

            var result = new StringBuilder();
            bool insideTag = false;

            foreach (char c in input)
            {
                if (c == '<') insideTag = true;

                if (!insideTag && englishToIndic.ContainsKey(c))
                    result.Append(englishToIndic[c]);
                else
                    result.Append(c);

                if (c == '>') insideTag = false;
            }

            return result.ToString();
        }

        // =============================
        //   1) التحويل السريع الآمن
        // =============================
        public static string ToIndicNumbersInsideHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return html;

            var englishToIndic = new Dictionary<char, char>
            {
                ['0'] = '٠',
                ['1'] = '١',
                ['2'] = '٢',
                ['3'] = '٣',
                ['4'] = '٤',
                ['5'] = '٥',
                ['6'] = '٦',
                ['7'] = '٧',
                ['8'] = '٨',
                ['9'] = '٩'
            };

            var result = new StringBuilder();
            bool insideTag = false;

            foreach (char c in html)
            {
                if (c == '<') insideTag = true;

                if (!insideTag && englishToIndic.ContainsKey(c))
                    result.Append(englishToIndic[c]);
                else
                    result.Append(c);

                if (c == '>') insideTag = false;
            }

            return result.ToString();
        }



        // =====================================================
        //   2) التحويل الذكي مع الحفاظ على المعادلات الحساسة
        // =====================================================
        //
        //   ✔ يحوّل النصوص خارج الوسوم
        //   ✔ يحوّل النصوص داخل span العادي (بدون style)
        //   ✘ لا يحوّل داخل span يحتوي style → مهم للكسور
        //   ✘ لا يحوّل داخل math-like structures
        //
        // =====================================================

        public static string ConvertNumbersOnly(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            var regex = new Regex(@"(?<=>)[^<>]+?(?=<|$)", RegexOptions.Compiled);

            var map = new Dictionary<char, char>
            {
                ['0'] = '٠',
                ['1'] = '١',
                ['2'] = '٢',
                ['3'] = '٣',
                ['4'] = '٤',
                ['5'] = '٥',
                ['6'] = '٦',
                ['7'] = '٧',
                ['8'] = '٨',
                ['9'] = '٩'
            };

            return regex.Replace(input, match =>
            {
                string text = match.Value;

                // ❌ لا نلمس أي نص مرتبط بتنسيقات كسور حساسة
                if (Regex.IsMatch(text, @"style\s*="))
                    return text;

                // ❌ لا نحول داخل math-like
                if (Regex.IsMatch(text, @"frac|latex|math", RegexOptions.IgnoreCase))
                    return text;

                // ✔ تحويل الأرقام في بقية النص
                var output = new StringBuilder();

                foreach (char c in text)
                {
                    output.Append(map.ContainsKey(c) ? map[c] : c);
                }

                return output.ToString();
            });
        }



        // =====================================================
        //   3) دالة نهائية تُستخدم في عرض السؤال بالكامل
        // =====================================================
        //
        //   ✔ تحافظ على HTML
        //   ✔ تحافظ على الكسور والمعادلات
        //   ✔ تحوّل الأرقام متى كان ذلك آمنًا
        //
        // =====================================================

        public static string ConvertHtmlPreservingTags(string html, bool isQuantitative)
        {
            if (string.IsNullOrWhiteSpace(html)) return html;

            if (!isQuantitative)
                return html; // أسئلة غير كمية — نتركها كما هي

            try
            {
                // المرحلة 1: تحويل الأرقام خارج الوسوم
                string step1 = ToIndicNumbersSafe(html);

                // المرحلة 2: التحويل الذكي داخل النصوص
                string step2 = ConvertNumbersOnly(step1);

                return step2;
            }
            catch
            {
                // إذا حدثت مشكلة نعيد النص الأصلي بدون تدخل
                return html;
            }
        }
    }
}
