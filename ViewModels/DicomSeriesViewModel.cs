namespace DicomSync.ViewModels
{
    public class DicomSeriesViewModel
    {
        public bool IsSelected { get; set; } = true;
        public string SeriesUID { get; set; }
        public string SeriesDescription { get; set; }
        public int ImageCount { get; set; }

        // Propriedade calculada para facilitar o Binding no XAML
        public string ImageCountInfo => $"{ImageCount} imagens nesta série";
    }
}