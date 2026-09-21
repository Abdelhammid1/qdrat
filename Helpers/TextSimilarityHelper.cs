using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace QdratNew.Helpers
{
    public sealed class TextSimilarityResult
    {
        public double Score { get; init; }
        public double TokenScore { get; init; }
        public double PhraseScore { get; init; }
        public double EditScore { get; init; }
        public double ContainmentScore { get; init; }
        public string MatchType { get; init; } = "ضعيف";
        public bool IsReliableMatch { get; init; }
    }

    public static class TextSimilarityHelper
    {
        private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex HiddenSpanRegex = new("<span[^>]*display\\s*:\\s*none[^>]*>.*?</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex TashkeelRegex = new("[\u064B-\u065F\u0670\u06D6-\u06ED]", RegexOptions.Compiled);
        private static readonly Regex TatweelRegex = new("\u0640+", RegexOptions.Compiled);
        private static readonly Regex NonWordRegex = new(@"[^\p{L}\p{Nd}\s]+", RegexOptions.Compiled);
        private static readonly Regex WhiteSpaceRegex = new(@"\s+", RegexOptions.Compiled);

        private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
        {
            "في", "من", "الى", "إلى", "على", "عن", "مع", "ثم", "او", "أو", "و", "ف", "ب", "ل",
            "ما", "ماذا", "كم", "هل", "اذا", "إذا", "اي", "أي", "الذي", "التي", "هذا", "هذه",
            "هو", "هي", "ان", "أن", "كان", "كانت", "يكون", "تكون", "كل", "عند", "بين",
            "قارن", "قارني", "مقارنه", "مقارنة", "القيمه", "القيمة", "الاولى", "الأولى", "الثانيه", "الثانية",
            "اختر", "اختاري", "الإجابة", "الاجابة", "الصحيحة", "مما", "يلي"
        };

        public static double CalculateSimilarity(string? s1, string? s2)
        {
            return Analyze(s1, s2).Score;
        }

        public static TextSimilarityResult Analyze(string? s1, string? s2)
        {
            var left = NormalizeForSimilarity(s1);
            var right = NormalizeForSimilarity(s2);

            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return new TextSimilarityResult();

            if (left == right)
            {
                return new TextSimilarityResult
                {
                    Score = 1,
                    TokenScore = 1,
                    PhraseScore = 1,
                    EditScore = 1,
                    ContainmentScore = 1,
                    MatchType = "تطابق كامل",
                    IsReliableMatch = true
                };
            }

            var leftTokens = Tokenize(left);
            var rightTokens = Tokenize(right);
            var tokenScore = WeightedTokenJaccard(leftTokens, rightTokens);
            var phraseScore = DiceCoefficient(GetCharacterNGrams(left, 3), GetCharacterNGrams(right, 3));
            var editScore = NormalizedLevenshtein(left, right);
            var containmentScore = CalculateContainmentScore(left, right, leftTokens, rightTokens);

            var score =
                (tokenScore * 0.46) +
                (phraseScore * 0.24) +
                (editScore * 0.20) +
                (containmentScore * 0.10);

            if (containmentScore >= 0.92 && tokenScore >= 0.68)
                score = Math.Max(score, 0.90);
            else if (tokenScore >= 0.84 && phraseScore >= 0.70)
                score = Math.Max(score, 0.88);
            else if (editScore >= 0.92 && phraseScore >= 0.82)
                score = Math.Max(score, 0.86);

            score = Math.Clamp(score, 0, 1);

            return new TextSimilarityResult
            {
                Score = Math.Round(score, 4),
                TokenScore = Math.Round(tokenScore, 4),
                PhraseScore = Math.Round(phraseScore, 4),
                EditScore = Math.Round(editScore, 4),
                ContainmentScore = Math.Round(containmentScore, 4),
                MatchType = ResolveMatchType(score, tokenScore, containmentScore),
                IsReliableMatch = score >= 0.78 || (tokenScore >= 0.80 && containmentScore >= 0.75)
            };
        }

        public static string NormalizeForSimilarity(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var decoded = WebUtility.HtmlDecode(text);
            decoded = HiddenSpanRegex.Replace(decoded, " ");
            decoded = HtmlTagRegex.Replace(decoded, " ");
            decoded = TashkeelRegex.Replace(decoded, string.Empty);
            decoded = TatweelRegex.Replace(decoded, string.Empty);

            var builder = new StringBuilder(decoded.Length);
            foreach (var character in decoded)
            {
                builder.Append(character switch
                {
                    'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                    'ى' => 'ي',
                    'ئ' => 'ي',
                    'ؤ' => 'و',
                    'ة' => 'ه',
                    '٠' => '0',
                    '١' => '1',
                    '٢' => '2',
                    '٣' => '3',
                    '٤' => '4',
                    '٥' => '5',
                    '٦' => '6',
                    '٧' => '7',
                    '٨' => '8',
                    '٩' => '9',
                    _ => char.ToLowerInvariant(character)
                });
            }

            var cleaned = NonWordRegex.Replace(builder.ToString(), " ");
            return WhiteSpaceRegex.Replace(cleaned, " ").Trim();
        }

        public static IReadOnlyCollection<string> GetSignificantTokens(string? text)
        {
            return Tokenize(NormalizeForSimilarity(text)).Keys.ToList();
        }

        private static Dictionary<string, int> Tokenize(string normalizedText)
        {
            return normalizedText
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => token.Length > 1 && !StopWords.Contains(token))
                .GroupBy(token => token)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        }

        private static double WeightedTokenJaccard(Dictionary<string, int> left, Dictionary<string, int> right)
        {
            if (left.Count == 0 || right.Count == 0)
                return 0;

            var allTokens = left.Keys.Union(right.Keys, StringComparer.Ordinal);
            var intersection = 0;
            var union = 0;

            foreach (var token in allTokens)
            {
                left.TryGetValue(token, out var leftCount);
                right.TryGetValue(token, out var rightCount);
                intersection += Math.Min(leftCount, rightCount);
                union += Math.Max(leftCount, rightCount);
            }

            return union == 0 ? 0 : (double)intersection / union;
        }

        private static HashSet<string> GetCharacterNGrams(string text, int size)
        {
            var compact = text.Replace(" ", string.Empty);
            if (compact.Length == 0)
                return new HashSet<string>(StringComparer.Ordinal);

            if (compact.Length <= size)
                return new HashSet<string>(new[] { compact }, StringComparer.Ordinal);

            var grams = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i <= compact.Length - size; i++)
                grams.Add(compact.Substring(i, size));

            return grams;
        }

        private static double DiceCoefficient(HashSet<string> left, HashSet<string> right)
        {
            if (left.Count == 0 || right.Count == 0)
                return 0;

            var matches = left.Intersect(right, StringComparer.Ordinal).Count();
            return (2.0 * matches) / (left.Count + right.Count);
        }

        private static double CalculateContainmentScore(
            string left,
            string right,
            Dictionary<string, int> leftTokens,
            Dictionary<string, int> rightTokens)
        {
            var shorter = left.Length <= right.Length ? left : right;
            var longer = left.Length > right.Length ? left : right;

            var phraseContainment = longer.Contains(shorter, StringComparison.Ordinal)
                ? (double)shorter.Length / Math.Max(longer.Length, 1)
                : 0;

            var smallerTokens = leftTokens.Count <= rightTokens.Count ? leftTokens : rightTokens;
            var largerTokens = leftTokens.Count > rightTokens.Count ? leftTokens : rightTokens;
            if (smallerTokens.Count == 0 || largerTokens.Count == 0)
                return phraseContainment;

            var containedTokens = smallerTokens.Keys.Count(largerTokens.ContainsKey);
            var tokenContainment = (double)containedTokens / smallerTokens.Count;

            return Math.Max(phraseContainment, tokenContainment);
        }

        private static double NormalizedLevenshtein(string left, string right)
        {
            var maxLength = Math.Max(left.Length, right.Length);
            if (maxLength == 0)
                return 1;

            var distance = LevenshteinDistance(left, right);
            return 1.0 - ((double)distance / maxLength);
        }

        private static int LevenshteinDistance(string source, string target)
        {
            if (source.Length == 0) return target.Length;
            if (target.Length == 0) return source.Length;

            var previous = new int[target.Length + 1];
            var current = new int[target.Length + 1];

            for (var j = 0; j <= target.Length; j++)
                previous[j] = j;

            for (var i = 1; i <= source.Length; i++)
            {
                current[0] = i;

                for (var j = 1; j <= target.Length; j++)
                {
                    var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                    current[j] = Math.Min(
                        Math.Min(current[j - 1] + 1, previous[j] + 1),
                        previous[j - 1] + cost);
                }

                (previous, current) = (current, previous);
            }

            return previous[target.Length];
        }

        private static string ResolveMatchType(double score, double tokenScore, double containmentScore)
        {
            if (score >= 0.94)
                return "تطابق شبه كامل";

            if (score >= 0.86)
                return "تشابه قوي";

            if (score >= 0.78)
                return "تشابه محتمل";

            if (tokenScore >= 0.75 || containmentScore >= 0.80)
                return "تشابه جزئي";

            return "ضعيف";
        }
    }
}
