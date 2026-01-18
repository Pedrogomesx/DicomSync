using FellowOakDicom;
using FellowOakDicom.Network;
using Xunit;
using MyDicomService = DicomSync.Services.DicomService;

namespace DicomSync.Tests.Services
{
    public class DicomServiceTests
    {
        [Theory]
        [InlineData(0x0106, "Erro de sintaxe: Tag obrigatória vazia ou inválida.")]
        [InlineData(0x0122, "Transfer Syntax não suportada pelo PACS destino.")]
        [InlineData(0xC000, "Erro de Out of Resources ou falha no banco de dados do PACS.")]
        [InlineData(0xFE00, "Operação abortada pelo servidor remoto.")]
        [InlineData(0xFFFF, "Falha na operação (Status 0xFFFFH)")]
        public void GetFriendlyError_ShouldReturnCorrectMessage(ushort code, string expected)
        {
            // Use Lookup to get the status from code
            var status = DicomStatus.Lookup(code);
            var result = MyDicomService.GetFriendlyError(status);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GroupIntoSeries_ShouldGroupCorrectly()
        {
            // Arrange
            var dataset1 = new DicomDataset();
            dataset1.Add(DicomTag.SeriesInstanceUID, "1.2.3.1");
            dataset1.Add(DicomTag.SeriesDescription, "Series 1");
            var file1 = new DicomFile(dataset1);

            var dataset2 = new DicomDataset();
            dataset2.Add(DicomTag.SeriesInstanceUID, "1.2.3.1");
            // SeriesDescription can be missing or same
            var file2 = new DicomFile(dataset2);

            var dataset3 = new DicomDataset();
            dataset3.Add(DicomTag.SeriesInstanceUID, "1.2.3.2");
            dataset3.Add(DicomTag.SeriesDescription, "Series 2");
            var file3 = new DicomFile(dataset3);

            var files = new List<DicomFile> { file1, file2, file3 };

            // Act
            var result = MyDicomService.GroupIntoSeries(files);

            // Assert
            Assert.Equal(2, result.Count);

            var series1 = result.FirstOrDefault(x => x.SeriesUID == "1.2.3.1");
            Assert.NotNull(series1);
            Assert.Equal(2, series1.ImageCount);
            Assert.Equal("Series 1", series1.SeriesDescription);

            var series2 = result.FirstOrDefault(x => x.SeriesUID == "1.2.3.2");
            Assert.NotNull(series2);
            Assert.Equal(1, series2.ImageCount);
            Assert.Equal("Series 2", series2.SeriesDescription);
        }
    }
}
