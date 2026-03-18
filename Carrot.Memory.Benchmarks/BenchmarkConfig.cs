using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;

namespace Carrot.Memory.Benchmarks
{
    public class BenchmarkConfig : ManualConfig
    {
        public const int Cols = 8192;
        public const int PageSize = 8192; // 256MB per page
        public const int TotalRows = 32768; // 4 Pages = 1GB

        public BenchmarkConfig()
        {
            AddExporter(MarkdownExporter.GitHub);
            SummaryStyle = SummaryStyle.Default.WithMaxParameterColumnWidth(100);
            WithOptions(ConfigOptions.JoinSummary);
            Orderer = new DefaultOrderer(SummaryOrderPolicy.Declared);
        }
    }
}
