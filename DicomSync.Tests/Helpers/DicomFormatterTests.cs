using DicomSync.Helpers;
using Xunit;

namespace DicomSync.Tests.Helpers
{
    public class DicomFormatterTests
    {
        [Theory]
        [InlineData("20230101", "01/01/2023")]
        [InlineData("20231231", "31/12/2023")]
        [InlineData("20230228", "28/02/2023")]
        [InlineData("invalid", "invalid")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void FormatDicomDate_ShouldFormatCorrectly(string input, string expected)
        {
            var result = DicomFormatter.FormatDicomDate(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("01/01/2023", "20230101")]
        [InlineData("31/12/2023", "20231231")]
        [InlineData("28/02/2023", "20230228")]
        [InlineData("01012023", "20230101")]
        [InlineData("01-01-2023", "20230101")]
        [InlineData("01.01.2023", "20230101")]
        [InlineData("invalid", "invalid")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void ToDicomDate_ShouldFormatCorrectly(string input, string expected)
        {
            var result = DicomFormatter.ToDicomDate(input);
            Assert.Equal(expected, result);
        }
    }
}
