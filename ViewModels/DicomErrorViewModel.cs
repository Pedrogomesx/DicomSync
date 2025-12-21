namespace DicomSync.ViewModels
{
    public class DicomErrorViewModel
    {
        public string InstanceUID { get; set; }
        public string ErrorCode { get; set; }
        public string Description { get; set; }
        public string ProbableCause { get; set; }
        public string Time { get; set; }
    }
}