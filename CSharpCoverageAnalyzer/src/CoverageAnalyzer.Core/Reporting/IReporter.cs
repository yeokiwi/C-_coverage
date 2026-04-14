using CoverageAnalyzer.Core.Models;

namespace CoverageAnalyzer.Core.Reporting;

public interface IReporter
{
    void Write(CoverageReport report, string outputPath);
}
