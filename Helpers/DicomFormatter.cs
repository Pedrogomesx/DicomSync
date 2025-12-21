using FellowOakDicom;
using FellowOakDicom.Network;
using System.Globalization;

namespace DicomSync.Helpers
{
    // Uma classe simples (DTO) apenas para transportar os dados do paciente
    public class PatientData
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string AccessionNumber { get; set; }
        public string BirthDate { get; set; }
        public string StudyDate { get; set; }
        public string StudyDescription { get; set; }
    }

    public static class DicomFormatter
    {
        // 1. Extrai dados sem saber o que é uma Janela ou TextBox
        public static PatientData ExtractPatientInfo(DicomDataset dataset)
        {
            return new PatientData
            {
                Name = dataset.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty),
                Id = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty),
                AccessionNumber = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty),
                BirthDate = dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, string.Empty),
                StudyDate = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, string.Empty),
                StudyDescription = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, string.Empty)
            };
        }
        // Converte yyyyMMdd para dd/MM/yyyy (Para exibição na UI)
        public static string FormatDicomDate(string dicomDate)
        {
            if (string.IsNullOrWhiteSpace(dicomDate) || dicomDate.Length != 8)
                return dicomDate;

            if (DateTime.TryParseExact(dicomDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                return date.ToString("dd/MM/yyyy");
            }
            return dicomDate;
        }

        // Converte dd/MM/yyyy para yyyyMMdd (Para salvar no arquivo DICOM)
        public static string ToDicomDate(string uiDate)
        {
            if (string.IsNullOrWhiteSpace(uiDate)) return string.Empty;

            // Remove barras ou pontos se o usuário digitou
            string cleanDate = uiDate.Replace("/", "").Replace("-", "").Replace(".", "");

            if (DateTime.TryParseExact(cleanDate, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                return date.ToString("yyyyMMdd");
            }
            return cleanDate; // Retorna o original se não conseguir converter
        }

        // 2. Traduz erros
        public static string GetFriendlyErrorMessage(DicomStatus status)
        {
            ushort code = status.Code;

            if (code == 0x0106 || code == 0x0120 || code == 0x0116)
                return $"Erro de Dados: Tag obrigatória ausente ({status.ErrorComment ?? "Unknown"}).";

            if (code == 0x0122 || (code >= 0xC000 && code <= 0xCFFF))
                return "Incompatibilidade: Compressão (Transfer Syntax) não suportada pelo destino.";

            if (code == 0xFE00 || code == 0x0110)
                return "Abortado: Operação interrompida.";

            return $"Erro DICOM Genérico (Code: {code:X4}).";
        }
    }
}