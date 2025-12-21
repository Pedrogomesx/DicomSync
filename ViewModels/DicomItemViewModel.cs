namespace DicomSync.ViewModels
{
    public class DicomItemViewModel
    {
        public bool IsSelected { get; set; } = true;
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public string SeriesUID { get; set; }
    }
}