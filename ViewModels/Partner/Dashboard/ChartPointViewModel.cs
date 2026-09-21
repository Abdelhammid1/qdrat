namespace QdratNew.ViewModels.Partner.Dashboard
{
    public class ChartPointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class ChartBarViewModel
    {
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class ChartSegmentViewModel
    {
        public string Segment { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class StackedBarViewModel
    {
        public string Name { get; set; } = string.Empty;

        public int Completed { get; set; }
        public int Late { get; set; }
        public int NotStarted { get; set; }
    }

    public class HorizontalBarViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
